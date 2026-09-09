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
    IServicioDeBitacora bitacora)
{
    private static readonly HashSet<string> _permisosValidos =
        new(PermisosDePanel.Todos, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Actualiza nombre y correo de un operador. No cambia contraseña ni permisos.</summary>
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

        var error = ValidarActualizacion(peticion, operador);
        if (error is not null) return error;

        var antes = Retrato(operador);

        operador.Nombre = peticion.Nombre.Trim();
        operador.Correo = peticion.Correo.Trim();
        operador.CorreoNormalizado = peticion.Correo.Trim().ToUpperInvariant();

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.OperadorActualizado,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(id, ct) is { } actualizado
            ? actualizado
            : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe tras la actualización.");
    }

    /// <summary>Reemplaza completamente los permisos de un operador.</summary>
    public async Task<Resultado<OperadorDto>> CambiarPermisosAsync(
        Guid operadorActualId,
        Guid id,
        PeticionPermisosOperador peticion,
        string contrasenaOperadorActual,
        CancellationToken ct)
    {
        if (await ContrasenaIncorrecta(operadorActualId, contrasenaOperadorActual, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        if (!peticion.Permisos.Any())
            return ErrorNegocio.Validacion("sin-permisos", "El operador debe tener al menos un permiso.");

        foreach (var p in peticion.Permisos)
        {
            if (!_permisosValidos.Contains(p))
                return ErrorNegocio.Validacion("permiso-invalido", $"Permiso desconocido: {p}");

            // Una acción sin su sección no se sostiene: para administrar algo hay que poder verlo.
            if (PermisosDePanel.VerQueExige(p) is { } ver && !peticion.Permisos.Contains(ver))
                return ErrorNegocio.Validacion(
                    "accion-sin-seccion",
                    $"No puedes «{PermisosDePanel.EtiquetaCorta(p)}» sin «{PermisosDePanel.EtiquetaCorta(ver)}».");
        }

        var operador = await baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        var antes = Retrato(operador);

        operador.Permisos.Clear();
        foreach (var permiso in peticion.Permisos.Distinct(StringComparer.OrdinalIgnoreCase))
            operador.Permisos.Add(new PermisoOperador { Permiso = permiso });

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.OperadorPermisosCambiados,
            antes: antes,
            despues: Retrato(operador),
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(id, ct) is { } conPermisos
            ? conPermisos
            : ErrorNegocio.NoEncontrado("operador-no-encontrado", "El operador no existe tras el cambio de permisos.");
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

    /// <summary>
    /// El operador principal le pone una contraseña nueva a otro operador y cierra todas sus
    /// sesiones. Es la operación más delicada del panel —equivale a entrar como otra persona—,
    /// por eso la puede ejecutar solo el principal y exige su re-autenticación.
    /// </summary>
    public async Task<Resultado<bool>> CambiarContrasenaAsync(
        Guid operadorActualId,
        Guid id,
        PeticionContrasenaNuevaDeOperador peticion,
        string contrasenaOperadorActual,
        CancellationToken ct)
    {
        if (await ContrasenaIncorrecta(operadorActualId, contrasenaOperadorActual, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var operadorActual = await baseDeDatos.OperadoresPlataforma
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == operadorActualId, ct);

        // Solo el dueño del SaaS puede dejar fuera a alguien poniéndole otra contraseña.
        if (operadorActual is null || !operadorActual.EsPrincipal)
            return ErrorNegocio.Regla(
                "solo-principal", "Solo el operador principal puede cambiar la contraseña de otro operador.");

        if (id == operadorActualId)
            return ErrorNegocio.Regla(
                "auto-cambio", "No puedes cambiarte la contraseña desde aquí: hazlo desde tu perfil.");

        if (peticion.ContrasenaNueva.Length < 12 || peticion.ContrasenaNueva.Length > 128)
            return ErrorNegocio.Validacion(
                "contrasena-invalida", "La contraseña debe tener entre 12 y 128 caracteres.");

        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (operador is null)
            return ErrorNegocio.NoEncontrado("operador-no-encontrado", "Ese operador no existe.");

        operador.HashContrasena = hasher.HashPassword(operador, peticion.ContrasenaNueva);

        await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
            id, "contraseña restablecida por el operador principal", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operador.Id.ToString(),
            AccionesDeBitacora.ContrasenaCambiada,
            despues: new { SesionDelOperador = operador.Nombre, SesionesCerradas = true },
            operadorId: operadorActualId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
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

        if (!p.Permisos.Any())
            return ErrorNegocio.Validacion("sin-permisos", "El operador debe tener al menos un permiso.");

        foreach (var permiso in p.Permisos)
        {
            if (!_permisosValidos.Contains(permiso))
                return ErrorNegocio.Validacion("permiso-invalido", $"Permiso desconocido: {permiso}");

            // Una acción sin su sección no se sostiene: para administrar algo hay que poder verlo.
            if (PermisosDePanel.VerQueExige(permiso) is { } ver && !p.Permisos.Contains(ver))
                return ErrorNegocio.Validacion(
                    "accion-sin-seccion",
                    $"No puedes «{PermisosDePanel.EtiquetaCorta(permiso)}» sin «{PermisosDePanel.EtiquetaCorta(ver)}».");
        }

        return null;
    }

    private static ErrorNegocio? ValidarActualizacion(PeticionGuardarOperador p, OperadorPlataforma operador)
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