namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Contexto de tenencia con valores fijos, para lo que corre fuera de una petición HTTP:
/// procesos en segundo plano, herramientas de línea de comandos y pruebas.
/// <para>
/// No es un doble de prueba: es la implementación legítima para código sin
/// <c>HttpContext</c>. Quien lo construye declara explícitamente en qué empresa opera, que
/// es justo lo que se quiere de un proceso de fondo.
/// </para>
/// </summary>
public sealed class ContextoEmpresaFijo(
    Guid? empresa = null,
    Guid? cuenta = null,
    Guid? usuario = null,
    IReadOnlySet<string>? permisos = null) : IContextoEmpresaInterno
{
    private readonly IReadOnlySet<string> _permisos = permisos ?? new HashSet<string>();

    /// <summary>Sin empresa: lo usan la purga y las herramientas de migración.</summary>
    public static ContextoEmpresaFijo SinEmpresa { get; } = new();

    public Guid? EmpresaActual { get; } = empresa;

    public Guid? CuentaActual { get; } = cuenta;

    public Guid? UsuarioActual { get; } = usuario;

    public bool HayEmpresa => EmpresaActual is not null;

    public Guid EmpresaId => EmpresaActual
        ?? throw new InvalidOperationException("Este contexto no tiene empresa activa.");

    public Guid UsuarioId => UsuarioActual
        ?? throw new InvalidOperationException("Este contexto no tiene usuario.");

    public bool Tiene(string permiso) => _permisos.Contains(permiso);
}
