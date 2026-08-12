namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Respuesta guardada de una petición con <c>Idempotency-Key</c>. Ante una repetición se
/// devuelve exactamente la misma respuesta durante 24 horas (CLAUDE.md §4).
/// <para>
/// Lleva <c>EmpresaId</c> y sí entra al filtro global: la clave de una empresa no puede
/// colisionar con la de otra ni devolverle su respuesta.
/// </para>
/// </summary>
public sealed class ClaveIdempotencia : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid UsuarioId { get; set; }

    /// <summary>Valor del encabezado <c>Idempotency-Key</c>.</summary>
    public required string Clave { get; set; }

    /// <summary>Ruta que se llamó, para no reutilizar una clave entre operaciones distintas.</summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// SHA-256 del cuerpo. La misma clave con un cuerpo distinto es un error del cliente y
    /// se rechaza; sin esto, un cobro podría devolver la respuesta de otro.
    /// </summary>
    public required string HashPeticion { get; set; }

    public int CodigoEstado { get; set; }

    public required string RespuestaJson { get; set; }

    public DateTime CreadoUtc { get; set; }

    /// <summary>Momento a partir del cual la purga puede borrarla.</summary>
    public DateTime ExpiraUtc { get; set; }
}
