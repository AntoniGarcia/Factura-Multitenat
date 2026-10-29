namespace Facturacion.Shared.Contratos;

/// <summary>
/// Lo que la mitad A le pregunta a la mitad B antes de borrar un registro de catálogo: si algún
/// comprobante de la cuenta lo usa —de cualquiera de sus empresas para clientes y productos,
/// de la empresa activa para series—, borradores incluidos. Solo lectura.
/// <para>
/// Se añadió después de congelar el contrato, para el permiso Eliminar de los catálogos
/// (AGENTS.md §11). El comprobante guarda la foto del cliente, del producto y de la serie, pero
/// también su identificador, y ninguna llave foránea impide borrarlos: sin preguntar, un
/// borrador quedaría apuntando a algo que ya no existe.
/// </para>
/// </summary>
public interface IUsoDeCatalogos
{
    Task<bool> ClienteUsadoAsync(Guid clienteId, CancellationToken ct);

    Task<bool> ProductoUsadoAsync(Guid productoId, CancellationToken ct);

    Task<bool> SerieUsadaAsync(Guid serieId, CancellationToken ct);
}
