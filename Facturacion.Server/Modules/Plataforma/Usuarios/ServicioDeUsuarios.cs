using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Usuarios de la empresa activa: alta, permisos, correo y baja lógica.
///
/// <para><b>Alta directa, no invitación</b></para>
/// El administrador teclea la contraseña y se la comunica al usuario por fuera del sistema.
/// Sustituyó a la invitación por correo, que se eliminó por completo.
///
/// <para>
/// Tiene dos consecuencias que se decidieron a sabiendas y conviene no descubrir de golpe
/// dentro de seis meses:
/// </para>
/// <list type="number">
///   <item>
///     El administrador conoce la contraseña, así que la bitácora ya no distingue con
///     certeza al usuario de quien le creó la cuenta. Por eso el alta queda registrada:
///     es lo único que permite reconstruir quién pudo haber actuado como quién.
///   </item>
///   <item>
///     Nadie comprueba que el correo sea de quien dice. Aceptar una invitación era esa
///     prueba. Mientras el correo no sirva para recuperar contraseñas, el riesgo se queda
///     en que un usuario no reciba lo que se le mande.
///   </item>
/// </list>
///
/// <para><b>Los dos caminos del alta</b></para>
/// Cuando el correo ya es de un usuario <b>de la misma cuenta</b> —el caso de "administrador
/// en la llantera, auxiliar en la cementera"— no se crea a nadie ni se toca su contraseña:
/// se le da acceso a la empresa nueva y ya.
///
/// <para><b>El límite de usuarios</b></para>
/// Tres accesos activos por empresa: el que la dio de alta más dos auxiliares. No hay ningún
/// campo de "rol": el límite cuenta filas de <see cref="UsuarioEmpresa"/>, no una etiqueta.
/// </summary>
public sealed class ServicioDeUsuarios(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IServicioDeBitacora bitacora,
    IContextoEmpresaInterno contexto,
    ServicioDeRefreshTokens refrescos,
    IOptions<OpcionesDeSoporte> opcionesSoporte)
{
    public const int LimitePorEmpresa = 3;

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
                ue.Usuario.CreadoPorAdministrador,
                ue.Permisos.Select(p => p.PermisoClave).ToList()))
            .ToListAsync(ct);

        return new UsuariosDeLaEmpresaDto(miembros, LimitePorEmpresa);
    }

    public async Task<Resultado<RespuestaCrearUsuario>> CrearAsync(PeticionCrearUsuario peticion, CancellationToken ct)
    {
        var correo = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var nombre = (peticion.Nombre ?? string.Empty).Trim();

        var error = ValidarDatosBasicos(correo, nombre, peticion.Permisos);
        if (error is not null) return error;

        var empresaId = contexto.EmpresaId;

        if (await EstaEnElLimiteAsync(empresaId, ct)) return ErrorDeLimite();

        var existente = await usuarios.FindByEmailAsync(correo);

        if (existente is not null)
            return await DarAccesoDirectoAsync(existente, empresaId, peticion.Permisos, ct);

        // La cuenta sale de la empresa activa y no del claim: es la misma, pero así el
        // usuario nuevo queda colgado justo de la cuenta a la que se le está dando acceso,
        // sin depender de que el token traiga el claim bien puesto.
        var cuentaId = await baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => e.CuentaId)
            .FirstOrDefaultAsync(ct);

        if (cuentaId == Guid.Empty)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "La empresa activa ya no existe.");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            CuentaId = cuentaId,
            UserName = correo,
            Email = correo,
            // Se marca confirmado sin que nadie lo haya confirmado: no hay ida y vuelta por
            // correo que lo pruebe. Es la contrapartida de haber quitado la invitación.
            EmailConfirmed = true,
            FechaAltaUtc = DateTime.UtcNow,
            CreadoPorAdministrador = true
        };

        var alta = await usuarios.CreateAsync(usuario, peticion.Contrasena ?? string.Empty);

        if (!alta.Succeeded)
            return ErrorNegocio.Validacion(
                "contrasena-invalida",
                "La contraseña no cumple los requisitos.",
                new Dictionary<string, string[]>
                {
                    ["contrasena"] = [.. alta.Errors.Select(e => e.Description)]
                });

        OtorgarAcceso(usuario.Id, empresaId, peticion.Permisos, contexto.UsuarioActual);

        // La contraseña NO se registra, ni cifrada ni truncada ni en forma de pista: la
        // bitácora se lee en una auditoría y no es sitio para material de acceso.
        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuario.Id.ToString(), AccionesDeBitacora.UsuarioCreado,
            despues: new { correo, nombre, EmpresaId = empresaId, Permisos = peticion.Permisos },
            usuarioId: usuario.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        return new RespuestaCrearUsuario(
            AccesoDirecto: false,
            Mensaje: $"{nombre} ya puede entrar con su correo y la contraseña que acabas de fijar.");
    }

    /// <summary>
    /// Cambia el correo de otro usuario. Es también su nombre de inicio de sesión, así que
    /// cambiarlo le cambia la forma de entrar: se le avisa por fuera, como la contraseña.
    /// </summary>
    public async Task<Resultado> CambiarCorreoAsync(Guid usuarioId, string correoNuevo, CancellationToken ct)
    {
        var correo = (correoNuevo ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(correo) || !EsCorreoValido(correo))
            return ErrorNegocio.Validacion("correo-invalido", "Escribe un correo válido.");

        var pertenece = await baseDeDatos.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == contexto.EmpresaId, ct);

        if (!pertenece)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a esta empresa.");

        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no existe.");

        if (string.Equals(usuario.Email, correo, StringComparison.OrdinalIgnoreCase))
            return Resultado.Exito();

        var ocupado = await usuarios.FindByEmailAsync(correo);
        if (ocupado is not null)
            return ErrorNegocio.Conflicto("correo-en-uso", "Ese correo ya está en uso.");

        var anterior = usuario.Email;

        // Los dos a la vez: UserName es lo que Identity compara al iniciar sesión, y dejarlo
        // con el correo viejo haría que el usuario entrara con uno y viera el otro.
        var cambioCorreo = await usuarios.SetEmailAsync(usuario, correo);
        if (!cambioCorreo.Succeeded)
            return ErrorNegocio.Validacion("correo-invalido", string.Join(" ", cambioCorreo.Errors.Select(e => e.Description)));

        var cambioNombre = await usuarios.SetUserNameAsync(usuario, correo);
        if (!cambioNombre.Succeeded)
            return ErrorNegocio.Validacion("correo-invalido", string.Join(" ", cambioNombre.Errors.Select(e => e.Description)));

        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.CorreoCambiado,
            antes: new { Correo = anterior }, despues: new { Correo = correo }, usuarioId: usuarioId);

        await baseDeDatos.SaveChangesAsync(ct);

        return Resultado.Exito();
    }

    /// <summary>
    /// Le pone contraseña nueva a un usuario dado de alta por un administrador. Es la otra
    /// mitad de la decisión de que ese usuario no pueda cambiarla él: sin esto, una
    /// contraseña olvidada dejaría el acceso perdido para siempre.
    ///
    /// <para><b>Solo a quien dio de alta un administrador</b></para>
    /// A un usuario que se registró él mismo <b>no</b> se le puede tocar la contraseña desde
    /// aquí. Si se pudiera, cualquier administrador podría apoderarse de la cuenta de quien
    /// lo nombró: le cambia la contraseña, entra como él y lo deja fuera. El dueño de la
    /// cuenta administra la suya y nadie más.
    ///
    /// <para><b>Se cierran todas sus sesiones</b></para>
    /// Cambiar la contraseña sin invalidar lo que ya está abierto no sirve para el caso que
    /// más importa —alguien que se fue de la empresa—: seguiría dentro con el token que
    /// tenía hasta que venciera.
    /// </summary>
    public async Task<Resultado> CambiarContrasenaAsync(Guid usuarioId, string contrasenaNueva, CancellationToken ct)
    {
        if (usuarioId == contexto.UsuarioActual)
            return ErrorNegocio.Regla(
                "no-a-uno-mismo",
                "Tu propia contraseña se cambia desde tu perfil, no desde aquí.");

        var pertenece = await baseDeDatos.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == contexto.EmpresaId, ct);

        if (!pertenece)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a esta empresa.");

        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no existe.");

        if (!usuario.CreadoPorAdministrador)
            return ErrorNegocio.Regla(
                "contrasena-no-administrable",
                $"{usuario.Nombre} creó su propia cuenta y administra su contraseña. " +
                "Nadie más puede cambiársela.");

        // Con el token de restablecimiento y no con RemovePassword + AddPassword: el token
        // aplica las reglas de longitud y composición de Identity en un solo paso, y no deja
        // al usuario un instante sin contraseña si algo falla a la mitad.
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, token, contrasenaNueva ?? string.Empty);

        if (!resultado.Succeeded)
            return ErrorNegocio.Validacion(
                "contrasena-invalida",
                "La contraseña no cumple los requisitos.",
                new Dictionary<string, string[]>
                {
                    ["contrasena"] = [.. resultado.Errors.Select(e => e.Description)]
                });

        await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(
            usuarioId, "contraseña cambiada por un administrador", ct);

        // Qué se cambió, nunca a qué: la contraseña no entra a la bitácora.
        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.ContrasenaCambiada,
            despues: new { PorAdministrador = true, SesionesCerradas = true }, usuarioId: usuarioId);

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

        // Reactivar suma un acceso, así que cuenta contra el límite igual que dar de alta.
        // Sin esto había un camino para pasarse: con el cupo lleno, desactivar a uno,
        // aprovechar el hueco para crear a otro, y reactivar al primero. Tres más uno.
        if (activo && await EstaEnElLimiteAsync(empresaId, ct))
            return ErrorDeLimite();

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

    // ── Interno ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cuenta solo accesos activos. Antes sumaba las invitaciones pendientes para que dos
    /// invitaciones seguidas no rebasaran el límite antes de aceptarse; sin invitaciones no
    /// hay estado intermedio que contar: el alta es inmediata.
    /// </summary>
    private async Task<bool> EstaEnElLimiteAsync(Guid empresaId, CancellationToken ct)
        => await baseDeDatos.UsuariosEmpresas.CountAsync(ue => ue.EmpresaId == empresaId && ue.Activo, ct)
           >= LimitePorEmpresa;

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

    private async Task<Resultado<RespuestaCrearUsuario>> DarAccesoDirectoAsync(
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

        return new RespuestaCrearUsuario(
            AccesoDirecto: true,
            Mensaje: $"{usuarioExistente.Nombre} ya tenía cuenta aquí y ahora tiene acceso a esta empresa. " +
                     "Su contraseña no cambió: entra con la que ya usaba.");
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
            miembro.Activo && miembro.Usuario.Activo, miembro.UsuarioId == contexto.UsuarioActual,
            miembro.Usuario.CreadoPorAdministrador, permisos);
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
            : ErrorNegocio.Validacion("datos-invalidos", "Revisa los datos del usuario.", errores);
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
