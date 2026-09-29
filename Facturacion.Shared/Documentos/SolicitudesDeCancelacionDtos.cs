namespace Facturacion.Shared.Documentos;

/// <summary>Filtros de la pantalla de solicitudes de cancelación (§30).</summary>
public static class FiltrosDeSolicitudes
{
    /// <summary>Las que siguen sin desenlace: en proceso o esperando al receptor.</summary>
    public const string Abiertas = "abiertas";

    public const string Todas = "todas";
}

/// <summary>
/// Un renglón de la pantalla de §30: la solicitud, el comprobante al que pertenece y lo último
/// que contestó el SAT.
/// </summary>
/// <param name="Estado">
/// De la solicitud, no del comprobante: <c>en_proceso</c>, <c>en_espera_del_receptor</c>,
/// <c>cancelada</c>, <c>rechazada</c> o <c>error</c>.
/// </param>
/// <param name="ConsultadaUtc">Última vez que se preguntó al SAT. Nulo si nunca.</param>
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

/// <param name="Abiertas">Cuántas siguen sin desenlace, sin importar el filtro aplicado.</param>
public sealed record PaginaDeSolicitudesDeCancelacion(
    IReadOnlyList<SolicitudDeCancelacionEnListaDto> Elementos,
    int Total,
    int Abiertas);

/// <summary>Resultado de «Verificar estatus SAT» sobre todas las solicitudes abiertas.</summary>
/// <param name="Actualizadas">Comprobantes cuyo estatus cambió por lo que dijo el SAT.</param>
/// <param name="Restantes">
/// Abiertas que no entraron en esta pasada por el tope por petición; se revisan en la siguiente.
/// </param>
public sealed record ResultadoDeVerificacionMasivaDto(
    int Revisadas,
    int Actualizadas,
    int Fallidas,
    int Restantes);
