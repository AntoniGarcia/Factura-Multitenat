namespace Facturacion.Server.Data.Entidades.Plataforma;

// El peso mexicano no se guarda: siempre está. Quitar una la desactiva.
public sealed class MonedaDeEmpresa : IEntidadDeEmpresa
{
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public required string Moneda { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime ModificadaUtc { get; set; }
}
