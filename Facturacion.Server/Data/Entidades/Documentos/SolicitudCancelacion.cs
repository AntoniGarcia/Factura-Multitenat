namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Una solicitud de cancelación ante el SAT. Es una entidad propia y no un par de campos en
/// el comprobante porque la cancelación no es instantánea: con los motivos que exigen
/// aceptación del receptor, el comprobante queda en <c>en_cancelacion</c> mientras el otro
/// acepta, rechaza o deja vencer el plazo. Eso tiene su propio ciclo de vida y su propia
/// respuesta del SAT.
/// </summary>
public sealed class SolicitudCancelacion : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Uno de <see cref="MotivosDeCancelacion"/>.</summary>
    public required string Motivo { get; set; }

    /// <summary>
    /// Folio fiscal del comprobante que sustituye al cancelado. El SAT lo exige
    /// <b>solo</b> con el motivo 01, y lo rechaza en los demás.
    /// </summary>
    public Guid? UuidSustituye { get; set; }

    /// <summary>Uno de <see cref="EstadosDeSolicitud"/>.</summary>
    public required string Estado { get; set; }

    public DateTime SolicitadaUtc { get; set; }

    /// <summary>Cuándo el SAT dio una respuesta definitiva. Nulo mientras sigue en proceso.</summary>
    public DateTime? ResueltaUtc { get; set; }

    /// <summary>Código de respuesta del SAT, para soporte.</summary>
    public string? CodigoRespuesta { get; set; }

    public string? MensajeRespuesta { get; set; }

    public Guid SolicitadaPorUsuarioId { get; set; }
}

/// <summary>
/// Los cuatro motivos del SAT. Fuera del MVP no hay más (CLAUDE.md §6).
/// </summary>
public static class MotivosDeCancelacion
{
    /// <summary>Comprobante emitido con errores <b>con</b> relación. Exige el UUID que lo sustituye.</summary>
    public const string ConErroresConRelacion = "01";

    /// <summary>Comprobante emitido con errores sin relación.</summary>
    public const string ConErroresSinRelacion = "02";

    /// <summary>No se llevó a cabo la operación.</summary>
    public const string NoSeLlevoACabo = "03";

    /// <summary>Operación nominativa relacionada en una factura global.</summary>
    public const string NominativaEnGlobal = "04";
}

/// <summary>Estados de una solicitud de cancelación.</summary>
public static class EstadosDeSolicitud
{
    /// <summary>Enviada al SAT, sin respuesta definitiva.</summary>
    public const string EnProceso = "en_proceso";

    /// <summary>Esperando que el receptor acepte o rechace.</summary>
    public const string EnEsperaDelReceptor = "en_espera_del_receptor";

    public const string Cancelada = "cancelada";

    public const string Rechazada = "rechazada";

    /// <summary>Falló el envío. Se puede reintentar.</summary>
    public const string Error = "error";
}
