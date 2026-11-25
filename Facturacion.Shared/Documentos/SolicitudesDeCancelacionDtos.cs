namespace Facturacion.Shared.Documentos;

public static class FiltrosDeSolicitudes
{
    // En proceso o esperando al receptor.
    public const string Abiertas = "abiertas";

    public const string Todas = "todas";
}

// Estado de la solicitud, no del comprobante: en_proceso, en_espera_del_receptor, cancelada,
// rechazada o error. ConsultadaUtc: última consulta al SAT; nulo si nunca.
public sealed record SolicitudDeCancelacionEnListaDto(
    Guid Id,
    Guid ComprobanteId,
    string TipoDeComprobante,
    string? Serie,
    int? Folio,
    Guid? Uuid,
    string ReceptorRfc,
    string ReceptorNombre,
    decimal Total,
    string Moneda,
    string Motivo,
    Guid? UuidSustituye,
    DateTime SolicitadaUtc,
    string Estado,
    DateTime? ResueltaUtc,
    string? MensajeRespuesta,
    string EstatusComprobante,
    string? EstadoCfdiSat,
    string? EsCancelableSat,
    string? EstatusCancelacionSat,
    DateTime? ConsultadaUtc);

// Abiertas: cuántas siguen sin desenlace, sin importar el filtro.
public sealed record PaginaDeSolicitudesDeCancelacion(
    IReadOnlyList<SolicitudDeCancelacionEnListaDto> Elementos,
    int Total,
    int Abiertas);

// Restantes: abiertas que no entraron por el tope por petición; se revisan en la siguiente.
public sealed record ResultadoDeVerificacionMasivaDto(
    int Revisadas,
    int Actualizadas,
    int Fallidas,
    int Restantes);
