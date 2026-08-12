namespace Facturacion.Shared.Comun;

/// <summary>
/// Resultado de una operación que no devuelve valor. Propaga el error de negocio sin excepción.
/// </summary>
public sealed class Resultado
{
    private Resultado() => EsExito = true;

    private Resultado(ErrorNegocio error)
    {
        EsExito = false;
        Error = error;
    }

    public bool EsExito { get; }

    public bool EsFallo => !EsExito;

    public ErrorNegocio? Error { get; }

    public static Resultado Exito() => new();

    public static Resultado Fallo(ErrorNegocio error) => new(error);

    public static implicit operator Resultado(ErrorNegocio error) => Fallo(error);
}
