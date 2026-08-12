using Serilog.Core;
using Serilog.Events;

namespace Facturacion.Server.Infra.Registro;

/// <summary>
/// Enmascara las propiedades estructuradas de cada evento de registro: RFC, contraseñas,
/// tokens y contenido de certificados no llegan al log (CLAUDE.md §4).
/// <para>
/// Recorre también estructuras, listas y diccionarios, porque un objeto registrado con
/// <c>{@Peticion}</c> esconde sus campos sensibles un nivel más abajo.
/// </para>
/// <para>
/// <b>Lo que este enriquecedor no puede ver:</b> un valor incrustado en el texto de la
/// plantilla, como en <c>_registro.LogInformation($"RFC {rfc}")</c>. Ahí el RFC deja de ser
/// una propiedad y pasa a ser parte del mensaje. De eso se encarga <see cref="SinkEnmascarado"/>.
/// </para>
/// </summary>
public sealed class EnriquecedorDatosSensibles : ILogEventEnricher
{
    public void Enrich(LogEvent evento, ILogEventPropertyFactory fabrica)
    {
        foreach (var propiedad in evento.Properties.ToList())
        {
            var enmascarado = Enmascarar(propiedad.Key, propiedad.Value);

            if (!ReferenceEquals(enmascarado, propiedad.Value))
                evento.AddOrUpdateProperty(new LogEventProperty(propiedad.Key, enmascarado));
        }
    }

    private static LogEventPropertyValue Enmascarar(string nombre, LogEventPropertyValue valor)
    {
        switch (valor)
        {
            case ScalarValue { Value: string texto }:
                if (EnmascaradorDeTextoSensible.EsNombreSensible(nombre))
                    return new ScalarValue(EnmascaradorDeTextoSensible.ValorOmitido);

                var limpio = EnmascaradorDeTextoSensible.Enmascarar(texto);
                return limpio == texto ? valor : new ScalarValue(limpio);

            case ScalarValue when EnmascaradorDeTextoSensible.EsNombreSensible(nombre):
                return new ScalarValue(EnmascaradorDeTextoSensible.ValorOmitido);

            case StructureValue estructura:
                var propiedades = estructura.Properties
                    .Select(p => new LogEventProperty(p.Name, Enmascarar(p.Name, p.Value)))
                    .ToList();
                return new StructureValue(propiedades, estructura.TypeTag);

            case SequenceValue secuencia:
                return new SequenceValue(secuencia.Elements.Select(e => Enmascarar(nombre, e)).ToList());

            case DictionaryValue diccionario:
                var pares = diccionario.Elements
                    .Select(p => KeyValuePair.Create(
                        p.Key,
                        Enmascarar(p.Key.Value as string ?? nombre, p.Value)))
                    .ToList();
                return new DictionaryValue(pares);

            default:
                return valor;
        }
    }
}
