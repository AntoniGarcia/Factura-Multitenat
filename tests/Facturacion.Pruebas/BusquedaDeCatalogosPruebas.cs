using System.Reflection;
using Facturacion.Client.Componentes.Comunes;
using Facturacion.Shared.Contratos;
using Microsoft.AspNetCore.Components;

namespace Facturacion.Pruebas;

public sealed class BusquedaDeCatalogosPruebas
{
    [Fact]
    public async Task Admite_parametros_de_bind_Clave_y_notifica_la_seleccion_y_limpieza()
    {
        var componente = new BuscadorCatalogo();
        string? seleccion = null;
        var opciones = new ClaveSatDto[] { new("c_FormaPago", "03", "Transferencia", true),
            new("c_FormaPago", "01", "Efectivo", true) };
        var parametros = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Catalogo"] = "c_FormaPago", ["Opciones"] = opciones, ["Clave"] = "03",
            ["ClaveChanged"] = EventCallback.Factory.Create<string>(new object(), (string valor) => seleccion = valor)
        });
        // Es la misma asignación de parámetros que realiza Blazor al abrir el formulario.
        parametros.SetParameterProperties(componente);
        var actual = typeof(BuscadorCatalogo).GetProperty("ValorActual", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(opciones[0], actual.GetValue(componente));
        var elegir = typeof(BuscadorCatalogo).GetMethod("AlElegirAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)elegir.Invoke(componente, [opciones[1]])!;
        Assert.Equal("01", seleccion);
        await (Task)elegir.Invoke(componente, [null])!;
        Assert.Equal("", seleccion);
    }

    [Theory]
    [InlineData("601", "601")]
    [InlineData("general", "601")]
    [InlineData("FISICAS", "612")]
    [InlineData("  físicas  ", "612")]
    [InlineData("6", "601,612")]
    [InlineData("", "601,612")]
    [InlineData("no existe", "")]
    public async Task Filtra_opciones_por_clave_descripcion_y_acentos(string texto, string claves)
    {
        var componente = new BuscadorCatalogo();
        typeof(BuscadorCatalogo).GetProperty(nameof(BuscadorCatalogo.Opciones))!.SetValue(componente,
            new ClaveSatDto[] { new("c_RegimenFiscal", "601", "General de ley", true),
                new("c_RegimenFiscal", "612", "Personas físicas con actividades", true) });
        var tarea = (Task<IEnumerable<ClaveSatDto>>)typeof(BuscadorCatalogo)
            .GetMethod("Buscar", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(componente, [texto, CancellationToken.None])!;
        Assert.Equal(claves, string.Join(',', (await tarea).Select(x => x.Clave)));
    }
}
