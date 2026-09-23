namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Perfil permanente del notario de una empresa. Se copia al comprobante al timbrar para que
/// cambiar esta configuración nunca reescriba una operación notarial ya emitida.
/// </summary>
public sealed class ConfiguracionNotario : IEntidadDeEmpresa
{
    public Guid EmpresaId { get; set; }
    public required string Curp { get; set; }
    public int NumeroNotaria { get; set; }
    public required string Estado { get; set; }
    public string? Adscripcion { get; set; }
    public DateTime ModificadoUtc { get; set; }
}
