namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Un timbre apartado para un comprobante mientras dura el timbrado.
///
/// <para><b>A diferencia del folio, el timbre sí se devuelve</b></para>
/// Un folio abandonado no se recicla porque la numeración consecutiva tiene que poder
/// explicar cada hueco (ver <see cref="ReservaFolio"/>). Un timbre es lo contrario: es
/// dinero que el cliente pagó. Si el timbrado no se completó, el PAC no cobró nada y
/// quedarse con el timbre sería cobrarle dos veces por un comprobante que no existe.
/// Por eso hay un estado <see cref="EstadosDeReservaTimbre.Devuelto"/> y un barrido que lo
/// aplica solo.
/// </summary>
public sealed class ReservaTimbre : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>
    /// Comprobante para el que se apartó. Sin llave foránea a propósito: la tabla de
    /// comprobantes es de la mitad B y esta mitad no la conoce ni puede depender de su
    /// esquema (REPARTO-EQUIPO.md §3).
    /// </summary>
    public Guid ComprobanteId { get; set; }

    /// <summary>Uno de <see cref="EstadosDeReservaTimbre"/>.</summary>
    public required string Estado { get; set; }

    public DateTime CreadaUtc { get; set; }

    /// <summary>Cuándo se confirmó o se devolvió. Nulo mientras sigue apartada.</summary>
    public DateTime? ResueltaUtc { get; set; }

    /// <summary>Por qué se devolvió. Nulo si se confirmó.</summary>
    public string? MotivoResolucion { get; set; }
}

/// <summary>Estados de una reserva de timbre. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class EstadosDeReservaTimbre
{
    /// <summary>Apartado, con el timbrado todavía en curso. Es el estado en el que nace.</summary>
    public const string Reservado = "reservado";

    /// <summary>El comprobante se timbró y el timbre se gastó.</summary>
    public const string Confirmado = "confirmado";

    /// <summary>El timbrado no se completó y el timbre volvió al saldo disponible.</summary>
    public const string Devuelto = "devuelto";
}
