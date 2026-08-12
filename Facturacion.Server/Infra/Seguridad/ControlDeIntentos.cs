using Microsoft.Extensions.Caching.Memory;

namespace Facturacion.Server.Infra.Seguridad;

/// <summary>
/// Límite de intentos con bloqueo creciente, por clave arbitraria. Se usa por dirección IP,
/// que es lo que Identity no cubre: su bloqueo es por cuenta, así que sin esto alguien puede
/// probar una contraseña en mil cuentas distintas sin bloquear ninguna.
/// </summary>
public interface IControlDeIntentos
{
    /// <summary>Tiempo que falta para poder volver a intentar, o <c>null</c> si no hay bloqueo.</summary>
    TimeSpan? BloqueoRestante(string clave);

    void RegistrarFallo(string clave);

    /// <summary>Borra el historial de la clave. Se llama tras un acceso correcto.</summary>
    void Limpiar(string clave);
}

/// <summary>
/// Implementación en memoria.
/// <para>
/// El estado es por instancia y se pierde al reiniciar. Es aceptable para el MVP —el
/// bloqueo por cuenta sí es persistente y es el que protege una cuenta concreta—, pero con
/// varias instancias detrás de un balanceador el límite por IP se multiplica por el número
/// de instancias. Cuando eso ocurra, hay que moverlo a un almacén compartido.
/// </para>
/// </summary>
public sealed class ControlDeIntentos(IMemoryCache cache) : IControlDeIntentos
{
    private sealed class Historial
    {
        public int Fallos { get; set; }
        public DateTime? BloqueadoHastaUtc { get; set; }
    }

    private static readonly TimeSpan Memoria = TimeSpan.FromHours(2);

    // A más reincidencia, más castigo. El primer tramo es corto a propósito: castiga al
    // robot sin arruinarle la mañana a quien de verdad se equivocó de contraseña.
    private static readonly (int Fallos, TimeSpan Bloqueo)[] Escala =
    [
        (20, TimeSpan.FromHours(1)),
        (15, TimeSpan.FromMinutes(15)),
        (10, TimeSpan.FromMinutes(5)),
        (5, TimeSpan.FromMinutes(1))
    ];

    public TimeSpan? BloqueoRestante(string clave)
    {
        if (!cache.TryGetValue<Historial>(Clave(clave), out var historial) || historial is null)
            return null;

        if (historial.BloqueadoHastaUtc is not { } hasta)
            return null;

        var restante = hasta - DateTime.UtcNow;
        return restante > TimeSpan.Zero ? restante : null;
    }

    public void RegistrarFallo(string clave)
    {
        var historial = cache.GetOrCreate(Clave(clave), entrada =>
        {
            entrada.SlidingExpiration = Memoria;
            return new Historial();
        })!;

        historial.Fallos++;

        foreach (var (fallos, bloqueo) in Escala)
        {
            if (historial.Fallos < fallos) continue;

            historial.BloqueadoHastaUtc = DateTime.UtcNow.Add(bloqueo);
            break;
        }
    }

    public void Limpiar(string clave) => cache.Remove(Clave(clave));

    private static string Clave(string clave) => $"intentos:{clave}";
}
