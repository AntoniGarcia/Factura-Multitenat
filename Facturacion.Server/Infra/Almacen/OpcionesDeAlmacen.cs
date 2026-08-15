using System.ComponentModel.DataAnnotations;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>
/// Dónde viven los archivos de las empresas y con qué se protege el llavero que los cifra
/// (CLAUDE.md §4).
/// </summary>
public sealed class OpcionesDeAlmacen
{
    public const string Seccion = "Almacen";

    /// <summary>
    /// Carpeta raíz de los archivos cifrados. <b>Fuera de <c>wwwroot</c></b>: nada aquí
    /// dentro se sirve como archivo estático, todo pasa por un endpoint autorizado.
    /// Relativa se resuelve contra la raíz de contenido de la aplicación.
    /// </summary>
    public string Raiz { get; init; } = "almacenamiento";

    /// <summary>Carpeta del llavero de Data Protection. También fuera de <c>wwwroot</c>.</summary>
    public string RutaLlavero { get; init; } = "almacenamiento/llavero";

    /// <summary>
    /// Certificado que protege el llavero, como PFX en base 64 y sin contraseña.
    /// <para>
    /// Es la clave maestra de CLAUDE.md §4: sin ella, el llavero queda en claro y quien se
    /// lleve la carpeta se lleva también la capacidad de descifrar los CSD. Se genera con
    /// <c>dotnet run -- --generar-llave-maestra</c> y se guarda en configuración o en una
    /// variable de entorno; <b>nunca en el repositorio</b>.
    /// </para>
    /// <para>
    /// No lleva contraseña a propósito: ponerle una y guardarla en el mismo archivo de
    /// configuración no protegería de nada, solo daría la impresión de hacerlo.
    /// </para>
    /// </summary>
    [Required]
    public string LlaveMaestraPfx { get; init; } = string.Empty;
}
