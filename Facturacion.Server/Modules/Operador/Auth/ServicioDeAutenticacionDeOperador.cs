using System.Diagnostics;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>Sesión de operador recién abierta: lo que se devuelve más el refresh que va a la cookie.</summary>
public sealed record SesionDeOperadorIniciada(
    RespuestaSesionDeOperador Respuesta,
    string RefreshEnClaro,
    TimeSpan DuracionRefresh);

/// <summary>
/// Inicio y cierre de sesión del panel de operador.
///
/// <para><b>Mismo endurecimiento que el del inquilino</b></para>
/// Piso de tiempo en la respuesta, hash señuelo cuando el correo no existe, límite por IP y
/// bloqueo creciente. Aquí importa más, no menos: desde este panel se acredita dinero, así que
/// es el objetivo más rentable de todo el sistema.
///
/// <para><b>Por qué no usa UserManager</b></para>
/// <c>UserManager</c> está atado a <see cref="Usuario"/>, que exige cuenta contratante. Se usa
/// suelto el mismo <c>IPasswordHasher</c> —el mismo PBKDF2 con las mismas iteraciones— y el
/// bloqueo se lleva en los campos de la entidad, que es lo que Identity haría de todas formas.
/// </summary>
public sealed class ServicioDeAutenticacionDeOperador(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeTokens tokens,
    ServicioDeRefreshTokensDeOperador refrescos,
    IServicioDeBitacora bitacora,
    IControlDeIntentos intentos,
    IOptions<OpcionesDeJwt> opciones)
{
    private readonly OpcionesDeJwt _opciones = opciones.Value;

    private static readonly TimeSpan PisoDeRespuesta = TimeSpan.FromMilliseconds(250);

    // Hash de una contraseña que no es de nadie, para gastar lo mismo cuando el correo no
    // existe que cuando sí. Con el mismo hasher del camino real, o el costo delataría cuál es.
    private static string? _hashSenuelo;

    private static OperadorPlataforma OperadorSenuelo => new()
    {
        Nombre = "señuelo",
        Correo = "senuelo",
        CorreoNormalizado = "SENUELO",
        HashContrasena = string.Empty
    };

    private static ErrorNegocio CredencialesInvalidas()
        => ErrorNegocio.Validacion("credenciales-invalidas", "El correo o la contraseña no son correctos.");

    public async Task<Resultado<SesionDeOperadorIniciada>> IniciarSesionAsync(
        PeticionInicioSesionOperador peticion, string? ip, string? agente, CancellationToken ct)
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

    private async Task<Resultado<SesionDeOperadorIniciada>> Intentar(
        PeticionInicioSesionOperador peticion, string? ip, string? agente, CancellationToken ct)
    {
        // Clave propia para el control por IP: los intentos contra el panel no deben mezclarse
        // con los del acceso de inquilinos, ni al contar ni al bloquear.
        var claveIp = $"operador:{ip ?? "desconocida"}";

        if (intentos.BloqueoRestante(claveIp) is { } esperaIp)
            return ErrorNegocio.LimiteExcedido(
                "demasiados-intentos",
                $"Demasiados intentos fallidos desde esta red. Vuelve a intentar en {Redondear(esperaIp)}.");

        var normalizado = peticion.Correo.Trim().ToUpperInvariant();

        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.CorreoNormalizado == normalizado, ct);

        var contrasenaCorrecta = operador is null
            ? VerificarSenuelo(peticion.Contrasena)
            : hasher.VerifyHashedPassword(operador, operador.HashContrasena, peticion.Contrasena)
                is not PasswordVerificationResult.Failed;

        if (operador is null || !contrasenaCorrecta)
        {
            await RegistrarFallo(operador, claveIp, peticion.Correo, ct);
            return CredencialesInvalidas();
        }

        // El bloqueo solo se le revela a quien acertó la contraseña; decirlo antes convertiría
        // el mensaje en un detector de correos dados de alta.
        if (operador.BloqueadoHastaUtc is { } hasta && hasta > DateTime.UtcNow)
            return ErrorNegocio.LimiteExcedido(
                "cuenta-bloqueada",
                $"La cuenta está bloqueada por intentos fallidos. Vuelve a intentar en {Redondear(hasta - DateTime.UtcNow)}.");

        if (!operador.Activo)
            return ErrorNegocio.Regla("operador-desactivado", "Tu acceso al panel está desactivado.");

        operador.AccesosFallidos = 0;
        operador.BloqueosConsecutivos = 0;
        operador.BloqueadoHastaUtc = null;
        operador.UltimoAccesoUtc = DateTime.UtcNow;
        intentos.Limpiar(claveIp);

        var duracion = peticion.MantenerSesion
            ? TimeSpan.FromDays(_opciones.DiasDeSesionLarga)
            : TimeSpan.FromHours(_opciones.HorasDeSesionCorta);

        var refresh = refrescos.CrearFamilia(operador.Id, duracion, ip, agente);

        bitacora.Registrar(
            EntidadesDeBitacora.SesionOperador,
            operador.Id.ToString(),
            AccionesDeBitacora.InicioSesionOperador,
            despues: new { operador.Correo },
            operadorId: operador.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        return Armar(operador, refresh, duracion);
    }

    /// <summary>Canjea la cookie por un access token nuevo, rotando el refresh.</summary>
    public async Task<Resultado<SesionDeOperadorIniciada>> RefrescarAsync(
        string refreshEnClaro, string? ip, string? agente, CancellationToken ct)
    {
        var rotado = await refrescos.RotarAsync(refreshEnClaro, ip, agente, ct);

        if (rotado.Error is { } error) return error;

        var emitido = rotado.Valor!;

        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == emitido.Registro.OperadorId, ct);

        if (operador is null || !operador.Activo)
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

        // La cookie conserva la vida que le quedaba a la familia: refrescar no alarga la sesión.
        var restante = emitido.Registro.ExpiraUtc - DateTime.UtcNow;

        return Armar(operador, emitido, restante > TimeSpan.Zero ? restante : TimeSpan.Zero);
    }

    /// <summary>Cierra la sesión invalidando la familia completa del token presentado.</summary>
    public async Task CerrarSesionAsync(string? refreshEnClaro, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshEnClaro)) return;

        var hash = ServicioDeRefreshTokens.Hash(refreshEnClaro);

        var token = await baseDeDatos.RefreshTokensOperador
            .SingleOrDefaultAsync(t => t.HashToken == hash, ct);

        if (token is null) return;

        await refrescos.InvalidarFamiliaAsync(token.FamiliaId, "cierre de sesión", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.SesionOperador,
            token.OperadorId.ToString(),
            AccionesDeBitacora.CierreSesionOperador,
            operadorId: token.OperadorId);

        await baseDeDatos.SaveChangesAsync(ct);
    }

    /// <summary>Los datos del operador de la sesión en curso, para repintar la cabecera.</summary>
    public async Task<SesionDeOperadorDto?> ObtenerSesionAsync(Guid operadorId, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

        return operador is null ? null : ADto(operador);
    }

    private SesionDeOperadorIniciada Armar(
        OperadorPlataforma operador, RefreshDeOperadorEmitido refresh, TimeSpan duracion)
    {
        var token = tokens.EmitirDeOperador(operador, refresh.Registro.FamiliaId);

        return new SesionDeOperadorIniciada(
            new RespuestaSesionDeOperador(token.Token, token.ExpiraUtc, ADto(operador)),
            refresh.EnClaro,
            duracion);
    }

    private static SesionDeOperadorDto ADto(OperadorPlataforma operador)
        => new(operador.Id, operador.Nombre, operador.Correo);

    private async Task RegistrarFallo(
        OperadorPlataforma? operador, string claveIp, string correo, CancellationToken ct)
    {
        intentos.RegistrarFallo(claveIp);

        if (operador is not null)
        {
            operador.AccesosFallidos++;

            // A los cinco fallos se cierra la puerta, y cada tanda seguida la cierra más tiempo.
            if (operador.AccesosFallidos >= 5)
            {
                operador.BloqueosConsecutivos++;
                operador.BloqueadoHastaUtc = DateTime.UtcNow.Add(DuracionDeBloqueo(operador.BloqueosConsecutivos));
                operador.AccesosFallidos = 0;
            }
        }

        // El correo tecleado se guarda para poder investigar un ataque, pero puede no existir:
        // por eso el registro va sin identificador cuando no lo hay.
        bitacora.Registrar(
            EntidadesDeBitacora.SesionOperador,
            operador?.Id.ToString(),
            AccionesDeBitacora.InicioSesionOperadorFallido,
            despues: new { Correo = correo, OperadorExiste = operador is not null },
            operadorId: operador?.Id);

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
        _hashSenuelo ??= hasher.HashPassword(OperadorSenuelo, "no-es-la-contrasena-de-nadie");

        hasher.VerifyHashedPassword(OperadorSenuelo, _hashSenuelo, contrasena);

        return false;
    }

    /// <summary>Rellena hasta el piso de respuesta, en todas las salidas sin excepción.</summary>
    private static async Task Nivelar(long marcaInicial)
    {
        var transcurrido = Stopwatch.GetElapsedTime(marcaInicial);

        if (transcurrido >= PisoDeRespuesta) return;

        await Task.Delay(PisoDeRespuesta - transcurrido, CancellationToken.None);
    }

    private static string Redondear(TimeSpan espera)
        => espera.TotalMinutes >= 1
            ? $"{Math.Ceiling(espera.TotalMinutes):0} min"
            : $"{Math.Ceiling(espera.TotalSeconds):0} s";
}
