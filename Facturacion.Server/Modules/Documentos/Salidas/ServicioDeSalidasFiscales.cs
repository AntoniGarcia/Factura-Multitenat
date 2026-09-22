using System.Security.Cryptography;
using System.Xml;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Entrega los archivos de un CFDI ya timbrado. Las consultas pasan por el filtro global de
/// empresa y el XML se lee con el propósito criptográfico de XML timbrado, por lo que ni una
/// ruta alterada ni un identificador de otra empresa permiten obtener un archivo ajeno.
/// </summary>
public sealed class ServicioDeSalidasFiscales(
    AppDbContext baseDeDatos,
    GeneradorDePdfCfdi generadorPdf,
    GeneradorDePdfCartaPorte generadorCartaPorte,
    HusoDeEmpresa huso,
    ServicioDeLogo logos,
    IAlmacenDeArchivos almacen,
    ILogger<ServicioDeSalidasFiscales> registro)
{
    public async Task<Resultado<ArchivoFiscal>> GenerarPdfAsync(Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await CargarAsync(comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado(
                "comprobante-no-encontrado", "Ese comprobante no existe.");

        if (!EsFiscal(comprobante))
            return ErrorNegocio.Conflicto(
                "comprobante-sin-cfdi", "El PDF fiscal solo está disponible después del timbrado.");

        // El complemento de pagos no contiene los conceptos de una factura y fingir que sí
        // los tiene produciría un reporte incorrecto. Carta Porte usa su propio generador.
        if (comprobante.TipoDeComprobante is not ("I" or "T"))
            return ErrorNegocio.Conflicto(
                "pdf-no-disponible-para-tipo",
                "La representación PDF para este tipo de comprobante todavía no está disponible.");

        var decimales = await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == comprobante.Moneda)
            .Select(m => (int?)m.Decimales)
            .FirstOrDefaultAsync(ct) ?? 2;

        var zona = await huso.ObtenerAsync(ct);
        if (comprobante.TipoDeComprobante == "T")
        {
            var traslado = await baseDeDatos.TrasladosCartaPorte
                .AsNoTracking()
                .Include(x => x.Ubicaciones)
                .Include(x => x.Mercancias)
                .FirstOrDefaultAsync(x => x.ComprobanteId == comprobante.Id, ct);

            if (traslado is null)
                return ErrorNegocio.Regla(
                    "traslado-sin-carta-porte", "El CFDI de traslado no tiene los datos de Carta Porte requeridos.");

            return new ArchivoFiscal(
                NombreDeArchivo(comprobante, "pdf"), "application/pdf",
                generadorCartaPorte.Generar(comprobante, traslado, zona, esBorrador: false));
        }

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);
        var logo = await logos.ObtenerAsync(ct);
        var archivoXml = await ObtenerXmlAsync(comprobanteId, ct);
        if (archivoXml.EsFallo) return archivoXml.Error!;

        DatosNotarialesDelPdf? datosNotariales;
        try
        {
            datosNotariales = DatosNotarialesDelPdf.DesdeXml(archivoXml.Valor.Contenido);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or FormatException or OverflowException)
        {
            registro.LogError(ex, "El XML timbrado de {Comprobante} no se pudo interpretar para el PDF.", comprobanteId);
            return ErrorNegocio.Regla("xml-no-disponible",
                "El XML de este CFDI no se pudo leer para generar el PDF. Avisa a soporte.");
        }

        var contenido = generadorPdf.Generar(comprobante,
            new DatosDelPdf(fechaLocal, decimales, logo?.Contenido, Notaria: datosNotariales));

        return new ArchivoFiscal(NombreDeArchivo(comprobante, "pdf"), "application/pdf", contenido);
    }

    public async Task<Resultado<ArchivoFiscal>> ObtenerXmlAsync(Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado(
                "comprobante-no-encontrado", "Ese comprobante no existe.");

        if (!EsFiscal(comprobante))
            return ErrorNegocio.Conflicto(
                "comprobante-sin-cfdi", "El XML fiscal solo está disponible después del timbrado.");

        if (string.IsNullOrWhiteSpace(comprobante.RutaXml))
            return ErrorNegocio.NoEncontrado(
                "xml-no-disponible",
                "El CFDI fue timbrado, pero su XML aún no está disponible para descarga. Avisa a soporte.");

        try
        {
            var contenido = await almacen.LeerAsync(
                comprobante.EmpresaId, CategoriasDeArchivo.XmlTimbrado, comprobante.RutaXml, ct);

            return new ArchivoFiscal(NombreDeArchivo(comprobante, "xml"), "application/xml", contenido);
        }
        catch (FileNotFoundException ex)
        {
            registro.LogError(ex, "No se encontró el XML timbrado de {Comprobante} en el almacén.", comprobante.Id);

            return ErrorNegocio.NoEncontrado(
                "xml-no-disponible",
                "El XML de este CFDI no está disponible para descarga. Avisa a soporte.");
        }
        catch (CryptographicException ex)
        {
            registro.LogError(ex, "No se pudo descifrar el XML timbrado de {Comprobante}.", comprobante.Id);

            return ErrorNegocio.Regla(
                "xml-no-disponible",
                "El XML de este CFDI no está disponible para descarga. Avisa a soporte.");
        }
    }

    private async Task<Comprobante?> CargarAsync(Guid comprobanteId, CancellationToken ct)
        => await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Include(c => c.Conceptos.OrderBy(x => x.Orden))
                .ThenInclude(x => x.Impuestos)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

    private static bool EsFiscal(Comprobante comprobante)
        => comprobante.Estatus is "timbrado" or "cancelado" && comprobante.Uuid is not null;

    private static string NombreDeArchivo(Comprobante comprobante, string extension)
    {
        var serie = string.IsNullOrWhiteSpace(comprobante.Serie) ? string.Empty : $"{comprobante.Serie}-";
        var folio = comprobante.Folio?.ToString() ?? comprobante.Uuid!.Value.ToString("N");

        return $"cfdi-{serie}{folio}.{extension}";
    }
}

/// <summary>Archivo fiscal preparado para una respuesta HTTP autenticada.</summary>
public sealed record ArchivoFiscal(string Nombre, string TipoContenido, byte[] Contenido);
