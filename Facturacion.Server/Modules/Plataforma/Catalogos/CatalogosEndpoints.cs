using System.Security.Cryptography;
using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>
/// Los cuatro endpoints de consulta de catálogos del SAT (ARQUITECTURA.md §7). Todos exigen
/// sesión: no hay pantalla del sistema que se use sin haber iniciado sesión, y así se evita
/// exponer sin autenticar un catálogo completo a quien sepa la URL.
/// </summary>
public static class CatalogosEndpoints
{
    /// <summary>
    /// Los catálogos chicos y estables que ARQUITECTURA.md §7 dice que se precargan en el Client.
    /// </summary>
    private static readonly string[] Precargables =
    [
        "c_UsoCFDI", "c_RegimenFiscal", "c_MetodoPago", "c_FormaPago", "c_Moneda",
        "c_ObjetoImp", "c_TipoRelacion", "c_Periodicidad", "c_Meses", "c_Exportacion",
        "c_TipoDeComprobante", "c_Impuesto", "c_TipoFactor"
    ];

    public static void MapCatalogos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/catalogos").WithTags("Catálogos del SAT").RequireAuthorization();

        grupo.MapGet("/precargables", ObtenerPrecargables);
        grupo.MapGet("/version", ObtenerVersiones);
        grupo.MapGet("/codigo-postal/{codigoPostal}/domicilio", ResolverDomicilioPorCodigoPostal);
        grupo.MapGet("/estados/{estado}/municipios", MunicipiosDeEstado);
        grupo.MapGet("/estados/{estado}/municipios/{municipio}/codigos-postales", CodigosPostalesDeMunicipio);
        grupo.MapGet("/{catalogo}/buscar", Buscar);
        grupo.MapGet("/{catalogo}/opciones", Opciones);
        grupo.MapGet("/{catalogo}/{clave}", Resolver);
    }

    private static async Task<IResult> Buscar(
        string catalogo, string? texto, int? tope,
        IServicioCatalogosSat catalogos, HttpContext contexto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
            return ErrorNegocio.Validacion("texto-corto", "Escribe al menos tres caracteres para buscar.")
                .AResultado(contexto);

        var topeEfectivo = Math.Clamp(tope ?? 20, 1, 50);

        try
        {
            var resultado = await catalogos.BuscarAsync(catalogo, texto.Trim(), topeEfectivo, ct);
            return Results.Ok(resultado);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or NotSupportedException)
        {
            return ErrorNegocio.Validacion("catalogo-invalido", ex.Message).AResultado(contexto);
        }
    }

    /// <summary>Listas completas solo donde el catálogo es pequeño y estable.</summary>
    private static async Task<IResult> Opciones(string catalogo, AppDbContext db, HttpContext contexto, CancellationToken ct)
    {
        IReadOnlyList<ClaveSatDto>? resultado = catalogo switch
        {
            "c_ConfigAutotransporte" => await Proyectar(db.SatConfiguracionesAutotransporte, catalogo, ct),
            "c_TipoPermiso" => await Proyectar(db.SatTiposPermiso, catalogo, ct),
            "c_FiguraTransporte" => await Proyectar(db.SatFigurasTransporte, catalogo, ct),
            "c_Estado" => await db.SatEstados.AsNoTracking().Where(x => x.Vigente).OrderBy(x => x.Nombre)
                .Select(x => new ClaveSatDto(catalogo, x.Clave, x.Nombre, x.Vigente)).ToListAsync(ct),
            _ => null
        };

        return resultado is null
            ? ErrorNegocio.Validacion("catalogo-no-listable", "Ese catálogo se busca por texto para evitar cargar demasiados datos.").AResultado(contexto)
            : Results.Ok(resultado);
    }

    private static async Task<IResult> ResolverDomicilioPorCodigoPostal(
        string codigoPostal, AppDbContext db, HttpContext contexto, CancellationToken ct)
    {
        var codigo = await db.SatCodigosPostales.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Clave == codigoPostal && x.Vigente, ct);
        if (codigo is null)
            return ErrorNegocio.NoEncontrado("codigo-postal-no-encontrado", "Ese código postal no está vigente en el catálogo SAT.").AResultado(contexto);

        var estado = await db.SatEstados.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Clave == codigo.ClaveEstado && x.Vigente, ct);
        if (estado is null)
            return ErrorNegocio.Validacion("estado-no-encontrado", "El código postal no tiene un estado SAT vigente asociado.").AResultado(contexto);

        var municipio = codigo.ClaveMunicipio is null ? null : await db.SatMunicipios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Clave == codigo.ClaveMunicipio && x.ClaveEstado == codigo.ClaveEstado && x.Vigente, ct);

        return Results.Ok(new Facturacion.Shared.Transporte.DomicilioPorCodigoPostalDto(
            codigo.Clave, estado.Clave, estado.Nombre, municipio?.Clave, municipio?.Descripcion));
    }

    private static async Task<IResult> MunicipiosDeEstado(string estado, AppDbContext db, CancellationToken ct)
        => Results.Ok(await db.SatMunicipios.AsNoTracking().Where(x => x.ClaveEstado == estado && x.Vigente)
            .OrderBy(x => x.Descripcion).Select(x => new ClaveSatDto("c_Municipio", x.Clave, x.Descripcion, true)).ToListAsync(ct));

    private static async Task<IResult> CodigosPostalesDeMunicipio(
        string estado, string municipio, string? texto, AppDbContext db, CancellationToken ct)
    {
        var filtro = (texto ?? string.Empty).Trim();
        return Results.Ok(await db.SatCodigosPostales.AsNoTracking()
            .Where(x => x.Vigente && x.ClaveEstado == estado && x.ClaveMunicipio == municipio &&
                        (filtro.Length == 0 || x.Clave.StartsWith(filtro)))
            .OrderBy(x => x.Clave).Take(100)
            .Select(x => new ClaveSatDto("c_CodigoPostal", x.Clave, x.Clave, true)).ToListAsync(ct));
    }

    private static async Task<IResult> Resolver(
        string catalogo, string clave, IServicioCatalogosSat catalogos, HttpContext contexto, CancellationToken ct)
    {
        try
        {
            var resultado = await catalogos.ResolverAsync(catalogo, clave, ct);
            return resultado is null
                ? ErrorNegocio.NoEncontrado("clave-no-encontrada", $"'{clave}' no existe en {catalogo}.").AResultado(contexto)
                : Results.Ok(resultado);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ErrorNegocio.Validacion("catalogo-invalido", ex.Message).AResultado(contexto);
        }
    }

    /// <summary>
    /// Todos los catálogos precargables en una sola respuesta, con ETag calculado a partir
    /// de cuándo se cargó cada uno por última vez: mientras nadie vuelva a correr el
    /// importador, el Client puede quedarse con su copia en memoria sin volver a pedirla.
    /// </summary>
    private static async Task<IResult> ObtenerPrecargables(AppDbContext db, HttpContext contexto, CancellationToken ct)
    {
        var versiones = await db.CatalogoVersiones.AsNoTracking()
            .Where(v => Precargables.Contains(v.Catalogo))
            .OrderBy(v => v.Catalogo)
            .Select(v => new { v.Catalogo, v.FechaCargaUtc, v.RenglonesVigentes })
            .ToListAsync(ct);

        var etag = CalcularEtag(string.Join('|', versiones.Select(v => $"{v.Catalogo}:{v.FechaCargaUtc:O}:{v.RenglonesVigentes}")));

        if (contexto.Request.Headers.IfNoneMatch.Any(valor => valor == etag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        contexto.Response.Headers.ETag = etag;

        var resultado = new Dictionary<string, IReadOnlyList<ClaveSatDto>>(Precargables.Length);

        foreach (var catalogo in Precargables)
            resultado[catalogo] = await BuscarTodoVigenteAsync(db, catalogo, ct);

        return Results.Ok(resultado);
    }

    private static async Task<IReadOnlyList<ClaveSatDto>> BuscarTodoVigenteAsync(
        AppDbContext db, string catalogo, CancellationToken ct) => catalogo switch
    {
        "c_FormaPago" => await Proyectar(db.SatFormasPago, catalogo, ct),
        "c_Exportacion" => await Proyectar(db.SatExportaciones, catalogo, ct),
        "c_MetodoPago" => await Proyectar(db.SatMetodosPago, catalogo, ct),
        "c_Periodicidad" => await Proyectar(db.SatPeriodicidades, catalogo, ct),
        "c_Meses" => await Proyectar(db.SatMeses, catalogo, ct),
        "c_TipoRelacion" => await Proyectar(db.SatTiposRelacion, catalogo, ct),
        "c_ObjetoImp" => await Proyectar(db.SatObjetosImp, catalogo, ct),
        "c_Moneda" => await Proyectar(db.SatMonedas, catalogo, ct),
        "c_TipoDeComprobante" => await Proyectar(db.SatTiposDeComprobante, catalogo, ct),
        "c_RegimenFiscal" => await Proyectar(db.SatRegimenesFiscales, catalogo, ct),
        "c_UsoCFDI" => await Proyectar(db.SatUsosCfdi, catalogo, ct),
        "c_Impuesto" => await Proyectar(db.SatImpuestos, catalogo, ct),
        "c_TipoFactor" => await db.SatTiposFactor.AsNoTracking().Where(x => x.Vigente)
            .OrderBy(x => x.Clave)
            .Select(x => new ClaveSatDto(catalogo, x.Clave, x.Clave, x.Vigente))
            .ToListAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(catalogo), catalogo, "No es un catálogo precargable.")
    };

    private static Task<List<ClaveSatDto>> Proyectar<TEntidad>(DbSet<TEntidad> tabla, string catalogo, CancellationToken ct)
        where TEntidad : class, Data.Entidades.Plataforma.Catalogos.ISatCatalogoSimple
        => tabla.AsNoTracking().Where(x => x.Vigente)
            .OrderBy(x => x.Clave)
            .Select(x => new ClaveSatDto(catalogo, x.Clave, x.Descripcion, x.Vigente))
            .ToListAsync(ct);

    private static async Task<IResult> ObtenerVersiones(AppDbContext db, CancellationToken ct)
    {
        var versiones = await db.CatalogoVersiones.AsNoTracking()
            .OrderBy(v => v.Catalogo)
            .Select(v => new CatalogoVersionDto(
                v.Catalogo, v.VersionCatalogo, v.RevisionCatalogo, v.FechaPublicacion, v.FechaCargaUtc, v.RenglonesVigentes))
            .ToListAsync(ct);

        return Results.Ok(versiones);
    }

    private static string CalcularEtag(string contenido)
        => $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)))}\"";
}
