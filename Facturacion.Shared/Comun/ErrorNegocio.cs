namespace Facturacion.Shared.Comun;

/// <summary>
/// Error de negocio esperado. Viaja dentro de un <see cref="Resultado{T}"/>, no como excepción:
/// que un RFC esté mal escrito o que no queden timbres no es una condición excepcional.
/// </summary>
/// <param name="Tipo">Clase de error; determina el código HTTP.</param>
/// <param name="Codigo">Identificador estable para el cliente, en kebab-case.</param>
/// <param name="Mensaje">Texto para el usuario, en español y entendible por un contador.</param>
/// <param name="Errores">Errores por campo, cuando el tipo es <see cref="TipoErrorNegocio.Validacion"/>.</param>
public sealed record ErrorNegocio(
    TipoErrorNegocio Tipo,
    string Codigo,
    string Mensaje,
    IReadOnlyDictionary<string, string[]>? Errores = null)
{
    public static ErrorNegocio Validacion(string codigo, string mensaje,
        IReadOnlyDictionary<string, string[]>? errores = null)
        => new(TipoErrorNegocio.Validacion, codigo, mensaje, errores);

    public static ErrorNegocio NoEncontrado(string codigo, string mensaje)
        => new(TipoErrorNegocio.NoEncontrado, codigo, mensaje);

    public static ErrorNegocio Conflicto(string codigo, string mensaje)
        => new(TipoErrorNegocio.Conflicto, codigo, mensaje);

    public static ErrorNegocio SinPermiso(string codigo, string mensaje)
        => new(TipoErrorNegocio.SinPermiso, codigo, mensaje);

    public static ErrorNegocio Regla(string codigo, string mensaje)
        => new(TipoErrorNegocio.ReglaDeNegocio, codigo, mensaje);

    public static ErrorNegocio LimiteExcedido(string codigo, string mensaje)
        => new(TipoErrorNegocio.LimiteExcedido, codigo, mensaje);
}
