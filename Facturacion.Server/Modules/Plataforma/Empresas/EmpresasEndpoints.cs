using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Empresa emisora, su configuración, su logo y sus certificados.
/// <para>
/// Todo el grupo exige el permiso <c>configurar_empresa</c>, salvo la lectura del logo, que
/// la necesita cualquiera que vea una vista previa. Ningún endpoint recibe un identificador
/// de empresa: la empresa es la del claim (CLAUDE.md §4).
/// </para>
/// </summary>
public static class EmpresasEndpoints
{
    /// <summary>Tope del cuerpo al subir archivos. El logo son 2 MB; un CSD, unos pocos kilobytes.</summary>
    private const long TopeDeSubida = 3 * 1024 * 1024;

    public static void MapEmpresas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/empresa").WithTags("Empresa");

        grupo.MapGet("/", Obtener).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/", Guardar).RequireAuthorization(Permisos.ConfigurarEmpresa);

        grupo.MapGet("/configuracion", ObtenerConfiguracion).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/configuracion", GuardarConfiguracion).RequireAuthorization(Permisos.ConfigurarEmpresa);

        // Lectura del logo: basta con tener sesión en la empresa. El endpoint no recibe
        // empresa ni ruta —las saca del claim—, así que no hay forma de pedir el de otra.
        grupo.MapGet("/logo", ObtenerLogo).RequireAuthorization();

        grupo.MapPost("/logo", SubirLogo)
            .RequireAuthorization(Permisos.ConfigurarEmpresa)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDeSubida));

        grupo.MapDelete("/logo", QuitarLogo).RequireAuthorization(Permisos.ConfigurarEmpresa);

        grupo.MapGet("/certificados", ListarCertificados).RequireAuthorization(Permisos.ConfigurarEmpresa);

        grupo.MapPost("/certificados", CargarCertificado)
            .RequireAuthorization(Permisos.ConfigurarEmpresa)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDeSubida));
    }

    private static async Task<IResult> Obtener(ServicioDeEmpresa empresas, CancellationToken ct)
    {
        var empresa = await empresas.ObtenerAsync(ct);
        return empresa is null ? Results.NotFound() : Results.Ok(empresa);
    }

    private static async Task<IResult> Guardar(
        PeticionGuardarEmpresa peticion, ServicioDeEmpresa empresas, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await empresas.GuardarAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObtenerConfiguracion(ServicioDeEmpresa empresas, CancellationToken ct)
        => Results.Ok(await empresas.ObtenerConfiguracionAsync(ct));

    private static async Task<IResult> GuardarConfiguracion(
        ConfiguracionEmpresaDto peticion, ServicioDeEmpresa empresas, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await empresas.GuardarConfiguracionAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObtenerLogo(ServicioDeLogo logos, CancellationToken ct)
    {
        var logo = await logos.ObtenerAsync(ct);

        return logo is null
            ? Results.NotFound()
            : Results.File(logo.Contenido, logo.TipoMime, logo.NombreOriginal);
    }

    private static async Task<IResult> SubirLogo(
        IFormFile archivo, ServicioDeLogo logos, HttpContext contexto, CancellationToken ct)
    {
        var contenido = await LeerAsync(archivo, ct);
        var resultado = await logos.GuardarAsync(archivo.FileName, contenido, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(new { tipoMime = resultado.Valor });
    }

    private static async Task<IResult> QuitarLogo(ServicioDeLogo logos, CancellationToken ct)
        => await logos.QuitarAsync(ct) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> ListarCertificados(ServicioDeCsd csd, CancellationToken ct)
        => Results.Ok(await csd.ListarAsync(ct));

    // [FromForm] en la contraseña no es decorativo: sin él, una API mínima busca un
    // parámetro suelto de ese tipo en la cadena de consulta, y una contraseña en la URL
    // acabaría en los registros del servidor y en el historial del navegador.
    private static async Task<IResult> CargarCertificado(
        IFormFile cer, IFormFile key, [FromForm] string contrasena,
        ServicioDeCsd csd, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await csd.CargarAsync(
            await LeerAsync(cer, ct), await LeerAsync(key, ct), contrasena, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<byte[]> LeerAsync(IFormFile archivo, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        return memoria.ToArray();
    }
}
