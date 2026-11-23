using System.Reflection;

namespace Facturacion.Shared.Comun;

/// <summary>
/// Normaliza <c>AssemblyInformationalVersion</c> conservando el identificador de compilación/commit
/// (acortado tras <c>+</c>) para que <c>/api/version</c> detecte automáticamente nuevas publicaciones.
/// </summary>
public static class VersionDeCompilacion
{
    private const int LongitudDeHash = 12;

    /// <summary>
    /// Versión leída del ensamblado compartido (<c>Facturacion.Shared</c>), garantizando que
    /// Server y Client comparen exactamente la misma fuente y no diverjan por tareas de build
    /// específicas del servidor (como NPOI/SourceLink).
    /// </summary>
    public static string Actual { get; } = Formatear(
        typeof(VersionDeCompilacion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);

    public static string Formatear(string? informativa)
    {
        if (string.IsNullOrWhiteSpace(informativa))
            return "desconocida";

        var separador = informativa.IndexOf('+');
        if (separador < 0)
            return informativa.Trim();

        var baseVersion = informativa[..separador].Trim();
        var metadato = informativa[(separador + 1)..].Trim();
        if (metadato.Length == 0)
            return baseVersion;

        var corto = metadato.Length <= LongitudDeHash ? metadato : metadato[..LongitudDeHash];
        return $"{baseVersion}+{corto}";
    }
}
