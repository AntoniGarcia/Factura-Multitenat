namespace Facturacion.Shared.Comun;

/// <summary>
/// Resultado de una operación que devuelve un valor, o un <see cref="ErrorNegocio"/>.
/// Es una clase y no una estructura a propósito: <c>default</c> de una estructura se vería
/// como un éxito con valor nulo, y ese estado no debe poder existir.
/// </summary>
public sealed class Resultado<T>
{
    private readonly T? _valor;

    private Resultado(T valor)
    {
        _valor = valor;
        EsExito = true;
    }

    private Resultado(ErrorNegocio error)
    {
        Error = error;
        EsExito = false;
    }

    public bool EsExito { get; }

    public bool EsFallo => !EsExito;

    public ErrorNegocio? Error { get; }

    /// <summary>Valor de la operación. Consultarlo en un resultado fallido es un error de programación.</summary>
    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException("Se leyó el valor de un resultado fallido.");

    public static Resultado<T> Exito(T valor) => new(valor);

    public static Resultado<T> Fallo(ErrorNegocio error) => new(error);

    public static implicit operator Resultado<T>(T valor) => Exito(valor);

    public static implicit operator Resultado<T>(ErrorNegocio error) => Fallo(error);
}
