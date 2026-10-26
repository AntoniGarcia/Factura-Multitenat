using Facturacion.Shared.Plataforma;

namespace Facturacion.Shared.Operador;

public sealed record DefinicionCatalogoSat(string Clave, string Nombre, bool RequiereBusqueda = false);
public sealed record CatalogoSatDeOperadorDto(DefinicionCatalogoSat Definicion, CatalogoVersionDto? UltimaCarga);
public sealed record FilaCatalogoSatDto(string Clave, string Descripcion, string? Contexto,
    bool Vigente, DateOnly InicioVigencia, DateOnly? FinVigencia);
public sealed record PaginaCatalogoSatDto(IReadOnlyList<FilaCatalogoSatDto> Elementos, int Total);

public static class CatalogosSatDeConsulta
{
    public static IReadOnlyList<DefinicionCatalogoSat> Disponibles { get; } =
    [
        new("c_UsoCFDI", "Uso de CFDI"), new("c_RegimenFiscal", "Regímenes fiscales"),
        new("c_MetodoPago", "Métodos de pago"), new("c_FormaPago", "Formas de pago"),
        new("c_Moneda", "Monedas"), new("c_ObjetoImp", "Objeto de impuesto"),
        new("c_TipoRelacion", "Tipos de relación"), new("c_Periodicidad", "Periodicidades"),
        new("c_Meses", "Meses"), new("c_Exportacion", "Exportación"),
        new("c_TipoDeComprobante", "Tipos de comprobante"), new("c_Impuesto", "Impuestos"),
        new("c_TipoFactor", "Tipos de factor"), new("c_TasaOCuota", "Tasas y cuotas"),
        new("c_ClaveProdServ", "Productos y servicios", true), new("c_ClaveUnidad", "Unidades"),
        new("c_Pais", "Países"), new("c_Estado", "Estados"),
        new("c_Municipio", "Municipios", true), new("c_CodigoPostal", "Códigos postales", true),
        new("c_Colonia", "Colonias", true), new("c_ConfigAutotransporte", "Configuraciones de autotransporte"),
        new("c_TipoPermiso", "Permisos de transporte"), new("c_FiguraTransporte", "Figuras de transporte"),
        new("c_ClaveProdServCP", "Mercancías de Carta Porte", true),
        new("c_INCOTERM", "Incoterms"), new("c_UnidadAduana", "Unidades de aduana"),
        new("c_FraccionArancelaria", "Fracciones arancelarias", true)
    ];
}
