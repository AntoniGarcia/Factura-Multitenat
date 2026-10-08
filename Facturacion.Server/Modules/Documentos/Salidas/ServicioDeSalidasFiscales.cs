using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
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

        // Carta Porte usa su propio generador; el complemento de pagos tiene su propia sección
        // en GeneradorDePdfCfdi, sin conceptos. Nómina y egresos no se emiten todavía.
        if (comprobante.TipoDeComprobante is not ("I" or "T" or "P"))
            return ErrorNegocio.Conflicto(
                "pdf-no-disponible-para-tipo",
                "La representación PDF para este tipo de comprobante todavía no está disponible.");

        // La raíz de un pago va en XXX, sin decimales; sus importes viven en el complemento y
        // se escriben con los de la moneda del pago, igual que en ServicioDeXmlCfdi.
        var monedaDeImportes = comprobante.TipoDeComprobante == "P" && comprobante.Pagos.Count > 0
            ? comprobante.Pagos[0].MonedaP
            : comprobante.Moneda;

        var decimales = await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == monedaDeImportes)
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

            var xmlDelTraslado = await ObtenerXmlAsync(comprobanteId, ct);
            if (xmlDelTraslado.EsFallo) return xmlDelTraslado.Error!;

            string? leyendaDelTraslado;
            try
            {
                leyendaDelTraslado = LeerLeyendaDelTimbre(xmlDelTraslado.Valor.Contenido);
            }
            catch (XmlException ex)
            {
                registro.LogError(ex, "El XML timbrado de {Comprobante} no se pudo interpretar para el PDF.", comprobanteId);
                return ErrorNegocio.Regla("xml-no-disponible",
                    "El XML de este CFDI no se pudo leer para generar el PDF. Avisa a soporte.");
            }

            return new ArchivoFiscal(
                NombreDeArchivoFiscal.Construir(comprobante, zona, "pdf"), "application/pdf",
                generadorCartaPorte.Generar(comprobante, traslado, zona, esBorrador: false, leyendaDelTraslado));
        }

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);
        var logo = await logos.ObtenerAsync(ct);
        var archivoXml = await ObtenerXmlAsync(comprobanteId, ct);
        if (archivoXml.EsFallo) return archivoXml.Error!;

        DatosNotarialesDelPdf? datosNotariales;
        decimal? retencionCincoAlMillar;
        string? leyenda;
        try
        {
            datosNotariales = DatosNotarialesDelPdf.DesdeXml(archivoXml.Valor.Contenido);
            retencionCincoAlMillar = LeerCincoAlMillar(archivoXml.Valor.Contenido);
            leyenda = LeerLeyendaDelTimbre(archivoXml.Valor.Contenido);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or FormatException or OverflowException)
        {
            registro.LogError(ex, "El XML timbrado de {Comprobante} no se pudo interpretar para el PDF.", comprobanteId);
            return ErrorNegocio.Regla("xml-no-disponible",
                "El XML de este CFDI no se pudo leer para generar el PDF. Avisa a soporte.");
        }

        var contenido = generadorPdf.Generar(comprobante,
            new DatosDelPdf(fechaLocal, decimales, logo?.Contenido, Notaria: datosNotariales,
                RetencionCincoAlMillar: retencionCincoAlMillar, LeyendaDelTimbre: leyenda) { ZonaHoraria = zona });

        return new ArchivoFiscal(NombreDeArchivoFiscal.Construir(comprobante, zona, "pdf"), "application/pdf", contenido);
    }

    private static string? LeerLeyendaDelTimbre(byte[] contenido)
    {
        using var lector = XmlReader.Create(new MemoryStream(contenido), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        return (string?)XDocument.Load(lector)
            .Descendants(XNamespace.Get(EspacioDeNombresTimbre) + "TimbreFiscalDigital")
            .FirstOrDefault()?
            .Attribute("Leyenda");
    }

    private const string EspacioDeNombresTimbre = "http://www.sat.gob.mx/TimbreFiscalDigital";

    private static decimal? LeerCincoAlMillar(byte[] contenido)
    {
        using var lector = XmlReader.Create(new MemoryStream(contenido), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        var documento = XDocument.Load(lector);
        var espacio = XNamespace.Get(EsquemasSat.EspacioDeNombresImpuestosLocales);
        var complemento = documento.Descendants(espacio + "ImpuestosLocales").FirstOrDefault();
        if (complemento is null) return null;

        var retencion = complemento.Elements(espacio + "RetencionesLocales")
            .FirstOrDefault(x => (string?)x.Attribute("ImpLocRetenido") == "5 al millar");
        if (retencion is null || (string?)retencion.Attribute("TasadeRetencion") != "0.50" ||
            !decimal.TryParse((string?)retencion.Attribute("Importe"),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var importe) || importe < 0 ||
            !decimal.TryParse((string?)complemento.Attribute("TotaldeRetenciones"),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var total) || total != importe)
            throw new InvalidDataException("El complemento de 5 al millar no tiene un importe válido.");

        return importe;
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

            var zona = await huso.ObtenerAsync(ct);

            return new ArchivoFiscal(
                NombreDeArchivoFiscal.Construir(comprobante, zona, "xml"), "application/xml", contenido);
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

    /// <summary>El XML timbrado y su PDF juntos, con las mismas validaciones que cada uno por separado.</summary>
    public async Task<Resultado<ArchivoFiscal>> GenerarZipAsync(Guid comprobanteId, CancellationToken ct)
    {
        var xml = await ObtenerXmlAsync(comprobanteId, ct);
        if (xml.EsFallo) return xml.Error!;

        var pdf = await GenerarPdfAsync(comprobanteId, ct);
        if (pdf.EsFallo) return pdf.Error!;

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var archivo in new[] { xml.Valor, pdf.Valor })
            {
                await using var flujo = zip.CreateEntry(archivo.Nombre, CompressionLevel.Optimal).Open();
                await flujo.WriteAsync(archivo.Contenido, ct);
            }
        }

        return new ArchivoFiscal(Path.ChangeExtension(xml.Valor.Nombre, "zip"), "application/zip", memoria.ToArray());
    }

    private async Task<Comprobante?> CargarAsync(Guid comprobanteId, CancellationToken ct)
        => await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Include(c => c.Conceptos.OrderBy(x => x.Orden))
                .ThenInclude(x => x.Impuestos)
            .Include(c => c.Relacionados)
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

    private static bool EsFiscal(Comprobante comprobante)
        => comprobante.Estatus is "timbrado" or "cancelado" && comprobante.Uuid is not null;

}

/// <summary>Archivo fiscal preparado para una respuesta HTTP autenticada.</summary>
public sealed record ArchivoFiscal(string Nombre, string TipoContenido, byte[] Contenido);
