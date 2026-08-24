using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioProductos"/> para pruebas en Development.
/// Devuelve 3 productos reales con claves SAT válidas.
/// </summary>
public sealed class DobleServicioProductos : IServicioProductos
{
    private static readonly List<ProductoParaConceptoDto> ProductosDisponibles = new()
    {
        new ProductoParaConceptoDto(
            ProductoId: Guid.NewGuid(),
            ClaveProdServ: "84101600",
            ClaveUnidad: "H87",
            UnidadTexto: "Prestación de Servicios",
            Descripcion: "Servicios de consultoría",
            ValorUnitario: 1000.00m,
            ObjetoImp: "02",
            Impuestos: new[]
            {
                new ImpuestoProductoDto(
                    Impuesto: "002",
                    TipoFactor: "Tasa",
                    TasaOCuota: 0.160000m,
                    EsRetencion: false)
            }),
        new ProductoParaConceptoDto(
            ProductoId: Guid.NewGuid(),
            ClaveProdServ: "01010101",
            ClaveUnidad: "LTR",
            UnidadTexto: "Litro",
            Descripcion: "Provision de agua",
            ValorUnitario: 50.00m,
            ObjetoImp: "02",
            Impuestos: new[]
            {
                new ImpuestoProductoDto(
                    Impuesto: "002",
                    TipoFactor: "Tasa",
                    TasaOCuota: 0.160000m,
                    EsRetencion: false)
            }),
        new ProductoParaConceptoDto(
            ProductoId: Guid.NewGuid(),
            ClaveProdServ: "80131700",
            ClaveUnidad: "H87",
            UnidadTexto: "Prestación de Servicios",
            Descripcion: "Servicios de educación",
            ValorUnitario: 500.00m,
            ObjetoImp: "02",
            Impuestos: new[]
            {
                new ImpuestoProductoDto(
                    Impuesto: "002",
                    TipoFactor: "Tasa",
                    TasaOCuota: 0.160000m,
                    EsRetencion: false)
            }),
    };

    public Task<ProductoParaConceptoDto?> ObtenerParaConceptoAsync(Guid productoId, CancellationToken ct)
    {
        // Devuelve el primer producto disponible para cualquier ID en pruebas.
        var producto = ProductosDisponibles.FirstOrDefault();
        return Task.FromResult(producto);
    }
}
