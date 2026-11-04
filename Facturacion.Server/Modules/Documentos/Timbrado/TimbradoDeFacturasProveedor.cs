using System.Security.Cryptography;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Documentos.Dobles;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

public sealed class TimbradoDeFacturasProveedor(
    IEmisionDeFacturasDeCompras emision, XmlDeFacturaProveedor xml,
    IHostEnvironment entorno, ILogger<TimbradoDeFacturasProveedor> registro, IProveedorPac? pac = null)
{
    public async Task<Resultado<Guid>> TimbrarAsync(Guid compra, byte[] version, string clave, CancellationToken ct)
    {
        // Esta fase solo se ejecuta con el doble de desarrollo, aunque exista un PAC real registrado.
        if (!entorno.IsDevelopment() || pac is not DobleProveedorPac)
            return ErrorNegocio.Regla("simulacion-no-disponible", "El timbrado de compras está disponible en la simulación local.");
        var preparado = await emision.PrepararAsync(compra, version, clave, ct);
        if (preparado.EsFallo) return preparado.Error!;
        var i = preparado.Valor;
        if (!i.IniciadoAhora) return i.Id;
        // Una desconexión del navegador no cancela el cierre de una operación ya apartada.
        using var plazo = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var proceso = plazo.Token;
        XmlFacturaProveedorDto? sellado = null;
        try
        {
            var csd = await emision.ObtenerCsdAsync(i.Id, i.Control, proceso);
            if (csd.EsFallo)
            {
                await emision.ResolverAsync(i.Id, i.Control, new(ResultadoFacturaProveedor.ErrorDePreparacion, csd.Error!.Codigo), proceso);
                return i.Id;
            }
            try
            {
                var generado = xml.Generar(i, csd.Valor);
                if (generado.EsFallo)
                {
                    await emision.ResolverAsync(i.Id, i.Control, new(ResultadoFacturaProveedor.ErrorDePreparacion, generado.Error!.Codigo), proceso);
                    return i.Id;
                }
                sellado = generado.Valor;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(csd.Valor.CertificadoCer);
                CryptographicOperations.ZeroMemory(csd.Valor.LlavePrivadaKey);
            }
            var guardado = await emision.GuardarXmlAsync(i.Id, i.Control, sellado, proceso);
            if (guardado.EsFallo) return i.Id;
        }
        catch (Exception ex)
        {
            registro.LogWarning("No se pudo preparar el intento {Intento}; tipo {Tipo}.", i.Id, ex.GetType().Name);
            // Si el guardado llegó a confirmarse, el contrato rechaza revertirlo y lo deja para consulta.
            using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await emision.ResolverAsync(i.Id, i.Control,
                new(ResultadoFacturaProveedor.ErrorDePreparacion, "error-al-preparar"), cierre.Token);
            return i.Id;
        }
        try
        {
            var respuesta = await pac.TimbrarAsync(sellado.Xml, i.ClavePac, proceso);
            await AplicarAsync(i with { Xml = sellado }, respuesta, proceso);
        }
        catch (Exception ex)
        {
            registro.LogWarning("El intento {Intento} queda pendiente de consulta; tipo {Tipo}.", i.Id, ex.GetType().Name);
        }
        return i.Id;
    }

    public async Task ConciliarAsync(CancellationToken ct)
    {
        if (!entorno.IsDevelopment() || pac is not DobleProveedorPac) return;
        var pendientes = await emision.ObtenerPendientesAsync(DateTime.UtcNow.AddMinutes(-30), 100, ct);
        foreach (var i in pendientes)
        {
            try
            {
                if (i.Xml is null)
                    await emision.ResolverAsync(i.Id, i.Control, new(ResultadoFacturaProveedor.ErrorDePreparacion, "preparacion-interrumpida"), ct);
                else
                {
                    var respuesta = await pac.ConsultarAsync(i.Xml.Xml, i.ClavePac, ct);
                    // Solo el simulador confirma inequívocamente que no lo recibió. Se conserva el mismo XML y clave.
                    if (respuesta.Resultado == ResultadoDePac.NoEncontrado)
                        respuesta = await pac.TimbrarAsync(i.Xml.Xml, i.ClavePac, ct);
                    await AplicarAsync(i, respuesta, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { registro.LogWarning("No se pudo conciliar {Intento}; tipo {Tipo}.", i.Id, ex.GetType().Name); }
        }
    }

    private async Task AplicarAsync(IntentoFacturaCompraDto i, RespuestaDePac respuesta, CancellationToken ct)
    {
        if (respuesta.Resultado == ResultadoDePac.Timbrado && i.Xml is not null && XmlDeFacturaProveedor.CoincideTimbre(i.Xml, respuesta))
            await emision.ResolverAsync(i.Id, i.Control, new(ResultadoFacturaProveedor.Timbrado,
                Uuid: respuesta.Uuid, FechaTimbradoUtc: respuesta.FechaTimbradoUtc, XmlTimbrado: respuesta.XmlTimbrado,
                NoCertificadoSat: respuesta.NoCertificadoSat, SelloSat: respuesta.SelloSat, CadenaOriginalSat: respuesta.CadenaOriginalSat), ct);
        else if (respuesta.Resultado == ResultadoDePac.Rechazado)
            await emision.ResolverAsync(i.Id, i.Control, new(ResultadoFacturaProveedor.Rechazado, "rechazo-del-proveedor"), ct);
        // Una respuesta incompleta, alterada o incierta se consulta después; no habilita una segunda emisión.
    }
}

public sealed class ConciliacionDeFacturasProveedor(
    IServiceScopeFactory ambitos, ILogger<ConciliacionDeFacturasProveedor> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var ambito = ambitos.CreateScope();
                await ambito.ServiceProvider.GetRequiredService<TimbradoDeFacturasProveedor>().ConciliarAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { registro.LogWarning("No se pudo revisar las facturas de compras pendientes; tipo {Tipo}.", ex.GetType().Name); }
        }
    }
}

public static class TimbradoDeFacturasProveedorEndpoints
{
    public static void MapTimbradoDeFacturasProveedor(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/operador/compras/{id:guid}/factura/timbrar", async (
            Guid id, PeticionTimbrarFacturaCompra p, HttpContext http, TimbradoDeFacturasProveedor servicio, CancellationToken ct) =>
        {
            var claves = http.Request.Headers["Idempotency-Key"];
            if (claves.Count != 1 || string.IsNullOrWhiteSpace(claves[0]))
                return ErrorNegocio.Validacion("idempotency-key-requerida", "Falta la clave de la solicitud.").AResultado(http);
            var r = await servicio.TimbrarAsync(id, p.Version, claves[0]!, ct);
            // La aceptación es estable aunque la conciliación termine después. El resultado fiscal se consulta por GET.
            return r.EsFallo ? r.Error!.AResultado(http) : Results.Json(new SolicitudFacturaCompraDto(r.Valor), statusCode: 202);
        }).RequireAuthorization(PoliticasDeOperador.Operador, PoliticasDeOperador.TimbrarCompras)
            .WithTags("Operador · Facturas de compras");
    }
}
