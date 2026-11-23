using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Seguridad;

internal static class RecuperacionDeContrasena
{
    internal const string SolicitudDeCliente = "cliente-solicitar-recuperacion";
    internal const string ConfirmacionDeCliente = "cliente-confirmar-recuperacion";
    internal const string SolicitudDeOperador = "operador-solicitar-recuperacion";
    internal const string ConfirmacionDeOperador = "operador-confirmar-recuperacion";

    internal static void AgregarLimitesDeRecuperacion(this IServiceCollection servicios)
    {
        servicios.AddRateLimiter(opciones =>
        {
            opciones.AddPolicy(SolicitudDeCliente, contexto => PorIp(contexto, 5));
            opciones.AddPolicy(ConfirmacionDeCliente, contexto => PorIp(contexto, 10));
            opciones.AddPolicy(SolicitudDeOperador, contexto => PorIp(contexto, 5));
            opciones.AddPolicy(ConfirmacionDeOperador, contexto => PorIp(contexto, 10));
            opciones.OnRejected = (rechazo, _) => new ValueTask(ResultadosDeError.EscribirProblema(
                rechazo.HttpContext, 429, "recuperacion-limite", "Espera un momento",
                "Se alcanzó el límite de intentos. Vuelve a probar en un minuto."));
        });
    }

    internal static async Task<IResult> SolicitarAsync(
        string? correo, ColaDeCodigosDeRecuperacion cola, HttpContext contexto, CancellationToken ct)
    {
        if (NormalizarCorreo(correo) is not { } normalizado)
            return ErrorNegocio.Validacion("correo-invalido", "Escribe un correo válido.").AResultado(contexto);

        var resultado = await cola.SolicitarAsync(normalizado, ct);
        return resultado.EsExito ? Results.NoContent() : resultado.Error!.AResultado(contexto);
    }

    internal static string? NormalizarCorreo(string? correo)
        => correo?.Trim() is { Length: > 0 and <= 254 } valor && new EmailAddressAttribute().IsValid(valor)
            ? valor.ToUpperInvariant()
            : null;

    internal static ErrorNegocio CodigoInvalido()
        => ErrorNegocio.Validacion("codigo-invalido", "El código no es válido o ya caducó. Revisa tu correo o solicita otro código.");

    private static RateLimitPartition<string> PorIp(HttpContext contexto, int intentos)
        => RateLimitPartition.GetFixedWindowLimiter(contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = intentos, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
