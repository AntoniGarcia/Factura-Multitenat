namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Una moneda de <c>c_Moneda</c> que la empresa ofrece al capturar facturas y pagos. El peso
/// mexicano no se guarda: siempre está. Quitar una la desactiva, no la borra (AGENTS.md §5).
/// </summary>
public sealed class MonedaDeEmpresa : IEntidadDeEmpresa
{
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public required string Moneda { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime ModificadaUtc { get; set; }
}
