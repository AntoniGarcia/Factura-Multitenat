using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Http.Features;

namespace Facturacion.Server.Modules.Operador.Catalogos;

public static class CatalogosDeOperadorEndpoints
{
    public static void MapCatalogosDeOperador(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/catalogos-sat").WithTags("Operador · Catálogos del SAT")
            .RequireAuthorization(PoliticasDeOperador.Operador, PoliticasDeOperador.VerCatalogosSat);
        grupo.MapGet("/", async (ServicioDeCatalogosDeOperador servicio, HttpContext contexto, CancellationToken ct) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await servicio.ListarAsync(ct));
        });
        grupo.MapGet("/actualizacion", (ActualizadorDeCatalogos actualizador, HttpContext contexto) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return actualizador.Estado is { } estado ? Results.Ok(estado) : Results.NoContent();
        });
        grupo.MapPost("/{catalogo}/actualizar", IniciarActualizacion)
            .RequireAuthorization(PoliticasDeOperador.AdministrarConfiguracion);
        grupo.MapGet("/{catalogo}", async (string catalogo, ServicioDeCatalogosDeOperador servicio, HttpContext contexto,
            CancellationToken ct, string? texto = null, bool? vigentes = null, int pagina = 0, int tamano = 24) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            var resultado = await servicio.ConsultarAsync(catalogo, texto, vigentes, pagina, tamano, ct);
            return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
        });
    }

    private static async Task<IResult> IniciarActualizacion(string catalogo, HttpContext contexto,
        ActualizadorDeCatalogos actualizador, CancellationToken ct)
    {
        var entrada = PreparacionAutomaticaDeCatalogos.Inventario.FirstOrDefault(x => string.Equals(x.Catalogo, catalogo, StringComparison.OrdinalIgnoreCase));
        if (entrada is null)
            return ErrorNegocio.Validacion("catalogo-invalido", "El catálogo no está disponible.").AResultado(contexto);
        if (!Guid.TryParse(contexto.User.FindFirst(ClavesDeClaim.Operador)?.Value, out var operador))
            return Results.Unauthorized();
        if (actualizador.Estado?.Estado == "cargando")
            return Results.Conflict(new DetalleProblema("catalogos-ocupados", "Actualización en curso", 409,
                "Espera a que termine la actualización actual.", null, "", null));
        const long limite = ActualizadorDeCatalogos.MaximoArchivo + 1024 * 1024;
        if (contexto.Request.ContentLength > limite)
            return ErrorNegocio.Validacion("archivo-grande", "El archivo admite hasta 50 MB.").AResultado(contexto);
        if (contexto.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } maximo)
            maximo.MaxRequestBodySize = limite;
        if (!contexto.Request.HasFormContentType)
            return ErrorNegocio.Validacion("archivo-requerido", "Selecciona el archivo oficial del SAT.").AResultado(contexto);
        IFormCollection formulario;
        try { formulario = await contexto.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = limite }, ct); }
        catch (InvalidDataException)
        {
            return ErrorNegocio.Validacion("archivo-invalido", "No se pudo leer el archivo. Revisa su tamaño y formato.").AResultado(contexto);
        }
        var archivo = formulario.Files.GetFile("archivo");
        if (archivo is null || archivo.Length == 0 || archivo.Length > ActualizadorDeCatalogos.MaximoArchivo
            || Path.GetExtension(archivo.FileName).ToLowerInvariant() is not (".xls" or ".xlsx"))
            return ErrorNegocio.Validacion("archivo-invalido", "Selecciona un Excel .xls o .xlsx de hasta 50 MB.").AResultado(contexto);
        var trabajo = await actualizador.EncolarAsync(entrada.Catalogo, archivo, operador, ct);
        return trabajo is null
            ? Results.Conflict(new DetalleProblema("catalogos-ocupados", "Actualización en curso", 409,
                "Espera a que termine la actualización actual.", null, "", null))
            : Results.Accepted("/api/operador/catalogos-sat/actualizacion", trabajo);
    }
}
