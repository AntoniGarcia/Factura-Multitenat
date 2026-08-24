using System.Text.Json.Serialization;

namespace Facturacion.Shared.Comun;

/// <summary>
/// Estatus de un comprobante. Son exactamente estos seis y ninguno más (ARQUITECTURA.md §5).
/// Vive en <c>Comun/</c>, carpeta de la mitad A, pero lo consume la mitad B en todo momento:
/// modificar este enum es en la práctica un cambio de contrato y exige acuerdo de los dos.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EstatusComprobante>))]
public enum EstatusComprobante
{
    [JsonStringEnumMemberName("borrador")]
    Borrador,

    [JsonStringEnumMemberName("timbrando")]
    Timbrando,

    [JsonStringEnumMemberName("timbrado")]
    Timbrado,

    [JsonStringEnumMemberName("error")]
    Error,

    [JsonStringEnumMemberName("cancelado")]
    Cancelado,

    [JsonStringEnumMemberName("en_cancelacion")]
    EnCancelacion
}

/// <summary>
/// Conversión a la cadena exacta de ARQUITECTURA.md §5, para lo que no pasa por JSON:
/// columnas de la base, filtros de consulta y bitácora.
/// </summary>
public static class EstatusComprobanteExtensiones
{
    private static readonly Dictionary<EstatusComprobante, string> ACadenas = new()
    {
        [EstatusComprobante.Borrador] = "borrador",
        [EstatusComprobante.Timbrando] = "timbrando",
        [EstatusComprobante.Timbrado] = "timbrado",
        [EstatusComprobante.Error] = "error",
        [EstatusComprobante.Cancelado] = "cancelado",
        [EstatusComprobante.EnCancelacion] = "en_cancelacion"
    };

    private static readonly Dictionary<string, EstatusComprobante> DesdeCadenas =
        ACadenas.ToDictionary(p => p.Value, p => p.Key);

    public static string ACadena(this EstatusComprobante estatus) => ACadenas[estatus];

    public static EstatusComprobante Desde(string valor) => DesdeCadenas.TryGetValue(valor, out var e)
        ? e
        : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Estatus de comprobante desconocido.");
}
