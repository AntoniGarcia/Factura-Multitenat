using System.Security.Claims;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Quién es el operador del SaaS en la petición en curso, si es que la hace uno.
/// <para>
/// Es el equivalente de <see cref="IContextoEmpresaInterno"/> para la otra identidad, y se
/// mantiene aparte por lo mismo que las tablas: mezclarlas dejaría que un descuido tratara a
/// un inquilino como operador. Aquí no hay tenencia que resolver —el operador no pertenece a
/// ninguna empresa—, así que solo responde quién es.
/// </para>
/// </summary>
public interface IContextoDeOperador
{
    /// <summary>Nulo en toda petición que no venga del panel de operador.</summary>
    Guid? OperadorId { get; }

    bool EsOperador { get; }
}

public sealed class ContextoDeOperadorHttp(IHttpContextAccessor accesor) : IContextoDeOperador
{
    private ClaimsPrincipal? Usuario => accesor.HttpContext?.User;

    public Guid? OperadorId
        => Guid.TryParse(Usuario?.FindFirstValue(ClavesDeClaim.Operador), out var valor) ? valor : null;

    public bool EsOperador => OperadorId is not null;
}

/// <summary>
/// Contexto de operador para código que corre fuera de una petición: procesos de fondo,
/// comandos de consola y pruebas. Equivale a <c>ContextoEmpresaFijo.SinEmpresa</c>.
/// </summary>
public sealed class ContextoDeOperadorFijo(Guid? operadorId) : IContextoDeOperador
{
    public static ContextoDeOperadorFijo SinOperador { get; } = new(null);

    public Guid? OperadorId { get; } = operadorId;

    public bool EsOperador => OperadorId is not null;
}
