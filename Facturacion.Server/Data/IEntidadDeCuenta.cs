namespace Facturacion.Server.Data;

/// Renglones de la cuenta que comparten sus empresas: filtro global y sellado
/// por la cuenta del claim. Una entidad implementa esta o IEntidadDeEmpresa, nunca ambas; si
/// además guarda la empresa que la originó, esa EmpresaId es un dato, no tenencia.
public interface IEntidadDeCuenta
{
    Guid CuentaId { get; set; }
}
