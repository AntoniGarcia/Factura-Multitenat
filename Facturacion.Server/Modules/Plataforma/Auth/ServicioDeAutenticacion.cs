using System.Diagnostics;
using System.Security.Claims;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>Sesión recién abierta o renovada, con lo que hay que plantar en la cookie.</summary>
public sealed record SesionIniciada(RespuestaSesion Respuesta, string RefreshEnClaro, TimeSpan DuracionCookie);

/// <summary>
/// El circuito de identidad completo: iniciar sesión, refrescar, cerrar y cambiar de empresa.
/// </summary>
public sealed class ServicioDeAutenticacion(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    ServicioDeTokens tokens,
    ServicioDeRefreshTokens refrescos,
    IServicioDeBitacora bitacora,
    IControlDeIntentos intentos,
    IHttpContextAccessor accesor,
    IOptions<OpcionesDeJwt> opciones)
{
    private readonly OpcionesDeJwt _opciones = opciones.Value;

    /// <summary>
    /// Piso de tiempo de <c>/iniciar-sesion</c>.
    ///
    /// <para><b>Por qué no basta con el hash señuelo</b></para>
    /// El señuelo iguala el costo del hash, que es lo caro, pero no todo lo demás: cuando el
    /// usuario existe, el camino de fallo hace un <c>UPDATE</c> extra para contar el intento;
    /// cuando no existe, no lo hace. Medido con 50 muestras de cada tipo, eso dejaba un sesgo
    /// consistente de ~1.2 ms. Parece poco, pero un sesgo consistente se extrae promediando:
    /// con diez mil intentos el ruido baja como la raíz de n y el sesgo queda muy por encima.
    /// Y en un SaaS de facturación, saber qué correos están dados de alta es la lista de
    /// clientes de alguien.
    ///
    /// <para><b>Qué hace el piso</b></para>
    /// Toda respuesta tarda lo mismo, sin importar por dónde pasó. Así el tiempo deja de
    /// depender del código, y las fases que agreguen trabajo aquí —la 8 toca este camino— no
    /// pueden reintroducir el oráculo sin darse cuenta.
    ///
    /// <para><b>Lo que el piso no cubre</b></para>
    /// Si el trabajo real llegara a superar el piso —una base lenta, mucha carga—, el relleno
    /// es cero y la diferencia vuelve a asomar. Por eso el piso está muy por encima del
    /// tiempo observado (~73 ms de media, ~77 ms en el percentil 90) y no pegado a él.
    /// </summary>
    private static readonly TimeSpan PisoDeRespuesta = TimeSpan.FromMilliseconds(250);

    // Hash de una contraseña que no es de nadie, para gastar el mismo tiempo cuando el correo
    // no existe que cuando existe. Se calcula con el MISMO hasher que usa el camino real
    // —el de UserManager, con las opciones configuradas—, no con uno nuevo por omisión: si
    // difirieran en número de iteraciones, el señuelo costaría distinto que lo que imita.
    private static string? _hashSenuelo;

    private static Usuario UsuarioSenuelo => new() { Nombre = "señuelo" };

    // Mismo texto y mismo costo tanto si el correo no existe como si la contraseña está mal.
    private static ErrorNegocio CredencialesInvalidas()
        => ErrorNegocio.Validacion("credenciales-invalidas", "El correo o la contraseña no son correctos.");

    public async Task<Resultado<SesionIniciada>> IniciarSesionAsync(
        PeticionInicioSesion peticion, string? ip, string? agente, CancellationToken ct)
    {
        var marca = Stopwatch.GetTimestamp();

        try
        {
            return await Intentar(peticion, ip, agente, ct);
        }
        finally
        {
            await Nivelar(marca);
        }
    }

    private async Task<Resultado<SesionIniciada>> Intentar(
        PeticionInicioSesion peticion, string? ip, string? agente, CancellationToken ct)
    {
        var claveIp = ip ?? "desconocida";

        if (intentos.BloqueoRestante(claveIp) is { } esperaIp)
            return ErrorNegocio.LimiteExcedido(
                "demasiados-intentos",
                $"Demasiados intentos fallidos desde esta red. Vuelve a intentar en {Redondear(esperaIp)}.");

        var usuario = await usuarios.FindByEmailAsync(peticion.Correo);

        var contrasenaCorrecta = usuario is null
            ? VerificarSenuelo(peticion.Contrasena)
            : await usuarios.CheckPasswordAsync(usuario, peticion.Contrasena);

        if (usuario is null || !contrasenaCorrecta)
        {
            await RegistrarFallo(usuario, claveIp, peticion.Correo, ct);
            return CredencialesInvalidas();
        }

        // El bloqueo solo se revela a quien acertó la contraseña. Decirlo antes convertiría
        // el mensaje en un detector de correos dados de alta.
        if (await usuarios.IsLockedOutAsync(usuario))
        {
            var espera = usuario.LockoutEnd!.Value.UtcDateTime - DateTime.UtcNow;
            return ErrorNegocio.LimiteExcedido(
                "cuenta-bloqueada",
                $"La cuenta está bloqueada por intentos fallidos. Vuelve a intentar en {Redondear(espera)}.");
        }

        if (!usuario.Activo)
            return ErrorNegocio.Regla("usuario-desactivado", "Tu usuario está desactivado. Pide a tu administrador que lo reactive.");

        await usuarios.ResetAccessFailedCountAsync(usuario);
        intentos.Limpiar(claveIp);

        if (usuario.BloqueosConsecutivos != 0)
        {
            usuario.BloqueosConsecutivos = 0;
            await usuarios.UpdateAsync(usuario);
        }

        var duracion = peticion.MantenerSesion
            ? TimeSpan.FromDays(_opciones.DiasDeSesionLarga)
            : TimeSpan.FromHours(_opciones.HorasDeSesionCorta);

        var refresh = refrescos.CrearFamilia(usuario.Id, duracion, ip, agente);
        var respuesta = await ConstruirRespuesta(usuario, empresaPreferida: null, refresh.Registro.FamiliaId, ct);

        // Con una sola empresa la sesión ya nace con ella activa; queda anotada en la familia
        // para que los refrescos posteriores la conserven.
        refresh.Registro.EmpresaActivaId = respuesta.Sesion.EmpresaActivaId;

        bitacora.Registrar(
            EntidadesDeBitacora.Sesion,
            usuario.Id.ToString(),
            AccionesDeBitacora.InicioSesion,
            despues: new { refresh.Registro.FamiliaId, respuesta.Sesion.EmpresaActivaId, peticion.MantenerSesion },
            empresaId: respuesta.Sesion.EmpresaActivaId,
            usuarioId: usuario.Id,
            cuentaId: usuario.CuentaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return new SesionIniciada(respuesta, refresh.EnClaro, duracion);
    }

    public async Task<Resultado<SesionIniciada>> RefrescarAsync(
        string refreshEnClaro, string? ip, string? agente, CancellationToken ct)
    {
        var rotado = await refrescos.RotarAsync(refreshEnClaro, ip, agente, ct);

        if (rotado.EsFallo)
            return rotado.Error!;

        var registro = rotado.Valor.Registro;

        var usuario = await usuarios.FindByIdAsync(registro.UsuarioId.ToString());

        if (usuario is null || !usuario.Activo)
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

        // La empresa activa viene de la familia, no del navegador. Si el usuario ya no tiene
        // acceso a ella, ConstruirSesion la descarta y lo devuelve al selector.
        var respuesta = await ConstruirRespuesta(usuario, registro.EmpresaActivaId, registro.FamiliaId, ct);

        // La duración de la cookie se conserva: la familia caduca cuando dijo que caducaría.
        var restante = registro.ExpiraUtc - DateTime.UtcNow;

        return new SesionIniciada(respuesta, rotado.Valor.EnClaro, restante);
    }

    public async Task CerrarSesionAsync(string? refreshEnClaro, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshEnClaro)) return;

        var hash = ServicioDeRefreshTokens.Hash(refreshEnClaro);

        var token = await baseDeDatos.RefreshTokens
            .SingleOrDefaultAsync(t => t.HashToken == hash, ct);

        // Cerrar una sesión que ya no existe no es un error: el resultado que el usuario
        // pidió —quedar fuera— ya se cumplió.
        if (token is null) return;

        await refrescos.InvalidarFamiliaAsync(token.FamiliaId, "cierre de sesión", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.Sesion,
            token.UsuarioId.ToString(),
            AccionesDeBitacora.CierreSesion,
            despues: new { token.FamiliaId },
            usuarioId: token.UsuarioId);

        await baseDeDatos.SaveChangesAsync(ct);
    }

    public async Task<Resultado<RespuestaSesion>> CambiarEmpresaAsync(Guid empresaId, CancellationToken ct)
    {
        var (usuario, familiaId) = await LeerDelToken(ct);

        if (usuario is null || familiaId is null)
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

        var tieneAcceso = await baseDeDatos.UsuariosEmpresas.AnyAsync(
            ue => ue.UsuarioId == usuario.Id
                  && ue.EmpresaId == empresaId
                  && ue.Activo
                  && ue.Empresa.Activa
                  && ue.Empresa.CuentaId == usuario.CuentaId,
            ct);

        // Mismo error que si la empresa no existiera: quien no tiene acceso tampoco tiene
        // por qué averiguar si el identificador que probó corresponde a una empresa real.
        if (!tieneAcceso)
            return ErrorNegocio.NoEncontrado("empresa-no-disponible", "No tienes acceso a esa empresa.");

        var respuesta = await ConstruirRespuesta(usuario, empresaId, familiaId.Value, ct);

        // La cookie no se toca: solo se recuerda del lado del servidor cuál quedó activa.
        await refrescos.FijarEmpresaActivaAsync(familiaId.Value, empresaId, ct);

        bitacora.Registrar(
            EntidadesDeBitacora.Sesion,
            usuario.Id.ToString(),
            AccionesDeBitacora.EmpresaCambiada,
            despues: new { EmpresaId = empresaId },
            empresaId: empresaId,
            usuarioId: usuario.Id,
            cuentaId: usuario.CuentaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return respuesta;
    }

    public async Task<Resultado<SesionDto>> ObtenerSesionAsync(CancellationToken ct)
    {
        var (usuario, _) = await LeerDelToken(ct);

        if (usuario is null || !usuario.Activo)
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

        var empresaActual = LeerGuidDelToken(ClavesDeClaim.Empresa);

        return await ConstruirSesion(usuario, empresaActual, ct);
    }

    private async Task<RespuestaSesion> ConstruirRespuesta(
        Usuario usuario, Guid? empresaPreferida, Guid familiaId, CancellationToken ct)
    {
        var sesion = await ConstruirSesion(usuario, empresaPreferida, ct);

        var token = tokens.Emitir(
            usuario, usuario.CuentaId, sesion.EmpresaActivaId, sesion.Permisos, familiaId);

        return new RespuestaSesion(token.Token, token.ExpiraUtc, sesion);
    }

    private async Task<SesionDto> ConstruirSesion(Usuario usuario, Guid? empresaPreferida, CancellationToken ct)
    {
        var empresas = await baseDeDatos.UsuariosEmpresas
            .Where(ue => ue.UsuarioId == usuario.Id
                         && ue.Activo
                         && ue.Empresa.Activa
                         && ue.Empresa.CuentaId == usuario.CuentaId)
            .OrderBy(ue => ue.Empresa.NombreFiscal)
            .Select(ue => new EmpresaDisponibleDto(ue.EmpresaId, ue.Empresa.NombreFiscal, ue.Empresa.Rfc))
            .ToListAsync(ct);

        // Con una sola empresa no tiene sentido preguntar cuál: el token ya sale con ella.
        Guid? activa = empresaPreferida is { } preferida && empresas.Any(e => e.Id == preferida)
            ? preferida
            : empresas.Count == 1 ? empresas[0].Id : null;

        var permisos = activa is { } empresa
            ? await baseDeDatos.UsuariosEmpresasPermisos
                .Where(p => p.UsuarioId == usuario.Id && p.EmpresaId == empresa)
                .Select(p => p.PermisoClave)
                .ToListAsync(ct)
            : [];

        return new SesionDto(
            usuario.Id,
            usuario.Nombre,
            usuario.Email ?? string.Empty,
            activa,
            empresas,
            permisos);
    }

    private async Task RegistrarFallo(Usuario? usuario, string claveIp, string correo, CancellationToken ct)
    {
        intentos.RegistrarFallo(claveIp);

        if (usuario is not null)
        {
            await usuarios.AccessFailedAsync(usuario);

            // Identity solo sabe bloquear por un plazo fijo. El castigo creciente se aplica
            // aquí, sobre el bloqueo que Identity acaba de poner.
            if (await usuarios.IsLockedOutAsync(usuario))
            {
                usuario.BloqueosConsecutivos++;
                await usuarios.SetLockoutEndDateAsync(
                    usuario, DateTimeOffset.UtcNow.Add(DuracionDeBloqueo(usuario.BloqueosConsecutivos)));
            }
        }

        // El correo tecleado se guarda para poder investigar un ataque, pero el usuario puede
        // no existir: por eso el registro va sin UsuarioId cuando no lo hay.
        bitacora.Registrar(
            EntidadesDeBitacora.Sesion,
            usuario?.Id.ToString(),
            AccionesDeBitacora.InicioSesionFallido,
            despues: new { Correo = correo, UsuarioExiste = usuario is not null },
            usuarioId: usuario?.Id,
            cuentaId: usuario?.CuentaId);

        await baseDeDatos.SaveChangesAsync(ct);
    }

    private static TimeSpan DuracionDeBloqueo(int bloqueosConsecutivos) => bloqueosConsecutivos switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromHours(1)
    };

    private bool VerificarSenuelo(string contrasena)
    {
        // La primera petición del proceso paga un hash extra al calcular el señuelo. El piso
        // de tiempo la absorbe, así que tampoco esa se distingue.
        _hashSenuelo ??= usuarios.PasswordHasher.HashPassword(UsuarioSenuelo, "no-es-la-contrasena-de-nadie");

        usuarios.PasswordHasher.VerifyHashedPassword(UsuarioSenuelo, _hashSenuelo, contrasena);

        return false;
    }

    /// <summary>
    /// Rellena hasta <see cref="PisoDeRespuesta"/>. Se aplica a <b>todas</b> las salidas de
    /// inicio de sesión, también a las que hoy no filtran nada: una sola regla es más fácil
    /// de sostener que una lista de qué caminos sí y cuáles no.
    /// </summary>
    private static async Task Nivelar(long marcaInicial)
    {
        var transcurrido = Stopwatch.GetElapsedTime(marcaInicial);

        if (transcurrido >= PisoDeRespuesta) return;

        // Sin el token de cancelación a propósito: si la petición se abortó, la respuesta se
        // descarta igual, y lanzar desde aquí ocultaría el resultado real.
        await Task.Delay(PisoDeRespuesta - transcurrido, CancellationToken.None);
    }

    private async Task<(Usuario? Usuario, Guid? FamiliaId)> LeerDelToken(CancellationToken ct)
    {
        var usuarioId = LeerGuidDelToken(ClavesDeClaim.Usuario);

        if (usuarioId is null)
            return (null, null);

        var usuario = await baseDeDatos.Users.SingleOrDefaultAsync(u => u.Id == usuarioId.Value, ct);

        return (usuario, LeerGuidDelToken(ClavesDeClaim.Familia));
    }

    private Guid? LeerGuidDelToken(string claim)
        => Guid.TryParse(accesor.HttpContext?.User.FindFirstValue(claim), out var valor) ? valor : null;

    private static string Redondear(TimeSpan espera)
    {
        var minutos = (int)Math.Ceiling(espera.TotalMinutes);
        return minutos <= 1 ? "un minuto" : $"{minutos} minutos";
    }
}
