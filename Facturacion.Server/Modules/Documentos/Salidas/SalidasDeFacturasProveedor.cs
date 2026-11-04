using System.Text;
using System.Xml;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

public sealed class SalidasDeFacturasProveedor(
    IEmisionDeFacturasDeCompras emision, GeneradorDePdfCfdi generador,
    ILogger<SalidasDeFacturasProveedor> registro)
{
    public async Task<Resultado<ArchivoFiscal>> ObtenerAsync(Guid compraId, bool pdf, CancellationToken ct)
    {
        var r = await emision.ObtenerEmitidaAsync(compraId, ct);
        if (r.EsFallo) return r.Error!;
        var f = r.Valor;
        var nombre = $"factura-compra-{f.Uuid:N}";
        if (!pdf) return new ArchivoFiscal(nombre + ".xml", "application/xml", Encoding.UTF8.GetBytes(f.XmlTimbrado));
        try
        {
            var (c, datos) = FacturaProveedorDesdeXml.Leer(f);
            return new ArchivoFiscal(nombre + ".pdf", "application/pdf", generador.Generar(c, datos));
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or FormatException or
            OverflowException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            registro.LogWarning("No se pudo generar el PDF de compra {Compra}; tipo {Tipo}.", compraId, ex.GetType().Name);
            return ErrorNegocio.Regla("pdf-compra-no-disponible", "No se pudo generar el PDF de esta factura.");
        }
    }
}

public static class SalidasDeFacturasProveedorEndpoints
{
    public static void MapSalidasDeFacturasProveedor(this IEndpointRouteBuilder rutas)
    {
        var operador = rutas.MapGroup("/api/operador/compras").WithTags("Operador · Facturas de compras")
            .RequireAuthorization(PoliticasDeOperador.Operador, PoliticasDeOperador.VerCompras);
        var cliente = rutas.MapGroup("/api/timbres/compras").WithTags("Timbres · Facturas de compras")
            .RequireAuthorization(Permisos.ComprarTimbres);
        foreach (var grupo in new[] { operador, cliente })
        {
            grupo.MapGet("/{id:guid}/factura/pdf", (Guid id, SalidasDeFacturasProveedor servicio, HttpContext http, CancellationToken ct)
                => Descargar(id, true, servicio, http, ct));
            grupo.MapGet("/{id:guid}/factura/xml", (Guid id, SalidasDeFacturasProveedor servicio, HttpContext http, CancellationToken ct)
                => Descargar(id, false, servicio, http, ct));
        }
    }

    private static async Task<IResult> Descargar(Guid id, bool pdf, SalidasDeFacturasProveedor servicio, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var r = await servicio.ObtenerAsync(id, pdf, ct);
        return r.EsExito ? Results.File(r.Valor.Contenido, r.Valor.TipoContenido, r.Valor.Nombre)
            : r.Error!.AResultado(http);
    }
}
