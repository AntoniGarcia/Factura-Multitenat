using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Los usuarios de la cuenta, administrados por su titular. Cada usuario tiene un solo juego de
/// permisos, el mismo en todas las empresas a las que entra (AGENTS.md §11, 27 de octubre de
/// 2026). El alta directa no verifica el correo y el creador conoce la contraseña inicial;
/// queda registrada en bitácora. El titular no se modifica desde esta pantalla.
/// </summary>
public sealed class ServicioDeUsuarios(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IServicioDeBitacora bitacora,
    IContextoEmpresaInterno contexto,
    ServicioDeRefreshTokens refrescos,
    IOptions<OpcionesDeSoporte> opcionesSoporte,
    IControlDeIntentos intentos)
{
    public async Task<Resultado> EliminarAsync(Guid usuarioId, string contrasena, CancellationToken ct)
    {
        var confirmacion = await ConfirmacionDeTitular.ValidarAsync(usuarios, contexto, intentos, contrasena);
        if (confirmacion.EsFallo) return confirmacion.Error!;
        var titular = confirmacion.Valor;

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var usuario = await baseDeDatos.Users
            .SingleOrDefaultAsync(x => x.Id == usuarioId && x.CuentaId == titular.CuentaId, ct);
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a tu cuenta.");
        if (usuarioId == titular.Id || usuario.EsTitular || !usuario.CreadoPorAdministrador)
            return ErrorNegocio.Regla("usuario-protegido", "No puedes eliminar este usuario.");

        // Se revisa toda su actividad, no solo la de la empresa abierta.
        var id = usuarioId.ToString();
        var tieneHistorial = await baseDeDatos.Bitacora.IgnoreQueryFilters().AnyAsync(x =>
                x.UsuarioId == usuarioId && !(x.Entidad == EntidadesDeBitacora.UsuarioEmpresa &&
                    x.EntidadId == id && x.Accion == AccionesDeBitacora.UsuarioCreado), ct)
            || await baseDeDatos.RefreshTokens.AnyAsync(x => x.UsuarioId == usuarioId, ct)
            || await baseDeDatos.Comprobantes.IgnoreQueryFilters().AnyAsync(x => x.CreadoPorUsuarioId == usuarioId, ct)
            || await baseDeDatos.EnviosDeCorreo.IgnoreQueryFilters().AnyAsync(x => x.EnviadoPorUsuarioId == usuarioId, ct)
            || await baseDeDatos.SolicitudesCancelacion.IgnoreQueryFilters().AnyAsync(x => x.SolicitadaPorUsuarioId == usuarioId, ct)
            || await baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AnyAsync(x => x.UsuarioId == usuarioId, ct)
            || await baseDeDatos.MovimientosTimbre.IgnoreQueryFilters().AnyAsync(x => x.UsuarioId == usuarioId, ct)
            || await baseDeDatos.CertificadosCsd.IgnoreQueryFilters().AnyAsync(x => x.CargadoPorUsuarioId == usuarioId, ct)
            || await baseDeDatos.ClavesIdempotencia.IgnoreQueryFilters().AnyAsync(x => x.UsuarioId == usuarioId, ct)
            || await baseDeDatos.UsuariosEmpresasPermisos.IgnoreQueryFilters().AnyAsync(x => x.OtorgadoPorUsuarioId == usuarioId, ct);
        if (tieneHistorial)
            return ErrorNegocio.Regla("usuario-con-historial", "Tiene actividad registrada. Desactívalo para conservar su historial.");

        var accesos = await baseDeDatos.UsuariosEmpresas
            .Include(x => x.Permisos)
            .Where(x => x.UsuarioId == usuarioId)
            .ToListAsync(ct);

        bitacora.Registrar(EntidadesDeBitacora.UsuarioEmpresa, id, AccionesDeBitacora.UsuarioEliminado,
            antes: new { usuario.Nombre, usuario.Email }, usuarioId: titular.Id);
        baseDeDatos.UsuariosEmpresasPermisos.RemoveRange(accesos.SelectMany(x => x.Permisos));
        baseDeDatos.UsuariosEmpresas.RemoveRange(accesos);
        baseDeDatos.Users.Remove(usuario);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return Resultado.Exito();
    }

    public async Task<UsuariosDeLaCuentaDto> ListarAsync(CancellationToken ct)
    {
        var cuentaId = await CuentaDeLaEmpresaAsync(contexto.EmpresaId, ct);
        var empresas = await EmpresasParaAccesoAsync(cuentaId, ct);

        var filas = await baseDeDatos.Users
            .AsNoTracking()
            .Where(u => u.CuentaId == cuentaId)
            .OrderBy(u => u.Nombre)
            .Select(u => new FilaDeUsuario(
                u.Id, u.Nombre, u.Email!, u.Activo, u.CreadoPorAdministrador, u.EsTitular,
                baseDeDatos.UsuariosEmpresas
                    .Where(ue => ue.UsuarioId == u.Id && ue.Activo)
                    .Select(ue => ue.EmpresaId)
                    .ToList(),
                baseDeDatos.UsuariosEmpresasPermisos
                    .Where(p => p.UsuarioId == u.Id)
                    .Select(p => p.PermisoClave)
                    .Distinct()
                    .ToList()))
            .ToListAsync(ct);

        var limite = await baseDeDatos.Cuentas
            .AsNoTracking()
            .Where(c => c.Id == cuentaId)
            .Select(c => c.LimiteDeUsuarios)
            .FirstOrDefaultAsync(ct);
        var enUso = await LimitesDeCuenta.UsuariosQueCuentan(baseDeDatos, cuentaId).CountAsync(ct);

        return new UsuariosDeLaCuentaDto([.. filas.Select(f => ADto(f, empresas))], limite, enUso, empresas);
    }

    public async Task<Resultado<RespuestaCrearUsuario>> CrearAsync(PeticionCrearUsuario peticion, CancellationToken ct)
    {
        var correo = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var nombre = (peticion.Nombre ?? string.Empty).Trim();

        var error = ValidarDatosBasicos(correo, nombre, peticion.Permisos);
        if (error is not null) return error;

        // La transacción sostiene el bloqueo de la cuenta hasta el final del alta.
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        var cuenta = await BloquearCuentaAsync(ct);
        if (cuenta is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "La empresa activa ya no existe.");

        var elegidas = await ValidarEmpresasAsync(cuenta.Id, peticion.Empresas, ct);
        if (elegidas.EsFallo) return elegidas.Error!;

        if (await usuarios.FindByEmailAsync(correo) is { } existente)
            return existente.CuentaId == cuenta.Id
                ? ErrorNegocio.Conflicto("usuario-existente",
                    "Ese correo ya es de un usuario de tu cuenta. Búscalo en la lista para cambiar sus empresas o sus permisos.")
                : ErrorNegocio.Conflicto("correo-en-uso", "Ese correo ya está en uso por otra cuenta.");

        if (await EstaEnElLimiteAsync(cuenta, ct)) return ErrorDeLimite(cuenta);

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            CuentaId = cuenta.Id,
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

        var ahora = DateTime.UtcNow;
        foreach (var empresaId in elegidas.Valor)
            baseDeDatos.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = usuario.Id, EmpresaId = empresaId, FechaAltaUtc = ahora });

        FijarPermisos(usuario.Id, elegidas.Valor, [], peticion.Permisos);

        // La contraseña NO se registra, ni cifrada ni truncada ni en forma de pista: la
        // bitácora se lee en una auditoría y no es sitio para material de acceso.
        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa, usuario.Id.ToString(), AccionesDeBitacora.UsuarioCreado,
            despues: new { correo, nombre, Empresas = elegidas.Valor, Permisos = peticion.Permisos },
            usuarioId: usuario.Id);

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return new RespuestaCrearUsuario($"{nombre} ya puede entrar con su correo y la contraseña que acabas de fijar.");
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

        if (await ErrorSiEsTitularAsync(usuarioId, ct) is { } titular) return titular;

        var usuario = await UsuarioDeLaCuentaAsync(usuarioId, ct);
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a tu cuenta.");

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

        if (await ErrorSiEsTitularAsync(usuarioId, ct) is { } titular) return titular;

        var usuario = await UsuarioDeLaCuentaAsync(usuarioId, ct);
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a tu cuenta.");

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

    /// <summary>
    /// Fija las empresas a las que entra el usuario y sus permisos, los mismos en todas. Una
    /// empresa desmarcada conserva el renglón inactivo: volver a marcarla no crea otro.
    /// </summary>
    public async Task<Resultado> ActualizarPermisosAsync(
        Guid usuarioId, PeticionActualizarPermisos peticion, CancellationToken ct)
    {
        if (Permisos.ErrorDeAsignacion(peticion.Permisos) is { } invalido)
            return ErrorNegocio.Validacion("permiso-invalido", invalido);

        if (await ErrorSiEsTitularAsync(usuarioId, ct) is { } titular) return titular;

        // Ganar la primera empresa activa hace que el usuario ocupe lugar: cuenta contra el límite.
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        var cuenta = await BloquearCuentaAsync(ct);
        if (cuenta is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "La empresa activa ya no existe.");

        var usuario = await UsuarioDeLaCuentaAsync(usuarioId, ct);
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a tu cuenta.");

        var elegidas = await ValidarEmpresasAsync(cuenta.Id, peticion.Empresas, ct);
        if (elegidas.EsFallo) return elegidas.Error!;

        if (!await OcupaLugarAsync(cuenta, usuarioId, ct) && usuario.Activo && await EstaEnElLimiteAsync(cuenta, ct))
            return ErrorDeLimite(cuenta);

        var accesos = await baseDeDatos.UsuariosEmpresas
            .Include(ue => ue.Empresa)
            .Where(ue => ue.UsuarioId == usuarioId)
            .ToListAsync(ct);
        var permisosActuales = await baseDeDatos.UsuariosEmpresasPermisos
            .Where(p => p.UsuarioId == usuarioId)
            .ToListAsync(ct);

        var empresasAntes = accesos.Where(ue => ue.Activo).Select(ue => ue.EmpresaId).ToList();
        var clavesAntes = permisosActuales.Select(p => p.PermisoClave).Distinct().ToList();

        // Las empresas que el operador desactivó no se ofrecen en la pantalla; su acceso se
        // deja como estaba en lugar de leer su ausencia como «desmarcada».
        foreach (var acceso in accesos.Where(ue => ue.Empresa.Activa))
            acceso.Activo = elegidas.Valor.Contains(acceso.EmpresaId);

        var ahora = DateTime.UtcNow;
        foreach (var empresaId in elegidas.Valor.Where(id => accesos.All(ue => ue.EmpresaId != id)))
        {
            var nuevo = new UsuarioEmpresa { UsuarioId = usuarioId, EmpresaId = empresaId, FechaAltaUtc = ahora };
            baseDeDatos.UsuariosEmpresas.Add(nuevo);
            accesos.Add(nuevo);
        }

        // También en los accesos inactivos: si se reactivan, entran ya con el juego vigente.
        FijarPermisos(usuarioId, [.. accesos.Select(ue => ue.EmpresaId)], permisosActuales, peticion.Permisos);

        var empresasDespues = accesos.Where(ue => ue.Activo).Select(ue => ue.EmpresaId).ToList();
        var clavesDespues = peticion.Permisos.Distinct().ToList();

        if (!empresasAntes.ToHashSet().SetEquals(empresasDespues) || !clavesAntes.ToHashSet().SetEquals(clavesDespues))
            bitacora.Registrar(
                EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.PermisosActualizados,
                antes: new { Empresas = empresasAntes, Permisos = clavesAntes },
                despues: new { Empresas = empresasDespues, Permisos = clavesDespues },
                usuarioId: usuarioId);

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Resultado.Exito();
    }

    /// <summary>
    /// Desactiva o reactiva al usuario en toda la cuenta. A qué empresas entra se decide en sus
    /// permisos; aquí solo se le abre o se le cierra la puerta.
    /// </summary>
    public async Task<Resultado<UsuarioDeLaCuentaDto>> CambiarActivoAsync(Guid usuarioId, bool activo, CancellationToken ct)
    {
        if (!activo && usuarioId == contexto.UsuarioActual)
            return ErrorNegocio.Regla("no-autodesactivar", "No puedes desactivarte a ti mismo. Pide a otro administrador que lo haga.");

        if (await ErrorSiEsTitularAsync(usuarioId, ct) is { } titular) return titular;

        // Reactivar cuenta contra el límite igual que dar de alta. Sin esto había un camino
        // para pasarse: con el cupo lleno, desactivar a uno, aprovechar el hueco para crear a
        // otro, y reactivar al primero.
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        var cuenta = await BloquearCuentaAsync(ct);
        if (cuenta is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "La empresa activa ya no existe.");

        var usuario = await UsuarioDeLaCuentaAsync(usuarioId, ct);
        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no pertenece a tu cuenta.");

        var conAcceso = await baseDeDatos.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.Activo && ue.Empresa.Activa, ct);

        if (activo)
        {
            if (!conAcceso)
                return ErrorNegocio.Regla("sin-empresas",
                    $"{usuario.Nombre} no tiene acceso a ninguna empresa. Márcale al menos una en sus permisos antes de reactivarlo.");

            if (usuario.Activo) return await ADtoAsync(usuario, cuenta.Id, ct);

            if (await EstaEnElLimiteAsync(cuenta, ct)) return ErrorDeLimite(cuenta);

            usuario.Activo = true;

            bitacora.Registrar(
                EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.UsuarioReactivado,
                usuarioId: usuarioId);
        }
        else
        {
            if (!usuario.Activo) return await ADtoAsync(usuario, cuenta.Id, ct);

            usuario.Activo = false;

            bitacora.Registrar(
                EntidadesDeBitacora.UsuarioEmpresa, usuarioId.ToString(), AccionesDeBitacora.UsuarioDesactivado,
                usuarioId: usuarioId);

            await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(usuarioId, "usuario desactivado", ct);
        }

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return await ADtoAsync(usuario, cuenta.Id, ct);
    }

    // ── Interno ─────────────────────────────────────────────────────────────────────────

    private sealed record FilaDeUsuario(
        Guid Id, string Nombre, string Correo, bool Activo, bool CreadoPorAdministrador, bool EsTitular,
        List<Guid> Empresas, List<string> Permisos);

    // La cuenta sale de la empresa activa y no del claim: es la misma, pero así el usuario
    // queda colgado justo de la cuenta a la que se le está dando acceso, sin depender de que
    // el token traiga el claim bien puesto.
    private Task<Guid> CuentaDeLaEmpresaAsync(Guid empresaId, CancellationToken ct)
        => baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => e.CuentaId)
            .FirstOrDefaultAsync(ct);

    private async Task<Cuenta?> BloquearCuentaAsync(CancellationToken ct)
    {
        var cuentaId = await CuentaDeLaEmpresaAsync(contexto.EmpresaId, ct);
        return cuentaId == Guid.Empty ? null : await LimitesDeCuenta.BloquearAsync(baseDeDatos, cuentaId, ct);
    }

    private async Task<Usuario?> UsuarioDeLaCuentaAsync(Guid usuarioId, CancellationToken ct)
    {
        var cuentaId = await CuentaDeLaEmpresaAsync(contexto.EmpresaId, ct);
        return await baseDeDatos.Users.FirstOrDefaultAsync(u => u.Id == usuarioId && u.CuentaId == cuentaId, ct);
    }

    private Task<List<EmpresaParaAccesoDto>> EmpresasParaAccesoAsync(Guid cuentaId, CancellationToken ct)
        => baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.CuentaId == cuentaId && e.Activa)
            .OrderBy(e => e.NombreFiscal)
            .Select(e => new EmpresaParaAccesoDto(
                e.Id, e.NombreFiscal, e.Rfc, new LicenciasDto(e.LicNotarios, e.LicObras, e.LicComercio, e.LicINE)))
            .ToListAsync(ct);

    /// <summary>
    /// Las empresas llegan del navegador (AGENTS.md §11): cada una tiene que ser una empresa
    /// activa de la cuenta. Una ajena responde igual que una inexistente.
    /// </summary>
    private async Task<Resultado<List<Guid>>> ValidarEmpresasAsync(
        Guid cuentaId, IReadOnlyList<Guid>? empresas, CancellationToken ct)
    {
        var elegidas = (empresas ?? []).Distinct().ToList();

        if (elegidas.Count == 0)
            return ErrorNegocio.Validacion("sin-empresas", "Elige al menos una empresa a la que entrará.");

        var validas = await baseDeDatos.Empresas
            .CountAsync(e => e.CuentaId == cuentaId && e.Activa && elegidas.Contains(e.Id), ct);

        return validas == elegidas.Count
            ? elegidas
            : ErrorNegocio.NoEncontrado("empresa-no-disponible", "Alguna de las empresas elegidas ya no está disponible.");
    }

    /// <summary>
    /// Deja el mismo juego de permisos en cada empresa indicada. Conserva los renglones que no
    /// cambian, con quién y cuándo los otorgó.
    /// </summary>
    private void FijarPermisos(
        Guid usuarioId, IReadOnlyList<Guid> empresas, IReadOnlyList<UsuarioEmpresaPermiso> actuales, IReadOnlyList<string> permisos)
    {
        var claves = permisos.Distinct().ToList();
        var ahora = DateTime.UtcNow;

        baseDeDatos.UsuariosEmpresasPermisos.RemoveRange(actuales.Where(p => !claves.Contains(p.PermisoClave)));

        foreach (var empresaId in empresas)
            baseDeDatos.UsuariosEmpresasPermisos.AddRange(claves
                .Where(clave => !actuales.Any(p => p.EmpresaId == empresaId && p.PermisoClave == clave))
                .Select(clave => new UsuarioEmpresaPermiso
                {
                    UsuarioId = usuarioId,
                    EmpresaId = empresaId,
                    PermisoClave = clave,
                    OtorgadoUtc = ahora,
                    OtorgadoPorUsuarioId = contexto.UsuarioActual
                }));
    }

    private async Task<bool> EstaEnElLimiteAsync(Cuenta cuenta, CancellationToken ct)
        => await LimitesDeCuenta.UsuariosQueCuentan(baseDeDatos, cuenta.Id).CountAsync(ct) >= cuenta.LimiteDeUsuarios;

    /// <summary>Quien ya tiene acceso activo a alguna empresa de la cuenta no ocupa un lugar nuevo.</summary>
    private Task<bool> OcupaLugarAsync(Cuenta cuenta, Guid usuarioId, CancellationToken ct)
        => LimitesDeCuenta.UsuariosQueCuentan(baseDeDatos, cuenta.Id).AnyAsync(u => u.Id == usuarioId, ct);

    private ErrorNegocio ErrorDeLimite(Cuenta cuenta)
        => LimitesDeCuenta.ErrorDeUsuarios(cuenta.LimiteDeUsuarios, opcionesSoporte.Value);

    private async Task<UsuarioDeLaCuentaDto> ADtoAsync(Usuario usuario, Guid cuentaId, CancellationToken ct)
    {
        var empresas = await EmpresasParaAccesoAsync(cuentaId, ct);
        var accesos = await baseDeDatos.UsuariosEmpresas
            .AsNoTracking()
            .Where(ue => ue.UsuarioId == usuario.Id && ue.Activo)
            .Select(ue => ue.EmpresaId)
            .ToListAsync(ct);
        var permisos = await baseDeDatos.UsuariosEmpresasPermisos
            .AsNoTracking()
            .Where(p => p.UsuarioId == usuario.Id)
            .Select(p => p.PermisoClave)
            .Distinct()
            .ToListAsync(ct);

        return ADto(new FilaDeUsuario(usuario.Id, usuario.Nombre, usuario.Email!, usuario.Activo,
            usuario.CreadoPorAdministrador, usuario.EsTitular, accesos, permisos), empresas);
    }

    // El titular no tiene renglones de permiso: la lista los muestra todos, como el token, y
    // entra a todas las empresas de su cuenta.
    private UsuarioDeLaCuentaDto ADto(FilaDeUsuario fila, IReadOnlyList<EmpresaParaAccesoDto> empresasDeLaCuenta)
    {
        var empresas = fila.EsTitular
            ? [.. empresasDeLaCuenta.Select(e => e.Id)]
            : fila.Empresas.Where(id => empresasDeLaCuenta.Any(e => e.Id == id)).ToList();

        return new UsuarioDeLaCuentaDto(
            fila.Id, fila.Nombre, fila.Correo,
            fila.Activo && empresas.Count > 0,
            fila.Id == contexto.UsuarioActual,
            fila.CreadoPorAdministrador,
            fila.EsTitular ? Permisos.Asignables : fila.Permisos,
            fila.EsTitular,
            empresas);
    }

    private async Task<ErrorNegocio?> ErrorSiEsTitularAsync(Guid usuarioId, CancellationToken ct)
        => await baseDeDatos.Users.AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.EsTitular, ct)
            ? ErrorNegocio.Regla("titular-no-modificable", "Nadie puede modificar al titular de la cuenta.")
            : null;

    private static ErrorNegocio? ValidarDatosBasicos(string correo, string nombre, IReadOnlyList<string> permisos)
    {
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(correo) || !EsCorreoValido(correo))
            errores["correo"] = ["Escribe un correo válido."];

        if (string.IsNullOrWhiteSpace(nombre))
            errores["nombre"] = ["El nombre es obligatorio."];

        if (Permisos.ErrorDeAsignacion(permisos) is { } invalido)
            errores["permisos"] = [invalido];

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
