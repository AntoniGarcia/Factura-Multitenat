using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Transporte;

/// <summary>Catálogos propios de transporte. La empresa activa se impone con el filtro global.</summary>
public static class CatalogosDeTransporteEndpoints
{
    public static void MapCatalogosDeTransporte(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/transporte").WithTags("Transporte");

        grupo.MapGet("/vehiculos", async (AppDbContext db, bool? activos, CancellationToken ct) =>
            await db.Vehiculos.AsNoTracking().Where(x => activos == null || x.Activo == activos)
                .OrderBy(x => x.Descripcion).Select(x => ADto(x)).ToListAsync(ct)).RequireAuthorization();
        grupo.MapGet("/figuras", async (AppDbContext db, bool? activos, CancellationToken ct) =>
            await db.FigurasTransporte.AsNoTracking().Where(x => activos == null || x.Activo == activos)
                .OrderBy(x => x.Nombre).Select(x => ADto(x)).ToListAsync(ct)).RequireAuthorization();

        grupo.MapPost("/vehiculos", CrearVehiculo).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/vehiculos/{id:guid}", ActualizarVehiculo).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/vehiculos/{id:guid}/activo", CambiarVehiculoActivo).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/figuras", CrearFigura).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/figuras/{id:guid}", ActualizarFigura).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/figuras/{id:guid}/activo", CambiarFiguraActiva).RequireAuthorization(Permisos.ConfigurarEmpresa);
    }

    private static async Task<IResult> CrearVehiculo(PeticionGuardarVehiculo p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var error = await ValidarVehiculo(p, db, ct);
        if (error is not null) return error.AResultado(http);
        var entidad = new Vehiculo { Id = Guid.NewGuid(), Clave = p.Clave.Trim(), Descripcion = p.Descripcion.Trim(),
            ConfiguracionAutotransporte = p.ConfiguracionAutotransporte, Placa = p.Placa.Trim(), AnioModelo = p.AnioModelo,
            Aseguradora = p.Aseguradora.Trim(), Poliza = p.Poliza.Trim(), TipoPermiso = p.TipoPermiso,
            NumeroPermiso = p.NumeroPermiso.Trim(), PesoBrutoVehicular = p.PesoBrutoVehicular, Activo = p.Activo, FechaAltaUtc = DateTime.UtcNow };
        db.Vehiculos.Add(entidad); await db.SaveChangesAsync(ct); return Results.Ok(ADto(entidad));
    }

    private static async Task<IResult> ActualizarVehiculo(Guid id, PeticionGuardarVehiculo p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var entidad = await db.Vehiculos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entidad is null) return ErrorNegocio.NoEncontrado("vehiculo-no-encontrado", "No se encontró ese vehículo.").AResultado(http);
        var error = await ValidarVehiculo(p, db, ct); if (error is not null) return error.AResultado(http);
        entidad.Clave = p.Clave.Trim(); entidad.Descripcion = p.Descripcion.Trim(); entidad.ConfiguracionAutotransporte = p.ConfiguracionAutotransporte;
        entidad.Placa = p.Placa.Trim(); entidad.AnioModelo = p.AnioModelo; entidad.Aseguradora = p.Aseguradora.Trim(); entidad.Poliza = p.Poliza.Trim();
        entidad.TipoPermiso = p.TipoPermiso; entidad.NumeroPermiso = p.NumeroPermiso.Trim(); entidad.PesoBrutoVehicular = p.PesoBrutoVehicular;
        entidad.Activo = p.Activo; entidad.FechaModificacionUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.Ok(ADto(entidad));
    }

    private static async Task<IResult> CambiarVehiculoActivo(Guid id, PeticionActivo p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (vehiculo is null) return ErrorNegocio.NoEncontrado("vehiculo-no-encontrado", "No se encontró ese vehículo.").AResultado(http);
        vehiculo.Activo = p.Activo; vehiculo.FechaModificacionUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Results.Ok(ADto(vehiculo));
    }

    private static async Task<IResult> CrearFigura(PeticionGuardarFiguraTransporte p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var error = await ValidarFigura(p, db, ct); if (error is not null) return error.AResultado(http);
        var entidad = AFigura(p); db.FigurasTransporte.Add(entidad); await db.SaveChangesAsync(ct); return Results.Ok(ADto(entidad));
    }

    private static async Task<IResult> ActualizarFigura(Guid id, PeticionGuardarFiguraTransporte p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var entidad = await db.FigurasTransporte.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entidad is null) return ErrorNegocio.NoEncontrado("figura-no-encontrada", "No se encontró esa figura de transporte.").AResultado(http);
        var error = await ValidarFigura(p, db, ct); if (error is not null) return error.AResultado(http);
        Copiar(p, entidad); entidad.FechaModificacionUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.Ok(ADto(entidad));
    }

    private static async Task<IResult> CambiarFiguraActiva(Guid id, PeticionActivo p, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var figura = await db.FigurasTransporte.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (figura is null) return ErrorNegocio.NoEncontrado("figura-no-encontrada", "No se encontró esa figura de transporte.").AResultado(http);
        figura.Activo = p.Activo; figura.FechaModificacionUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Results.Ok(ADto(figura));
    }

    private static async Task<ErrorNegocio?> ValidarVehiculo(PeticionGuardarVehiculo p, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Clave) || string.IsNullOrWhiteSpace(p.Descripcion) || string.IsNullOrWhiteSpace(p.Placa) ||
            string.IsNullOrWhiteSpace(p.Aseguradora) || string.IsNullOrWhiteSpace(p.Poliza) || string.IsNullOrWhiteSpace(p.NumeroPermiso) ||
            p.AnioModelo is < 1900 or > 2100 || p.PesoBrutoVehicular <= 0)
            return ErrorNegocio.Validacion("vehiculo-incompleto", "Completa los datos obligatorios del vehículo.");
        if (!await db.SatConfiguracionesAutotransporte.AnyAsync(x => x.Clave == p.ConfiguracionAutotransporte && x.Vigente, ct) ||
            !await db.SatTiposPermiso.AnyAsync(x => x.Clave == p.TipoPermiso && x.Vigente, ct))
            return ErrorNegocio.Validacion("catalogo-sat-invalido", "La configuración o el tipo de permiso no están vigentes en el SAT.");
        return null;
    }

    private static async Task<ErrorNegocio?> ValidarFigura(PeticionGuardarFiguraTransporte p, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Clave) || string.IsNullOrWhiteSpace(p.Rfc) || string.IsNullOrWhiteSpace(p.Nombre) ||
            string.IsNullOrWhiteSpace(p.Calle) || string.IsNullOrWhiteSpace(p.NumeroExterior) || p.CodigoPostal.Length != 5 ||
            (p.TipoFigura == "01" && string.IsNullOrWhiteSpace(p.NumeroLicencia)))
            return ErrorNegocio.Validacion("figura-incompleta", "Completa los datos fiscales y el domicilio de la figura.");
        if (!await db.SatFigurasTransporte.AnyAsync(x => x.Clave == p.TipoFigura && x.Vigente, ct) ||
            !await db.SatMunicipios.AnyAsync(x => x.Clave == p.Municipio && x.ClaveEstado == p.Estado && x.Vigente, ct) ||
            !await db.SatCodigosPostales.AnyAsync(x => x.Clave == p.CodigoPostal && x.ClaveEstado == p.Estado && x.ClaveMunicipio == p.Municipio && x.Vigente, ct))
            return ErrorNegocio.Validacion("domicilio-transporte-invalido", "El tipo de figura o su domicilio no coincide con los catálogos SAT vigentes.");
        return null;
    }

    private static VehiculoDto ADto(Vehiculo x) => new(x.Id, x.Clave, x.Descripcion, x.ConfiguracionAutotransporte, x.Placa, x.AnioModelo, x.Aseguradora, x.Poliza, x.TipoPermiso, x.NumeroPermiso, x.PesoBrutoVehicular, x.Activo);
    private static FiguraTransporteDto ADto(FiguraTransporte x) => new(x.Id, x.Clave, x.TipoFigura, x.Rfc, x.Nombre, x.NumeroLicencia, x.Calle, x.NumeroExterior, x.NumeroInterior, x.Estado, x.Municipio, x.CodigoPostal, x.Activo);
    private static FiguraTransporte AFigura(PeticionGuardarFiguraTransporte p) => new()
    {
        Id = Guid.NewGuid(), FechaAltaUtc = DateTime.UtcNow, Clave = p.Clave.Trim(),
        TipoFigura = p.TipoFigura, Rfc = p.Rfc.Trim().ToUpperInvariant(), Nombre = p.Nombre.Trim(),
        NumeroLicencia = p.NumeroLicencia?.Trim(), Calle = p.Calle.Trim(),
        NumeroExterior = p.NumeroExterior.Trim(), NumeroInterior = p.NumeroInterior?.Trim(),
        Estado = p.Estado, Municipio = p.Municipio, CodigoPostal = p.CodigoPostal, Activo = p.Activo
    };
    private static void Copiar(PeticionGuardarFiguraTransporte p, FiguraTransporte x) { x.Clave = p.Clave.Trim(); x.TipoFigura = p.TipoFigura; x.Rfc = p.Rfc.Trim().ToUpperInvariant(); x.Nombre = p.Nombre.Trim(); x.NumeroLicencia = p.NumeroLicencia?.Trim(); x.Calle = p.Calle.Trim(); x.NumeroExterior = p.NumeroExterior.Trim(); x.NumeroInterior = p.NumeroInterior?.Trim(); x.Estado = p.Estado; x.Municipio = p.Municipio; x.CodigoPostal = p.CodigoPostal; x.Activo = p.Activo; }
    private sealed record PeticionActivo(bool Activo);
}
