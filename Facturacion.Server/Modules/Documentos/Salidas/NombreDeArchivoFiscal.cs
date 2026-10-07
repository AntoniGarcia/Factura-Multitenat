using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Nombre de los archivos de un CFDI: <c>SERIE_FOLIO_RFCRECEPTOR_DDMMAAAA_HHMMSS</c>, con la
/// fecha de emisión en la hora del lugar de expedición. Es la convención del sistema anterior;
/// los contadores de los clientes tienen sus carpetas y sus reglas de archivo montadas sobre
/// ella, así que la descarga y el correo la usan igual.
/// </summary>
public static class NombreDeArchivoFiscal
{
    /// <summary>Lo que Windows no admite en un nombre de archivo. Un RFC con Ñ o &amp; sí se queda.</summary>
    private static readonly char[] NoAdmitidos = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static string Construir(Comprobante comprobante, TimeZoneInfo zona, string extension)
    {
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);

        var partes = new List<string>(4);

        // Sin serie se omite el segmento en vez de dejar un guion bajo al principio.
        if (!string.IsNullOrWhiteSpace(comprobante.Serie))
            partes.Add(comprobante.Serie);

        // Timbrado sin serie no tiene folio interno; el UUID es lo único que lo distingue.
        partes.Add(comprobante.Folio?.ToString(CultureInfo.InvariantCulture)
            ?? comprobante.Uuid?.ToString("N").ToUpperInvariant()
            ?? comprobante.Id.ToString("N"));

        partes.Add(comprobante.ReceptorRfc);
        partes.Add(fechaLocal.ToString("ddMMyyyy_HHmmss", CultureInfo.InvariantCulture));

        var nombre = string.Join('_', partes);

        foreach (var caracter in NoAdmitidos)
            nombre = nombre.Replace(caracter, '-');

        return $"{nombre}.{extension}";
    }
}
