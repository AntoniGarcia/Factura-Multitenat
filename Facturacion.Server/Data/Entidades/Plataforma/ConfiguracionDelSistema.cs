namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Configuración única del SaaS. No pertenece a una empresa y, por tanto, queda fuera del
/// filtro de tenencia. La contraseña SMTP se guarda protegida; nunca vuelve al navegador.
/// </summary>
public sealed class ConfiguracionDelSistema
{
    public byte Id { get; set; }

    public required string ServidorSmtp { get; set; }

    public int PuertoSmtp { get; set; }

    public required string UsuarioSmtp { get; set; }

    public string? ContrasenaSmtpCifrada { get; set; }

    public required string RemitenteCorreo { get; set; }

    public required string RemitenteNombre { get; set; }

    public bool UsarTls { get; set; }

    public required string NombreDelSistema { get; set; }

    public required string AsuntoVerificacion { get; set; }

    public required string CuerpoVerificacion { get; set; }

    public required string AsuntoContrasena { get; set; }

    public required string CuerpoContrasena { get; set; }

    public DateTime ActualizadaUtc { get; set; }

    public Guid ActualizadaPorOperadorId { get; set; }
}
