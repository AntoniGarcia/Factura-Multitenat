namespace Facturacion.Server.Data;

/// <summary>
/// Marca una entidad cuyos renglones son de la cuenta y los comparten todas sus empresas
/// (AGENTS.md §11). Igual que <see cref="IEntidadDeEmpresa"/>, queda cubierta por el filtro
/// global de consulta —por la cuenta del claim— y por el sellado de <c>CuentaId</c> al
/// guardar, y no se filtra a mano en un servicio ni en un endpoint.
/// <para>
/// Una entidad implementa una de las dos, nunca ambas. Si además conserva la empresa que la
/// originó, esa <c>EmpresaId</c> es un dato, no tenencia.
/// </para>
/// </summary>
public interface IEntidadDeCuenta
{
    Guid CuentaId { get; set; }
}
