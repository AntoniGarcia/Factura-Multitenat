using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Administración de operadores del SaaS: alta, edición, permisos y baja lógica.
/// <para>
/// Todas las operaciones exigen la contraseña del operador que las ejecuta (re-autenticación),
/// igual que el restablecimiento de contraseña de usuarios de inquilinos. Esto evita que una
/// sesión abierta sin vigilancia permita crear operadores con permisos elevados.
/// </para>
/// </summary>
public sealed class ServicioDeOperadores(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeRefreshTokensDeOperador refrescos,
    IServicioDeBitacora bitacora,
    IControlDeIntentos intentos)
{
    private static readonly HashSet<string> _permisosValidos =
        new(PermisosDePanel.Todos, StringComparer.OrdinalIgnoreCase);

    public async Task<Resultado> EliminarAsync(Guid operadorActualId, Guid id, string contrasena, CancellationToken ct)
    {
        var claveIntentos = $"eliminar-operador:{operadorActualId}";
        if (intentos.BloqueoRestante(claveIntentos) is not null)
            return ErrorNegocio.LimiteExcedido("demasiados-intentos", "Espera antes de volver a confirmar tu contraseña.");
        var actual = await baseDeDatos.OperadoresPlataforma.SingleOrDefaultAsync(x => x.Id == operadorActualId && x.Activo, ct);
        if (actual is null || actual.BloqueadoHastaUtc > DateTime.UtcNow)
            return ErrorNegocio.Regla("operador-no-activo", "Tu acceso de operador no está disponible.");
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length > 128 ||
            await ContrasenaIncorrecta(operadorActualId, contrasena, ct))
        {
            intentos.RegistrarFallo(claveIntentos);
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");
        }
        intentos.Limpiar(claveIntentos);

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var operador = await baseDeDatos.OperadoresPlataforma.Include(x => x.Permisos).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");
        if (id == operadorActualId || operador.EsPrincipal)
            return ErrorNegocio.Regla("operador-protegido", "No puedes eliminar tu acceso ni al operador principal.");
        if (operador.UltimoAccesoUtc is not null ||
            await baseDeDatos.Bitacora.IgnoreQueryFilters().AnyAsync(x => x.OperadorId == id, ct) ||
            await baseDeDatos.RefreshTokensOperador.AnyAsync(x => x.OperadorId == id, ct) ||
            await baseDeDatos.ConfiguracionesDelSistema.AnyAsync(x => x.ActualizadaPorOperadorId == id, ct))
            return ErrorNegocio.Regla("operador-con-historial", "Tiene actividad registrada. Desactívalo para conservar su historial.");

        bitacora.Registrar(EntidadesDeBitacora.OperadorPlataforma, id.ToString(), AccionesDeBitacora.OperadorEliminado,
            antes: Retrato(operador), operadorId: operadorActualId);
        baseDeDatos.OperadoresPlataforma.Remove(operador);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return Resultado.Exito();
    }

    /// <summary>Lista paginada de operadores, con sus permisos.</summary>
    public async Task<PaginaDeOperadores> ListarAsync(
        string? texto, bool? activos, int pagina, int tamano, CancellationToken ct)
    {
        IQueryable<OperadorPlataforma> consulta = baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos);

        if (activos is { } valor)
            consulta = consulta.Where(o => o.Activo == valor);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            consulta = consulta.Where(o =>
                EF.Functions.Like(o.Nombre, patron) ||
                EF.Functions.Like(o.Correo, patron));
        }

        var total = await consulta.CountAsync(ct);

        var crudos = await consulta
            .OrderBy(o => o.Nombre)
            .Skip(pagina * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        var elementos = crudos.Select(ADto).ToList();

        return new PaginaDeOperadores(elementos, total);
    }

    /// <summary>Detalle de un operador por Id.</summary>
    public async Task<OperadorDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos)
            .Where(o => o.Id == id)
            .Select(o => ADto(o))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Crea un operador nuevo. Devuelve el DTO creado (sin contraseña).
    /// <para>
    /// Exige re-autenticación del operador que crea: se pasa su contraseña en <paramref name="contrasenaOperadorActual"/>.
    /// </para>
    /// </summary>
    public async Task<Resultado<OperadorDto>> CrearAsync(
        Guid operadorActualId,
        PeticionGuardarOperador peticion,
        string contrasenaOperadorActual,
        CancellationToken ct)
    {
        if (await ContrasenaIncorrecta(operadorActualId, contrasenaOperadorActual, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var error = ValidarCreacion(peticion);
        if (error is not null) return error;

        if (await ValidarPermisosDelegablesAsync(operadorActualId, peticion.Permisos, ct) is { } errorDelegacion)
            return errorDelegacion;

        var correoNormalizado = peticion.Correo.Trim().ToUpperInvariant();

        var existe = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .AnyAsync(o => o.CorreoNormalizado == correoNormalizado, ct);

        if (existe)
            return ErrorNegocio.Conflicto("correo-repetido", "Ya existe un operador con ese correo.");

        var operador = new OperadorPlataforma
        {
            Id = Guid.NewGuid(),
            Nombre = peticion.Nombre.Trim(),
            Correo = peticion.Correo.Trim(),
            CorreoNormalizado = correoNormalizado,
            HashContrasena = hasher.HashPassword(null!, peticion.Contrasena),
            Activo = true,
            FechaAltaUtc = DateTime.UtcNow,
            Permisos = peticion.Permisos
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new PermisoOperador { Permiso = p }).ToList()
        };

        baseDeDatos.OperadoresPlataforma.Add(operador);

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.OperadorCreado,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);

        var releido = await ReleerAsync(operador.Id, ct);
        return releido is { } creado
            ? creado
            : ErrorNegocio.Regla("operador-no-creado", "No se pudo leer el operador recién creado.");
    }

    /// <summary>
    /// Actualiza un operador: nombre, correo, permisos y, si viene contraseña nueva, la
    /// contraseña. Cambiar la contraseña de otro operador es la operación más delicada del
    /// panel —equivale a entrar como otra persona—, por eso solo la puede ejecutar el principal
    /// y, al hacerlo, se cierran todas las sesiones del operador afectado.
    /// </summary>
    public async Task<Resultado<OperadorDto>> ActualizarAsync(
        Guid operadorActualId,
        Guid id,
        PeticionGuardarOperador peticion,
        string contrasenaOperadorActual,
        CancellationToken ct)
    {
        if (await ContrasenaIncorrecta(operadorActualId, contrasenaOperadorActual, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var operador = await baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        var error = ValidarActualizacion(peticion);
        if (error is not null) return error;

        if (ValidarPermisos(peticion.Permisos) is { } errorPermisos)
            return errorPermisos;

        if (await ValidarPermisosDelegablesAsync(operadorActualId, peticion.Permisos, ct) is { } errorDelegacion)
            return errorDelegacion;

        var antes = Retrato(operador);
        var permisosAnteriores = operador.Permisos.Select(p => p.Permiso).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cambiaronPermisos = !permisosAnteriores.SetEquals(peticion.Permisos);

        operador.Nombre = peticion.Nombre.Trim();
        operador.Correo = peticion.Correo.Trim();
        operador.CorreoNormalizado = peticion.Correo.Trim().ToUpperInvariant();

        operador.Permisos.Clear();
        foreach (var permiso in peticion.Permisos.Distinct(StringComparer.OrdinalIgnoreCase))
            operador.Permisos.Add(new PermisoOperador { Permiso = permiso });

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.OperadorActualizado,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        if (!string.IsNullOrWhiteSpace(peticion.Contrasena))
        {
            if (await SoloPrincipalParaContrasenaAsync(operadorActualId, id, ct) is { } errorSeguridad)
                return errorSeguridad;

            if (peticion.Contrasena.Length < 12 || peticion.Contrasena.Length > 128)
                return ErrorNegocio.Validacion(
                    "contrasena-invalida", "La contraseña debe tener entre 12 y 128 caracteres.");

            operador.HashContrasena = hasher.HashPassword(operador, peticion.Contrasena);

            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, "contraseña restablecida por el operador principal", ct);

            bitacora.Registrar(
                EntidadesDeBitacora.OperadorPlataforma,
                operador.Id.ToString(),
                AccionesDeBitacora.ContrasenaCambiada,
                despues: new { SesionDelOperador = operador.Nombre, SesionesCerradas = true },
                operadorId: operadorActualId);
        }

        if (cambiaronPermisos && string.IsNullOrWhiteSpace(peticion.Contrasena))
            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, "permisos modificados", ct);

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(id, ct) is { } actualizado
            ? actualizado
            : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe tras la actualización.");
    }

    /// <summary>
    /// Las reglas de seguridad de cambiarle la contraseña a otro operador, antes de tocarla:
    /// solo el principal puede, y no sobre sí mismo — eso vive en el perfil.
    /// </summary>
    private async Task<ErrorNegocio?> SoloPrincipalParaContrasenaAsync(
        Guid operadorActualId, Guid id, CancellationToken ct)
    {
        if (id == operadorActualId)
            return ErrorNegocio.Regla(
                "auto-cambio", "No puedes cambiarte la contraseña desde aquí: hazlo desde tu perfil.");

        var operadorActual = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == operadorActualId, ct);

        if (operadorActual is null || !operadorActual.EsPrincipal)
            return ErrorNegocio.Regla(
                "solo-principal", "Solo el operador principal puede cambiar la contraseña de otro operador.");

        return null;
    }

    /// <summary>Alta o baja lógica de un operador. Al dar de baja se invalidan sus sesiones.</summary>
    public async Task<Resultado<OperadorDto>> CambiarActivoAsync(
        Guid operadorActualId,
        Guid id,
        PeticionCambiarActivoOperador peticion,
        string contrasenaOperadorActual,
        CancellationToken ct)
    {
        if (await ContrasenaIncorrecta(operadorActualId, contrasenaOperadorActual, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var operador = await baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        if (operador.Activo == peticion.Activo)
            return await ReleerAsync(id, ct) is { } mismo
                ? mismo
                : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe.");

        // Un operador no puede desactivarse a sí mismo.
        if (id == operadorActualId && !peticion.Activo)
            return ErrorNegocio.Regla("auto-desactivacion", "No puedes desactivar tu propio acceso.");

        // El operador principal (dueño del SaaS) no puede ser desactivado por nadie:
        // la plataforma quedaría sin administrador.
        if (operador.EsPrincipal && !peticion.Activo)
            return ErrorNegocio.Regla("principal-no-desactivable", "El operador principal no puede ser desactivado.");

        var antes = Retrato(operador);

        operador.Activo = peticion.Activo;

        if (!peticion.Activo)
            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, peticion.Motivo ?? "desactivado por operador", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            peticion.Activo ? AccionesDeBitacora.OperadorReactivado : AccionesDeBitacora.OperadorDesactivado,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(id, ct) is { } final
            ? final
            : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe tras el cambio de estado.");
    }

    private static ErrorNegocio? ValidarCreacion(PeticionGuardarOperador p)
    {
        if (string.IsNullOrWhiteSpace(p.Nombre))
            return ErrorNegocio.Validacion("nombre-requerido", "El operador necesita un nombre.");

        if (p.Nombre.Trim().Length > 128)
            return ErrorNegocio.Validacion("nombre-muy-largo", "El nombre no puede pasar de 128 caracteres.");

        if (string.IsNullOrWhiteSpace(p.Correo))
            return ErrorNegocio.Validacion("correo-requerido", "El operador necesita un correo.");

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(p.Correo.Trim()))
            return ErrorNegocio.Validacion("correo-invalido", "El correo no tiene un formato válido.");

        if (string.IsNullOrWhiteSpace(p.Contrasena))
            return ErrorNegocio.Validacion("contrasena-requerida", "La contraseña es obligatoria.");

        if (p.Contrasena.Length < 12 || p.Contrasena.Length > 128)
            return ErrorNegocio.Validacion("contrasena-invalida", "La contraseña debe tener entre 12 y 128 caracteres.");

        var errorPermisos = ValidarPermisos(p.Permisos);
        if (errorPermisos is not null) return errorPermisos;

        return null;
    }

    /// <summary>
    /// Reglas comunes de permisos al crear o al actualizar: al menos uno, conocidos y, si es
    /// una acción, con su sección de ver incluida.
    /// </summary>
    private static ErrorNegocio? ValidarPermisos(IReadOnlyList<string> permisos)
    {
        if (!permisos.Any())
            return ErrorNegocio.Validacion("sin-permisos", "El operador debe tener al menos un permiso.");

        foreach (var permiso in permisos)
        {
            if (!_permisosValidos.Contains(permiso))
                return ErrorNegocio.Validacion("permiso-invalido", $"Permiso desconocido: {permiso}");

            // Una acción sin su sección no se sostiene: para administrar algo hay que poder verlo.
            if (PermisosDePanel.VerQueExige(permiso) is { } ver && !permisos.Contains(ver, StringComparer.OrdinalIgnoreCase))
                return ErrorNegocio.Validacion(
                    "accion-sin-seccion",
                    $"No puedes «{PermisosDePanel.EtiquetaCorta(permiso)}» sin «{PermisosDePanel.EtiquetaCorta(ver)}».");
        }

        return null;
    }

    private async Task<ErrorNegocio?> ValidarPermisosDelegablesAsync(
        Guid operadorActualId, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        var actual = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == operadorActualId && o.Activo, ct);

        if (actual is null)
            return ErrorNegocio.Validacion("operador-no-activo", "Tu cuenta de operador no está activa.");

        if (actual.EsPrincipal) return null;

        var propios = actual.Permisos.Select(p => p.Permiso);
        return permisos.FirstOrDefault(permiso => !PermisosDePanel.Autoriza(propios, permiso)) is { } noDelegable
            ? ErrorNegocio.Regla(
                "permiso-no-delegable",
                $"No puedes otorgar «{PermisosDePanel.EtiquetaCorta(noDelegable)}» porque no tienes ese permiso.")
            : null;
    }

    private static ErrorNegocio? ValidarActualizacion(PeticionGuardarOperador p)
    {
        if (string.IsNullOrWhiteSpace(p.Nombre))
            return ErrorNegocio.Validacion("nombre-requerido", "El operador necesita un nombre.");

        if (p.Nombre.Trim().Length > 128)
            return ErrorNegocio.Validacion("nombre-muy-largo", "El nombre no puede pasar de 128 caracteres.");

        if (string.IsNullOrWhiteSpace(p.Correo))
            return ErrorNegocio.Validacion("correo-requerido", "El operador necesita un correo.");

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(p.Correo.Trim()))
            return ErrorNegocio.Validacion("correo-invalido", "El correo no tiene un formato válido.");

        return null;
    }

    private async Task<bool> ContrasenaIncorrecta(
        Guid operadorId, string contrasena, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

        if (operador is null) return true;

        return hasher.VerifyHashedPassword(operador, operador.HashContrasena, contrasena)
            is PasswordVerificationResult.Failed;
    }

    private async Task<OperadorDto?> ReleerAsync(Guid id, CancellationToken ct)
        => await ObtenerAsync(id, ct);

    private static OperadorDto ADto(OperadorPlataforma o) => new(
        o.Id,
        o.Nombre,
        o.Correo,
        o.Activo,
        o.EsPrincipal,
        o.FechaAltaUtc,
        [.. o.Permisos.Select(p => p.Permiso)]);

    private static object Retrato(OperadorPlataforma o) => new
    {
        o.Nombre,
        o.Correo,
        o.Activo,
        Permisos = o.Permisos.Select(p => p.Permiso).OrderBy(x => x).ToList()
    };
}
