using System.Text.Json.Serialization;

namespace Facturacion.Shared.Comun;

/// <summary>Estados compartidos de los comprobantes. Sus valores forman parte del contrato entre cliente, servidor y base de datos.</summary>
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

/// <summary>Convierte los estados a los valores usados en consultas, almacenamiento y bitácora.</summary>
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
