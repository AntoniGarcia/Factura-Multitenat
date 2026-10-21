using System.ComponentModel.DataAnnotations;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>Ubicación de los archivos y protección del llavero de cifrado.</summary>
public sealed class OpcionesDeAlmacen
{
    public const string Seccion = "Almacen";

    /// <summary>
    /// Carpeta raíz de los archivos cifrados. <b>Fuera de <c>wwwroot</c></b>: nada aquí
    /// dentro se sirve como archivo estático, todo pasa por un endpoint autorizado.
    /// En Development una ruta relativa se resuelve contra la raíz de contenido. Fuera de
    /// Development debe configurarse una ruta absoluta y persistente.
    /// </summary>
    public string Raiz { get; init; } = "almacenamiento";

    /// <summary>
    /// Carpeta del llavero de Data Protection. También fuera de <c>wwwroot</c>; fuera de
    /// Development debe ser absoluta y persistente.
    /// </summary>
    public string RutaLlavero { get; init; } = "almacenamiento/llavero";

    /// <summary>Certificado PFX en base 64 que protege el llavero. Se genera con --generar-llave-maestra y se guarda fuera del repositorio. No incluye contraseña porque se almacena junto a la configuración protegida.</summary>
    [Required]
    public string LlaveMaestraPfx { get; init; } = string.Empty;
}
