using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Usuarios de la empresa activa: invitación, permisos y baja lógica.
///
/// <para><b>Los dos caminos de "invitar"</b></para>
/// Cuando el correo ya es de un usuario <b>de la misma cuenta</b> —el caso de "administrador
/// en la llantera, auxiliar en la cementera" del prompt de la fase 8— no tiene sentido
/// mandarle una invitación para que fije una contraseña que ya tiene: se le da acceso a la
/// empresa nueva de una vez. Solo un correo que no existe todavía pasa por el token y el
/// correo electrónico.
///
/// <para><b>El límite de usuarios</b></para>
/// Tres accesos activos por empresa —el que la dio de alta más dos auxiliares—, contando
/// también las invitaciones pendientes: si no se contaran, dos invitaciones mandadas seguidas
/// podrían pasar de largo el límite antes de que ninguna se acepte. No hay ningún campo de
/// "rol": el límite cuenta filas de <see cref="UsuarioEmpresa"/>, no una etiqueta.
/// </summary>
public sealed class ServicioDeInvitaciones(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IServicioDeCorreo correo,
    IServicioDeBitacora bitacora,
    IContextoEmpresaInterno contexto,
    IHttpContextAccessor accesor,
    ServicioDeRefreshTokens refrescos,
    IOptions<OpcionesDeSoporte> opcionesSoporte)
{
    public const int LimitePorEmpresa = 3;

    private static readonly TimeSpan Vigencia = TimeSpan.FromHours(72);

    public async Task<UsuariosDeLaEmpresaDto> ListarAsync(CancellationToken ct)
    {
        var empresaId = contexto.EmpresaId;

        var miembros = await baseDeDatos.UsuariosEmpresas
            .AsNoTracking()
            .Where(ue => ue.EmpresaId == empresaId)
            .OrderBy(ue => ue.Usuario.Nombre)
            .Select(ue => new UsuarioDeEmpresaDto(
                ue.UsuarioId,
                ue.Usuario.Nombre,
                ue.Usuario.Email!,
                ue.Activo && ue.Usuario.Activo,
                ue.UsuarioId == contexto.UsuarioActual,
                ue.Permisos.Select(p => p.PermisoClave).ToList()))
            .ToListAsync(ct);

        var invitaciones = await baseDeDatos.Invitaciones
            .AsNoTracking()
            .Where(i => i.EmpresaId == empresaId && i.AceptadaUtc == null && i.RevocadaUtc == null)
            .OrderBy(i => i.CreadaUtc)
            .ToListAsync(ct);

        return new UsuariosDeLaEmpresaDto(
            miembros,
            invitaciones.Select(ADto).ToList(),
            LimitePorEmpresa);
    }

    public async Task<Resultado<RespuestaInvitar>> InvitarAsync(PeticionInvitarUsuario peticion, CancellationToken ct)
    {
        var correoNormalizado = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var nombre = (peticion.Nombre ?? string.Empty).Trim();

        var error = ValidarDatosBasicos(correoNormalizado, nombre, peticion.Permisos);
        if (error is not null) return error;

        var empresaId = contexto.EmpresaId;

        var yaEnLimite = await EstaEnElLimiteAsync(empresaId, ct);
        if (yaEnLimite) return ErrorDeLimite();

        var usuarioExistente = await usuarios.FindByEmailAsync(correoNormalizado);

        if (usuarioExistente is not null)
            return await DarAccesoDirectoAsync(usuarioExistente, empresaId, peticion.Permisos, ct);

        return await CrearOReenviarInvitacionAsync(empresaId, correoNormalizado, nombre, peticion.Permisos, ct);
    }

    public async Task<Resultado> ReenviarAsync(Guid invitacionId, CancellationToken ct)
    {
        var invitacion = await baseDeDatos.Invitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId, ct);

        if (invitacion is null)
            return ErrorNegocio.NoEncontrado("invitacion-no-encontrada", "No se encontró esa invitación.");

        if (invitacion.AceptadaUtc is not null)
            return ErrorNegocio.Regla("invitacion-aceptada", "Esa invitación ya se aceptó.");

        if (invitacion.RevocadaUtc is not null)
            return ErrorNegocio.Regla("invitacion-revocada", "Esa invitación está revocada.");

        var (enClaro, hash) = NuevoToken();
        invitacion.HashToken = hash;
        invitacion.ExpiraUtc = DateTime.UtcNow.Add(Vigencia);

        bitacora.Registrar(
            EntidadesDeBitacora.Invitacion, invitacion.Id.ToString(), AccionesDeBitacora.InvitacionReenviada,
            despues: new { invitacion.Correo });

        await baseDeDatos.SaveChangesAsync(ct);

        await EnviarCorreoDeInvitacionAsync(invitacion, enClaro, ct);

        return Resultado.Exito();
    }

    public async Task<Resultado> RevocarAsync(Guid invitacionId, CancellationToken ct)
    {
        var invitacion = await baseDeDatos.Invitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId, ct);

        if (invitacion is null)
            return ErrorNegocio.NoEncontrado("invitacion-no-encontrada", "No se encontró esa invitación.");

        if (invitacion.AceptadaUtc is not null)
            return ErrorNegocio.Regla("invitacion-aceptada", "Esa invitación ya se aceptó; no se puede revocar.");

        if (invitacion.RevocadaUtc is null)
        {
            invitacion.RevocadaUtc = DateTime.UtcNow;

            bitacora.Registrar(
                EntidadesDeBitacora.Invitacion, invitacion.Id.ToString(), AccionesDeBitacora.InvitacionRevocada,
                despues: new { invitacion.Correo });

            await baseDeDatos.SaveChangesAsync(ct);
        }

        return Resultado.Exito();
    }

    /// <summary>Para la pantalla pública de aceptar. Sin empresa activa: por eso ignora el filtro.</summary>
    public async Task<InvitacionPublicaDto?> ObtenerPublicaAsync(string token, CancellationToken ct)
    {
        var invitacion = await BuscarPorTokenAsync(token, ct);
        if (invitacion is null) return null;

        var nombreEmpresa = await baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.Id == invitacion.EmpresaId)
            .Select(e => e.NombreFiscal)
            .FirstOrDefaultAsync(ct);

        return new InvitacionPublicaDto(invitacion.Correo, invitacion.Nombre, nombreEmpresa ?? "—");
    }

    public async Task<Resultado> AceptarAsync(PeticionAceptarInvitacion peticion, CancellationToken ct)
    {
        var invitacion = await BuscarPorTokenAsync(peticion.Token, ct);

        if (invitacion is null)
            return ErrorNegocio.Validacion("invitacion-invalida", "Esta invitación ya no es válida. Pide que te inviten otra vez.");

        // Recontado por si, entre que se mandó la invitación y se acepta, la empresa llegó
        // al límite por otra vía: no hay bloqueo de renglón aquí —a diferencia de folios o
        // timbres— porque aceptar una invitación no es una operación de alta frecuencia y
        // una carrera perdida cuesta, cuando mucho, un cuarto usuario que se corrige a mano.
        if (await EstaEnElLimiteAsync(invitacion.EmpresaId, ct))
            return ErrorDeLimite();

        var empresa = await baseDeDatos.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == invitacion.EmpresaId, ct);

        if (empresa is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "La empresa de esta invitación ya no existe.");

        // Alguien pudo haber aceptado otra invitación con este mismo correo mientras esta
        // seguía pendiente. En ese caso ya existe cuenta: se le da acceso directo, igual que
        // si el administrador hubiera invitado a un correo que ya conocía.
        var usuario = await usuarios.FindByEmailAsync(invitacion.Correo);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = invitacion.Nombre,
                CuentaId = empresa.CuentaId,
                UserName = invitacion.Correo,
                Email = invitacion.Correo,
                EmailConfirmed = true,
                FechaAltaUtc = DateTime.UtcNow
            };

            var alta = await usuarios.CreateAsync(usuario, peticion.Contrasena);

            if (!alta.Succeeded)
                return ErrorNegocio.Validacion(
                    "contrasena-invalida",
                    "La contraseña no cumple los requisitos.",
                    new Dictionary<string, string[]>
                    {
                        ["contrasena"] = [.. alta.Errors.Select(e => e.Description)]
                    });
        }

        OtorgarAcceso(usuario.Id, invitacion.EmpresaId, PermisosDe(invitacion.PermisosClaves), invitacion.CreadaPorUsuarioId);

        invitacion.AceptadaUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.Invitacion, invitacion.Id.ToString(), AccionesDeBitacora.InvitacionAceptada,
            despues: new { invitacion.Correo, UsuarioId = usuario.Id },
            usuarioId: usuario.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        return Resultado.Exito();
    }

    public async Task<Resultado> ActualizarPermisosAsync(Guid usuarioId, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        var invalido = permisos.FirstOrDefault(p => !Permisos.EsValido(p));
        if (invalido is not null)
            return ErrorNegocio.Validacion("permiso-invalido", $"«{invalido}» no es un permiso válido.");

        var empresaId = contexto.EmpresaId;

        var pertenece = await baseDeDatos.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == empresaId, ct);

        if (!pertenece)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a esta empresa.");

        var actuales = await baseDeDatos.UsuariosEmpresasPermisos
            .Where(p => p.UsuarioId == usuarioId && p.EmpresaId == empresaId)
            .ToListAsync(ct);

        var clavesActuales = actuales.Select(p => p.PermisoClave).ToList();
        var clavesNuevas = permisos.Distinct().ToList();

        var aQuitar = actuales.Where(p => !clavesNuevas.Contains(p.PermisoClave)).ToList();
        var aAgregar = clavesNuevas.Except(clavesActuales).Select(clave => new UsuarioEmpresaPermiso
        {
            UsuarioId = usuarioId,
            EmpresaId = empresaId,
            PermisoClave = clave,
            OtorgadoUtc = DateTime.UtcNow,
            OtorgadoPorUsuarioId = contexto.UsuarioActual
        }).ToList();

        if (aQuitar.Count == 0 && aAgregar.Count == 0) return Resultado.Exito();

        baseDeDatos.UsuariosEmpresasPermisos.RemoveRange(aQuitar);
        baseDeDatos.UsuariosEmpresasPermisos.AddRange(aAgregar);

        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.PermisosActualizados,
            antes: new { Permisos = clavesActuales }, despues: new { Permisos = clavesNuevas },
            usuarioId: usuarioId);

        await baseDeDatos.SaveChangesAsync(ct);

        return Resultado.Exito();
    }

    public async Task<Resultado<UsuarioDeEmpresaDto>> CambiarActivoAsync(Guid usuarioId, bool activo, CancellationToken ct)
    {
        var empresaId = contexto.EmpresaId;

        if (!activo && usuarioId == contexto.UsuarioActual)
            return ErrorNegocio.Regla("no-autodesactivar", "No puedes desactivarte a ti mismo. Pide a otro administrador que lo haga.");

        var miembro = await baseDeDatos.UsuariosEmpresas
            .Include(ue => ue.Usuario)
            .FirstOrDefaultAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == empresaId, ct);

        if (miembro is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a esta empresa.");

        if (miembro.Activo == activo)
            return await ADtoAsync(miembro, ct);

        miembro.Activo = activo;

        if (activo)
        {
            // Reactivar en cualquier empresa levanta también la cuenta completa: quedó
            // apagada solo porque se había quedado sin ninguna, y sigue siendo la misma persona.
            miembro.Usuario.Activo = true;

            bitacora.Registrar(
                EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.UsuarioReactivado,
                despues: new { EmpresaId = empresaId }, usuarioId: usuarioId);
        }
        else
        {
            var tieneOtraEmpresaActiva = await baseDeDatos.UsuariosEmpresas
                .AsNoTracking()
                .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId != empresaId && ue.Activo, ct);

            bitacora.Registrar(
                EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.UsuarioDesactivado,
                despues: new { EmpresaId = empresaId, SinNingunaEmpresa = !tieneOtraEmpresaActiva }, usuarioId: usuarioId);

            if (!tieneOtraEmpresaActiva)
            {
                miembro.Usuario.Activo = false;

                await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(
                    usuarioId, "usuario desactivado: sin acceso a ninguna empresa", ct);
            }
        }

        await baseDeDatos.SaveChangesAsync(ct);

        return await ADtoAsync(miembro, ct);
    }

    private async Task<bool> EstaEnElLimiteAsync(Guid empresaId, CancellationToken ct)
    {
        var activos = await baseDeDatos.UsuariosEmpresas.CountAsync(ue => ue.EmpresaId == empresaId && ue.Activo, ct);

        var pendientes = await baseDeDatos.Invitaciones.CountAsync(
            i => i.EmpresaId == empresaId && i.AceptadaUtc == null && i.RevocadaUtc == null && i.ExpiraUtc > DateTime.UtcNow,
            ct);

        return activos + pendientes >= LimitePorEmpresa;
    }

    private ErrorNegocio ErrorDeLimite()
    {
        var soporte = opcionesSoporte.Value;

        var contacto = (soporte.Correo, soporte.Telefono) switch
        {
            ({ } correo, { } telefono) => $" Comunícate a {correo} o al {telefono}.",
            ({ } correo, null) => $" Comunícate a {correo}.",
            (null, { } telefono) => $" Comunícate al {telefono}.",
            _ => string.Empty
        };

        return ErrorNegocio.LimiteExcedido(
            "limite-de-usuarios",
            $"Esta empresa ya tiene el máximo de {LimitePorEmpresa} usuarios (tú más dos auxiliares). " +
            $"Para agregar más, contacta a soporte.{contacto}");
    }

    private async Task<Resultado<RespuestaInvitar>> DarAccesoDirectoAsync(
        Usuario usuarioExistente, Guid empresaId, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        if (usuarioExistente.CuentaId != contexto.CuentaActual)
            return ErrorNegocio.Conflicto("correo-en-uso", "Ese correo ya está en uso por otra cuenta.");

        var existente = await baseDeDatos.UsuariosEmpresas
            .FirstOrDefaultAsync(ue => ue.UsuarioId == usuarioExistente.Id && ue.EmpresaId == empresaId, ct);

        if (existente is { Activo: true })
            return ErrorNegocio.Conflicto("ya-tiene-acceso", "Ese usuario ya tiene acceso a esta empresa.");

        if (existente is not null)
        {
            // Ya había pertenecido y se le había quitado el acceso: se reactiva en vez de
            // crear un segundo renglón, y se le dejan los permisos que se acaban de elegir.
            existente.Activo = true;
            usuarioExistente.Activo = true;

            await ReemplazarPermisosAsync(usuarioExistente.Id, empresaId, permisos, ct);
        }
        else
        {
            OtorgarAcceso(usuarioExistente.Id, empresaId, permisos, contexto.UsuarioActual);
        }

        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuarioExistente.Id.ToString(), AccionesDeBitacora.AccesoOtorgadoDirecto,
            despues: new { EmpresaId = empresaId, Permisos = permisos }, usuarioId: usuarioExistente.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        return new RespuestaInvitar(
            AccesoDirecto: true,
            Mensaje: $"{usuarioExistente.Nombre} ya tenía cuenta en tu cuenta y ahora tiene acceso a esta empresa.");
    }

    private async Task<Resultado<RespuestaInvitar>> CrearOReenviarInvitacionAsync(
        Guid empresaId, string correo, string nombre, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        // Reutiliza el renglón si ya había una invitación pendiente (o vencida sin resolver)
        // para este correo en esta empresa: el índice único la habría rechazado si se
        // insertara una fila nueva mientras la vieja sigue sin aceptarse ni revocarse.
        var invitacion = await baseDeDatos.Invitaciones.FirstOrDefaultAsync(
            i => i.EmpresaId == empresaId && i.Correo == correo && i.AceptadaUtc == null && i.RevocadaUtc == null, ct);

        var (enClaro, hash) = NuevoToken();
        var esNueva = invitacion is null;

        if (invitacion is null)
        {
            invitacion = new Invitacion
            {
                Id = Guid.NewGuid(),
                EmpresaId = empresaId,
                Correo = correo,
                Nombre = nombre,
                HashToken = hash,
                PermisosClaves = string.Join(',', permisos.Distinct()),
                CreadaUtc = DateTime.UtcNow,
                ExpiraUtc = DateTime.UtcNow.Add(Vigencia),
                CreadaPorUsuarioId = contexto.UsuarioId
            };

            baseDeDatos.Invitaciones.Add(invitacion);
        }
        else
        {
            invitacion.Nombre = nombre;
            invitacion.HashToken = hash;
            invitacion.PermisosClaves = string.Join(',', permisos.Distinct());
            invitacion.ExpiraUtc = DateTime.UtcNow.Add(Vigencia);
        }

        bitacora.Registrar(
            EntidadesDeBitacora.Invitacion, invitacion.Id.ToString(),
            esNueva ? AccionesDeBitacora.UsuarioInvitado : AccionesDeBitacora.InvitacionReenviada,
            despues: new { Correo = correo, Permisos = permisos });

        await baseDeDatos.SaveChangesAsync(ct);

        await EnviarCorreoDeInvitacionAsync(invitacion, enClaro, ct);

        return new RespuestaInvitar(AccesoDirecto: false, Mensaje: $"Se envió la invitación a {correo}.");
    }

    private async Task EnviarCorreoDeInvitacionAsync(Invitacion invitacion, string tokenEnClaro, CancellationToken ct)
    {
        var solicitud = accesor.HttpContext!.Request;
        var origen = $"{solicitud.Scheme}://{solicitud.Host}";
        var enlace = $"{origen}/aceptar-invitacion?token={Uri.EscapeDataString(tokenEnClaro)}";

        var cuerpo =
            $"<p>Te invitaron a facturar en el sistema.</p>" +
            $"<p><a href=\"{enlace}\">Acepta la invitación</a> para fijar tu contraseña. " +
            $"El enlace es válido por 72 horas.</p>" +
            $"<p>Si no esperabas este correo, ignóralo.</p>";

        try
        {
            await correo.EnviarAsync(invitacion.Correo, "Te invitaron al sistema de facturación", cuerpo, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // La invitación ya quedó guardada; el administrador puede reenviarla desde la
            // pantalla si el correo no llegó. No se convierte en error de negocio: quien
            // invita no puede arreglar un SMTP caído desde ahí.
        }
    }

    private async Task<Invitacion?> BuscarPorTokenAsync(string token, CancellationToken ct)
    {
        var hash = ServicioDeRefreshTokens.Hash(token);

        var invitacion = await baseDeDatos.Invitaciones
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.HashToken == hash, ct);

        if (invitacion is null) return null;
        if (invitacion.AceptadaUtc is not null) return null;
        if (invitacion.RevocadaUtc is not null) return null;
        if (invitacion.ExpiraUtc <= DateTime.UtcNow) return null;

        return invitacion;
    }

    private void OtorgarAcceso(Guid usuarioId, Guid empresaId, IReadOnlyList<string> permisos, Guid? otorgadoPor)
    {
        baseDeDatos.UsuariosEmpresas.Add(new UsuarioEmpresa
        {
            UsuarioId = usuarioId,
            EmpresaId = empresaId,
            FechaAltaUtc = DateTime.UtcNow
        });

        var ahora = DateTime.UtcNow;

        baseDeDatos.UsuariosEmpresasPermisos.AddRange(permisos.Distinct().Select(clave => new UsuarioEmpresaPermiso
        {
            UsuarioId = usuarioId,
            EmpresaId = empresaId,
            PermisoClave = clave,
            OtorgadoUtc = ahora,
            OtorgadoPorUsuarioId = otorgadoPor
        }));
    }

    private async Task ReemplazarPermisosAsync(Guid usuarioId, Guid empresaId, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        var actuales = await baseDeDatos.UsuariosEmpresasPermisos
            .Where(p => p.UsuarioId == usuarioId && p.EmpresaId == empresaId)
            .ToListAsync(ct);

        baseDeDatos.UsuariosEmpresasPermisos.RemoveRange(actuales);

        var ahora = DateTime.UtcNow;

        baseDeDatos.UsuariosEmpresasPermisos.AddRange(permisos.Distinct().Select(clave => new UsuarioEmpresaPermiso
        {
            UsuarioId = usuarioId,
            EmpresaId = empresaId,
            PermisoClave = clave,
            OtorgadoUtc = ahora,
            OtorgadoPorUsuarioId = contexto.UsuarioActual
        }));
    }

    private async Task<UsuarioDeEmpresaDto> ADtoAsync(UsuarioEmpresa miembro, CancellationToken ct)
    {
        var permisos = await baseDeDatos.UsuariosEmpresasPermisos
            .AsNoTracking()
            .Where(p => p.UsuarioId == miembro.UsuarioId && p.EmpresaId == miembro.EmpresaId)
            .Select(p => p.PermisoClave)
            .ToListAsync(ct);

        return new UsuarioDeEmpresaDto(
            miembro.UsuarioId, miembro.Usuario.Nombre, miembro.Usuario.Email!,
            miembro.Activo && miembro.Usuario.Activo, miembro.UsuarioId == contexto.UsuarioActual, permisos);
    }

    private static InvitacionDto ADto(Invitacion i) => new(
        i.Id, i.Correo, i.Nombre, PermisosDe(i.PermisosClaves), i.CreadaUtc, i.ExpiraUtc,
        i.ExpiraUtc <= DateTime.UtcNow ? "expirada" : "pendiente");

    private static List<string> PermisosDe(string permisosClaves)
        => [.. permisosClaves.Split(',', StringSplitOptions.RemoveEmptyEntries)];

    private static (string EnClaro, string Hash) NuevoToken()
    {
        var enClaro = ServicioDeRefreshTokens.GenerarToken();
        return (enClaro, ServicioDeRefreshTokens.Hash(enClaro));
    }

    private static ErrorNegocio? ValidarDatosBasicos(string correo, string nombre, IReadOnlyList<string> permisos)
    {
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(correo) || !EsCorreoValido(correo))
            errores["correo"] = ["Escribe un correo válido."];

        if (string.IsNullOrWhiteSpace(nombre))
            errores["nombre"] = ["El nombre es obligatorio."];

        var invalido = permisos.FirstOrDefault(p => !Permisos.EsValido(p));
        if (invalido is not null)
            errores["permisos"] = [$"«{invalido}» no es un permiso válido."];

        return errores.Count == 0
            ? null
            : ErrorNegocio.Validacion("datos-invalidos", "Revisa los datos de la invitación.", errores);
    }

    private static bool EsCorreoValido(string correo)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(correo);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
