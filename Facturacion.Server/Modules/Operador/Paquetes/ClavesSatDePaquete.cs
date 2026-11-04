using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Operador.Paquetes;

public sealed class ClavesSatDePaquete(IServicioCatalogosSat catalogos)
{
    public async Task<ErrorNegocio?> ValidarAsync(string? producto, string? unidad, CancellationToken ct)
    {
        producto = ClavesSatPredeterminadasDePaquetes.Producto(producto);
        unidad = ClavesSatPredeterminadasDePaquetes.Unidad(unidad);
        if (producto is not { Length: 8 } || !producto.All(char.IsAsciiDigit) || unidad is not { Length: > 0 and <= 3 })
            return Invalida();
        if (await catalogos.ResolverAsync("c_ClaveProdServ", producto, ct) is not { Vigente: true } ||
            await catalogos.ResolverAsync("c_ClaveUnidad", unidad, ct) is not { Vigente: true })
            return Invalida();
        return null;
    }

    public async Task<IReadOnlyList<ClaveSatDto>> OpcionesAsync(string catalogo, string? texto, string? clave, CancellationToken ct)
    {
        if (catalogo is not ("c_ClaveProdServ" or "c_ClaveUnidad")) return [];
        if (!string.IsNullOrWhiteSpace(clave) && clave.Length <= 8)
            return await catalogos.ResolverAsync(catalogo, clave, ct) is { } opcion ? [opcion] : [];
        return !string.IsNullOrWhiteSpace(texto) && texto.Trim().Length is >= 3 and <= 100
            ? await catalogos.BuscarAsync(catalogo, texto.Trim(), 50, ct) : [];
    }

    private static ErrorNegocio Invalida() => ErrorNegocio.Validacion("claves-sat-paquete-invalidas",
        "Selecciona una clave de producto o servicio y una unidad SAT vigentes.");
}
