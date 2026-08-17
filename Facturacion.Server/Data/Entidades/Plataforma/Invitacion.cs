namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Invitación a un correo que todavía no tiene usuario en la cuenta. Token de un solo uso,
/// vigente 72 horas (PROMPT-FASES-A §8).
/// <para>
/// Cuando el correo invitado ya pertenece a un usuario <b>de la misma cuenta</b> —el caso de
/// "administrador en la llantera, auxiliar en la cementera"— no se crea ninguna invitación:
/// <see cref="Usuarios.ServicioDeInvitaciones"/> le da acceso directo a la empresa nueva, sin
/// contraseña que fijar porque ya tiene una. Ver ese servicio para el porqué.
/// </para>
/// </summary>
public sealed class Invitacion : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public required string Correo { get; set; }

    public required string Nombre { get; set; }

    /// <summary>SHA-256 del token en base64, igual que los refresh tokens: el valor en claro nunca se guarda.</summary>
    public required string HashToken { get; set; }

    /// <summary>
    /// Los permisos que tendrá al aceptar, separados por coma. No es una tabla hija porque
    /// todavía no hay <c>UsuarioId</c> al que colgarla: se normaliza a
    /// <see cref="Data.Entidades.Plataforma.UsuarioEmpresaPermiso"/> hasta que se acepta.
    /// </summary>
    public required string PermisosClaves { get; set; }

    public DateTime CreadaUtc { get; set; }

    public DateTime ExpiraUtc { get; set; }

    public DateTime? AceptadaUtc { get; set; }

    public DateTime? RevocadaUtc { get; set; }

    public Guid CreadaPorUsuarioId { get; set; }
}
