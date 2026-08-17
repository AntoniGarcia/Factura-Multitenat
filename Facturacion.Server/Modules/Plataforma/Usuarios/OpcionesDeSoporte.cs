namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Cómo contactar a soporte cuando una empresa llega al límite de usuarios. Todavía no
/// existe un canal de soporte real, así que ambos campos pueden quedar vacíos: el mensaje
/// se ajusta solo cuando alguien los llene en configuración.
/// </summary>
public sealed class OpcionesDeSoporte
{
    public const string Seccion = "Soporte";

    public string? Correo { get; init; }

    public string? Telefono { get; init; }
}
