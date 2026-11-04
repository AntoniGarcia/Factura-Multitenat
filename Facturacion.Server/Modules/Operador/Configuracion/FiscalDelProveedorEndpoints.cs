using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;

namespace Facturacion.Server.Modules.Operador.Configuracion;

public static class FiscalDelProveedorEndpoints
{
    public static void MapFiscalDelProveedor(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/configuracion/facturacion")
            .WithTags("Operador · Facturación").RequireAuthorization(PoliticasDeOperador.Operador);
        grupo.MapGet("/", (ServicioFiscalDelProveedor servicio, CancellationToken ct) => servicio.ObtenerAsync(ct))
            .RequireAuthorization(PoliticasDeOperador.VerConfiguracion);
        grupo.MapGet("/regimenes", (ServicioFiscalDelProveedor servicio, CancellationToken ct) => servicio.RegimenesAsync(ct))
            .RequireAuthorization(PoliticasDeOperador.VerConfiguracion);
        grupo.MapGet("/codigos-postales", async (string? texto, IServicioCatalogosSat catalogos, CancellationToken ct) =>
            string.IsNullOrWhiteSpace(texto) || texto.Trim().Length is < 3 or > 100
                ? Array.Empty<ClaveSatDto>()
                : await catalogos.BuscarAsync("c_CodigoPostal", texto.Trim(), 50, ct))
            .RequireAuthorization(PoliticasDeOperador.VerConfiguracion);
        grupo.MapPut("/", async (PeticionGuardarFiscalProveedor p, ServicioFiscalDelProveedor servicio,
            HttpContext contexto, CancellationToken ct) =>
        {
            if (!TryOperador(contexto, out var id)) return Results.Unauthorized();
            var r = await servicio.GuardarAsync(id, p, ct);
            return r.EsExito ? Results.Ok(r.Valor) : r.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.AdministrarConfiguracion);
        grupo.MapPost("/certificados", CargarCertificado)
            .RequireAuthorization(PoliticasDeOperador.AdministrarConfiguracion)
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(256 * 1024));
    }

    private static async Task<IResult> CargarCertificado(HttpContext contexto,
        ServicioFiscalDelProveedor servicio, CancellationToken ct)
    {
        if (!TryOperador(contexto, out var id)) return Results.Unauthorized();
        if (!contexto.Request.HasFormContentType) return Results.BadRequest();
        if (contexto.Request.ContentLength > 256 * 1024)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (contexto.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
            limite.MaxRequestBodySize = 256 * 1024;
        contexto.Features.Set<IFormFeature>(new FormFeature(contexto.Request, new FormOptions
        { MultipartBodyLengthLimit = 65536, ValueLengthLimit = 1024, ValueCountLimit = 8 }));
        IFormCollection form;
        try { form = await contexto.Request.ReadFormAsync(ct); }
        catch (InvalidDataException)
        {
            return ErrorNegocio.Validacion("csd-formato-invalido", "Revisa los archivos y los datos de la carga.").AResultado(contexto);
        }
        var cer = form.Files.GetFile("cer"); var key = form.Files.GetFile("key");
        if (cer is null || key is null || cer.Length is 0 or > 65536 || key.Length is 0 or > 65536
            || !Path.GetExtension(cer.FileName).Equals(".cer", StringComparison.OrdinalIgnoreCase)
            || !Path.GetExtension(key.FileName).Equals(".key", StringComparison.OrdinalIgnoreCase))
            return ErrorNegocio.Validacion("csd-incompleto", "Carga un archivo .cer y otro .key, de hasta 64 KB.").AResultado(contexto);
        var versionTexto = form["version"].ToString();
        if (versionTexto.Length > 16 || !Convert.TryFromBase64String(versionTexto, new byte[8], out var longitud) || longitud != 8)
            return ErrorNegocio.Validacion("version-invalida", "Recarga los datos fiscales antes de cargar el CSD.").AResultado(contexto);
        using var cerMemoria = new MemoryStream(); using var keyMemoria = new MemoryStream();
        await cer.CopyToAsync(cerMemoria, ct); await key.CopyToAsync(keyMemoria, ct);
        var r = await servicio.CargarCsdAsync(id, cerMemoria.ToArray(), keyMemoria.ToArray(),
            form["contrasena"].ToString(), form["contrasenaOperador"].ToString(), Convert.FromBase64String(versionTexto), ct);
        return r.EsExito ? Results.Ok(r.Valor) : r.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext c, out Guid id)
        => Guid.TryParse(c.User.FindFirstValue(ClavesDeClaim.Operador), out id);
}
