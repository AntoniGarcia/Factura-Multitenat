using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Administración de operadores del SaaS: alta, edición, permisos y baja lógica.
/// </summary>
public sealed class ServicioDeOperadores(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeRefreshTokensDeOperador refrescos,
    IServicioDeBitacora bitacora)
{
    private static readonly HashSet<string> _permisosValidos =
        new(PermisosDePanel.Todos, StringComparer.Ordinal);

    public async Task<Resultado> EliminarAsync(Guid operadorActualId, Guid id, CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var operador = await baseDeDatos.OperadoresPlataforma.Include(x => x.Permisos).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");
        if (id == operadorActualId || operador.EsPrincipal)
            return ErrorNegocio.Regla("operador-protegido", "No puedes eliminar tu acceso ni al operador principal.");
        if (await ValidarPermisosDelegablesAsync(operadorActualId, operador.PermisosDelPanel(), ct) is { } errorDelegacion)
            return errorDelegacion;
        if (operador.EstaEliminado()) return Resultado.Exito();

        bitacora.Registrar(EntidadesDeBitacora.OperadorPlataforma, id.ToString(), AccionesDeBitacora.OperadorEliminado,
            antes: Retrato(operador), operadorId: operadorActualId);
        operador.Activo = false;
        operador.CorreoNormalizado = $"{OperadorPlataforma.PrefijoEliminado}{id:N}";
        operador.HashContrasena = hasher.HashPassword(operador, Guid.NewGuid().ToString("N"));
        baseDeDatos.RemoveRange(operador.Permisos);
        await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(id, "operador eliminado", ct);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return Resultado.Exito();
    }

    /// <summary>Lista paginada de operadores, con sus permisos.</summary>
    public async Task<PaginaDeOperadores> ListarAsync(
        string? texto, bool? activos, bool eliminados, int pagina, int tamano, CancellationToken ct)
    {
        IQueryable<OperadorPlataforma> consulta = baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos)
            .Where(o => o.CorreoNormalizado.StartsWith(OperadorPlataforma.PrefijoEliminado) == eliminados);

        if (activos is { } valor)
            consulta = consulta.Where(o => o.Activo == valor);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            var identificador = Guid.TryParse(texto.Trim(), out var valorId) ? valorId : (Guid?)null;
            consulta = consulta.Where(o =>
                o.Id == identificador ||
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

    public async Task<IReadOnlyList<ActividadDeOperadorDto>> ActividadAsync(Guid id, int pagina, CancellationToken ct)
    {
        var destino = id.ToString();
        return await baseDeDatos.Bitacora.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.OperadorId == id || b.Entidad == EntidadesDeBitacora.OperadorPlataforma && b.EntidadId == destino)
            .OrderByDescending(b => b.MomentoUtc).ThenByDescending(b => b.Id)
            .Skip(pagina * 25).Take(25)
            .Select(b => new ActividadDeOperadorDto(b.MomentoUtc, b.Accion, b.Entidad, b.OperadorId == id)).ToListAsync(ct);
    }

    public async Task<Resultado<int>> LimpiarHistorialDeSesionesAsync(Guid operadorActualId, Guid id, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma.AsNoTracking().Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);
        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");
        if (await ValidarPermisosDelegablesAsync(operadorActualId, operador.PermisosDelPanel(), ct,
                requierePrincipal: operador.EsPrincipal) is { } error)
            return error;

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        var cantidad = await baseDeDatos.Bitacora.IgnoreQueryFilters()
            .Where(b => b.OperadorId == id && b.EmpresaId == null && b.CuentaId == null && b.UsuarioId == null
                && (b.Entidad == EntidadesDeBitacora.SesionOperador
                    && (b.Accion == AccionesDeBitacora.InicioSesionOperador || b.Accion == AccionesDeBitacora.CierreSesionOperador)
                    || b.Entidad == EntidadesDeBitacora.RefreshTokenOperador && b.Accion == AccionesDeBitacora.TokenRotado))
            .ExecuteDeleteAsync(ct);
        if (cantidad > 0)
        {
            bitacora.Registrar(EntidadesDeBitacora.OperadorPlataforma, id.ToString(), AccionesDeBitacora.HistorialDeSesionesLimpiado,
                despues: new { RegistrosEliminados = cantidad }, operadorId: operadorActualId);
            await baseDeDatos.SaveChangesAsync(ct);
        }
        await transaccion.CommitAsync(ct);
        return cantidad;
    }

    /// <summary>Detalle de un operador por Id.</summary>
    public async Task<OperadorDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);
        return operador is null ? null : ADto(operador);
    }

    /// <summary>
    /// Crea un operador nuevo. Devuelve el DTO creado (sin contraseña).
    /// </summary>
    public async Task<Resultado<OperadorDto>> CrearAsync(
        Guid operadorActualId,
        PeticionGuardarOperador peticion,
        CancellationToken ct)
    {
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
    /// contraseña. Para cambiar la contraseña se necesita el permiso específico y se cierran
    /// todas las sesiones del operador afectado.
    /// </summary>
    public async Task<Resultado<OperadorDto>> ActualizarAsync(
        Guid operadorActualId,
        Guid id,
        PeticionGuardarOperador peticion,
        CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var operador = await baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        if (operador.EstaEliminado())
            return ErrorNegocio.Regla("operador-eliminado", "El operador fue eliminado y su acceso no se puede recuperar.");

        if (operador.EsPrincipal || id == operadorActualId)
            return ErrorNegocio.Regla("operador-protegido",
                "Edita tus propios datos desde el perfil. El operador principal no se puede modificar desde aquí.");

        var error = ValidarActualizacion(peticion);
        if (error is not null) return error;

        if (ValidarPermisos(peticion.Permisos) is { } errorPermisos)
            return errorPermisos;

        var cambiaContrasena = !string.IsNullOrWhiteSpace(peticion.Contrasena);
        if (cambiaContrasena && (peticion.Contrasena.Length < 12 || peticion.Contrasena.Length > 128))
            return ErrorNegocio.Validacion(
                "contrasena-invalida", "La contraseña debe tener entre 12 y 128 caracteres.");

        var correo = peticion.Correo.Trim();
        var correoNormalizado = correo.ToUpperInvariant();
        var cambiaCorreo = correoNormalizado != operador.CorreoNormalizado;
        // El correo permite recuperar el acceso, igual que cambiar la contraseña.
        if (await ValidarPermisosDelegablesAsync(operadorActualId,
                operador.PermisosDelPanel().Concat(peticion.Permisos)
                    .Concat(cambiaContrasena ? [PermisosDePanel.CambiarContrasenaOperadores] : [])
                    .Concat(cambiaCorreo ? [PermisosDePanel.CambiarCorreoOperadores] : []).Distinct(), ct)
            is { } errorDelegacion)
            return errorDelegacion;

        if (cambiaCorreo && await baseDeDatos.OperadoresPlataforma
                .AnyAsync(o => o.Id != id && o.CorreoNormalizado == correoNormalizado, ct))
            return ErrorNegocio.Conflicto("correo-repetido", "Ya existe un operador con ese correo.");

        var antes = Retrato(operador);
        var solicitados = peticion.Permisos.ToHashSet(StringComparer.Ordinal);
        var permisosAnteriores = operador.Permisos.Select(p => p.Permiso).ToHashSet(StringComparer.Ordinal);
        var cambiaronPermisos = !permisosAnteriores.SetEquals(solicitados);

        operador.Nombre = peticion.Nombre.Trim();
        operador.Correo = correo;
        operador.CorreoNormalizado = correoNormalizado;

        foreach (var permiso in operador.Permisos.Where(p => !solicitados.Contains(p.Permiso)).ToList())
        {
            operador.Permisos.Remove(permiso);
            baseDeDatos.Remove(permiso);
        }
        foreach (var permiso in solicitados.Except(permisosAnteriores))
            operador.Permisos.Add(new PermisoOperador { Permiso = permiso });

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.OperadorActualizado,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        if (cambiaContrasena)
        {
            operador.HashContrasena = hasher.HashPassword(operador, peticion.Contrasena);

            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, "contraseña restablecida por otro operador", ct);

            bitacora.Registrar(
                EntidadesDeBitacora.OperadorPlataforma,
                operador.Id.ToString(),
                AccionesDeBitacora.ContrasenaCambiada,
                despues: new { SesionDelOperador = operador.Nombre, SesionesCerradas = true },
                operadorId: operadorActualId);
        }

        if (!cambiaContrasena && (cambiaronPermisos || cambiaCorreo))
            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, cambiaCorreo ? "correo modificado" : "permisos modificados", ct);

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return await ReleerAsync(id, ct) is { } actualizado
            ? actualizado
            : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe tras la actualización.");
    }

    /// <summary>Alta o baja lógica de un operador. Al dar de baja se invalidan sus sesiones.</summary>
    public async Task<Resultado<OperadorDto>> CambiarActivoAsync(
        Guid operadorActualId,
        Guid id,
        PeticionCambiarActivoOperador peticion,
        CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var operador = await baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        if (operador.EstaEliminado())
            return ErrorNegocio.Regla("operador-eliminado", "El operador fue eliminado y su acceso no se puede recuperar.");

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
        if (await ValidarPermisosDelegablesAsync(operadorActualId, operador.PermisosDelPanel(), ct) is { } errorDelegacion)
            return errorDelegacion;

        var antes = Retrato(operador);

        operador.Activo = peticion.Activo;

        if (!peticion.Activo)
            await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
                id, "desactivado por operador", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            peticion.Activo ? AccionesDeBitacora.OperadorReactivado : AccionesDeBitacora.OperadorDesactivado,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

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
    /// Solo se guardan permisos concretos del panel.
    /// </summary>
    private static ErrorNegocio? ValidarPermisos(IReadOnlyList<string>? permisos)
    {
        if (permisos is not { Count: > 0 })
            return ErrorNegocio.Validacion("sin-permisos", "El operador debe tener al menos un permiso.");

        foreach (var permiso in permisos)
        {
            if (!_permisosValidos.Contains(permiso))
                return ErrorNegocio.Validacion("permiso-invalido", $"Permiso desconocido: {permiso}");

        }

        return null;
    }

    private async Task<ErrorNegocio?> ValidarPermisosDelegablesAsync(
        Guid operadorActualId, IEnumerable<string> permisos, CancellationToken ct, bool requierePrincipal = false)
    {
        var actual = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == operadorActualId && o.Activo, ct);

        if (actual is null)
            return ErrorNegocio.Validacion("operador-no-activo", "Tu cuenta de operador no está activa.");

        if (actual.EsPrincipal) return null;
        if (requierePrincipal)
            return ErrorNegocio.Regla("operador-protegido", "Solo el operador principal puede limpiar su historial de sesiones.");

        var propios = actual.PermisosDelPanel();
        return permisos.FirstOrDefault(permiso => !propios.Contains(permiso)) is { } noDelegable
            ? ErrorNegocio.Regla(
                "permiso-no-delegable",
                $"Para hacer este cambio necesitas «{PermisosDePanel.EtiquetaCorta(noDelegable)}».")
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

    private async Task<OperadorDto?> ReleerAsync(Guid id, CancellationToken ct)
        => await ObtenerAsync(id, ct);

    private static OperadorDto ADto(OperadorPlataforma o) => new(
        o.Id,
        o.Nombre,
        o.Correo,
        o.Activo,
        o.EsPrincipal,
        o.FechaAltaUtc,
        o.PermisosDelPanel(), o.EstaEliminado());

    private static object Retrato(OperadorPlataforma o) => new
    {
        o.Nombre,
        o.Correo,
        o.Activo,
        Permisos = o.Permisos.Select(p => p.Permiso).OrderBy(x => x).ToList()
    };
}
