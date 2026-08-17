namespace Facturacion.Shared.Comun;

/// <summary>
/// Los seis permisos del sistema. La autorización es por permiso, nunca por rol (CLAUDE.md §4).
/// </summary>
public enum Permiso
{
    Timbrar,
    Cancelar,
    AdministrarUsuarios,
    ComprarTimbres,
    VerReportes,
    ConfigurarEmpresa
}

/// <summary>
/// Las claves textuales de los permisos, tal como viajan en el token y como se guardan
/// en la base. Existen para que nadie escriba la cadena a mano en una política o en un claim.
/// </summary>
public static class Permisos
{
    public const string Timbrar = "timbrar";
    public const string Cancelar = "cancelar";
    public const string AdministrarUsuarios = "administrar_usuarios";
    public const string ComprarTimbres = "comprar_timbres";
    public const string VerReportes = "ver_reportes";
    public const string ConfigurarEmpresa = "configurar_empresa";

    private static readonly Dictionary<Permiso, string> ACadenas = new()
    {
        [Permiso.Timbrar] = Timbrar,
        [Permiso.Cancelar] = Cancelar,
        [Permiso.AdministrarUsuarios] = AdministrarUsuarios,
        [Permiso.ComprarTimbres] = ComprarTimbres,
        [Permiso.VerReportes] = VerReportes,
        [Permiso.ConfigurarEmpresa] = ConfigurarEmpresa
    };

    private static readonly Dictionary<string, Permiso> DesdeCadenas =
        ACadenas.ToDictionary(p => p.Value, p => p.Key);

    /// <summary>Las seis claves, en el orden del enum. Es la fuente de la semilla de la tabla.</summary>
    public static IReadOnlyList<string> Todos { get; } = [.. ACadenas.Values];

    public static string ACadena(this Permiso permiso) => ACadenas[permiso];

    public static Permiso Desde(string clave) => DesdeCadenas.TryGetValue(clave, out var p)
        ? p
        : throw new ArgumentOutOfRangeException(nameof(clave), clave, "Permiso desconocido.");

    public static bool EsValido(string clave) => DesdeCadenas.ContainsKey(clave);

    // Etiqueta corta para casillas de captura (fase 8). La descripción larga de la tabla
    // Permisos, sembrada desde aquí mismo, es para quien consulta la base; esta es para
    // quien arma el formulario de invitación y no necesita el Client pidiéndole el catálogo
    // al servidor solo para dibujar seis casillas fijas.
    private static readonly Dictionary<string, string> Etiquetas = new()
    {
        [Timbrar] = "Timbrar",
        [Cancelar] = "Cancelar",
        [AdministrarUsuarios] = "Administrar usuarios",
        [ComprarTimbres] = "Comprar timbres",
        [VerReportes] = "Ver reportes",
        [ConfigurarEmpresa] = "Configurar empresa"
    };

    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}
