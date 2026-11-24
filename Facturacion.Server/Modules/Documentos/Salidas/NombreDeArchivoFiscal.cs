using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

// SERIE_FOLIO_RFCRECEPTOR_DDMMAAAA_HHMMSS con la hora del lugar de expedición: la convención
// del sistema anterior, sobre la que los contadores tienen montado su archivo.
public static class NombreDeArchivoFiscal
{
    // Lo que Windows no admite en un nombre de archivo; un RFC con Ñ o & sí se queda.
    private static readonly char[] NoAdmitidos = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static string Construir(Comprobante comprobante, TimeZoneInfo zona, string extension)
    {
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);

        var partes = new List<string>(4);

        // Sin serie se omite el segmento en vez de dejar un guion bajo al principio.
        if (!string.IsNullOrWhiteSpace(comprobante.Serie))
            partes.Add(comprobante.Serie);

        // Sin serie no hay folio interno; el UUID es lo único que lo distingue.
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
