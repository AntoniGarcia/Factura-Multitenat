using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Infra.Integracion;

/// <summary>
/// Comprueba al arrancar el estado de la frontera entre las dos mitades
/// (<c>Facturacion.Shared.Contratos</c>).
///
/// <para><b>Las dos cosas que mira, y por qué solo una es fatal</b></para>
/// Un <b>doble registrado fuera de <c>Development</c></b> tumba el arranque: es un fallo
/// silencioso: el sistema responde, las pantallas se pintan y los datos son inventados.
/// Vale mucho más un servicio que no levanta que uno que factura contra un doble.
///
/// <para>
/// Un <b>contrato sin implementación</b> solo se registra en el log. La mitad A tiene que
/// poder desplegarse antes de que la mitad B exista, y hoy <c>IResumenDocumentos</c> es
/// justamente eso. Quien resuelva un contrato ausente recibe <c>null</c> y decide qué hacer;
/// el tablero, por ejemplo, omite el recuadro en vez de inventar cifras.
/// </para>
/// </summary>
public static class VerificacionDeContratos
{
    private static readonly string[] IndiciosEnElNombre =
        ["Doble", "Falso", "Fake", "Stub", "Simulado"];

    public static WebApplication VerificarContratos(this WebApplication aplicacion)
    {
        var contratos = typeof(IResumenDocumentos).Assembly
            .GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(IResumenDocumentos).Namespace)
            .OrderBy(t => t.Name)
            .ToArray();

        using var ambito = aplicacion.Services.CreateScope();
        var registro = ambito.ServiceProvider.GetRequiredService<ILogger<Program>>();

        List<string> dobles = [];
        List<string> ausentes = [];

        foreach (var contrato in contratos)
        {
            // Se resuelve en vez de leer el ServiceCollection porque varios contratos se
            // registran con fábrica (sp => ...), y ahí el descriptor no dice qué tipo
            // concreto sale. El tipo real solo se conoce resolviendo.
            var implementacion = ambito.ServiceProvider.GetService(contrato)?.GetType();

            if (implementacion is null)
            {
                ausentes.Add(contrato.Name);
                continue;
            }

            if (EsDoble(implementacion))
                dobles.Add($"{contrato.Name} -> {implementacion.FullName}");
        }

        if (dobles.Count > 0 && !aplicacion.Environment.IsDevelopment())
            throw new InvalidOperationException(
                $"Hay dobles de prueba registrados en el entorno '{aplicacion.Environment.EnvironmentName}': " +
                string.Join("; ", dobles) +
                ". Un doble fuera de Development devuelve datos inventados sin avisar. " +
                "Registra la implementación real o no despliegues.");

        foreach (var doble in dobles)
            registro.LogWarning("Contrato servido por un doble de prueba: {Doble}", doble);

        if (ausentes.Count > 0)
            registro.LogWarning(
                "Contratos sin implementación registrada: {Ausentes}. Quien los resuelva recibe null.",
                string.Join(", ", ausentes));

        registro.LogInformation(
            "Contratos verificados: {Total} en total, {Ausentes} sin implementación, {Dobles} servidos por un doble.",
            contratos.Length, ausentes.Count, dobles.Count);

        return aplicacion;
    }

    private static bool EsDoble(Type implementacion)
        => implementacion.GetCustomAttributes(typeof(DobleDePruebaAttribute), inherit: false).Length > 0
           || IndiciosEnElNombre.Any(indicio =>
               implementacion.Name.Contains(indicio, StringComparison.OrdinalIgnoreCase));
}
