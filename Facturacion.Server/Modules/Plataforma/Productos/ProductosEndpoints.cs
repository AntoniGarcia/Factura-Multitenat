using System.Globalization;
using System.Text;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Server.Modules.Plataforma.Productos;

/// <summary>
/// Catálogo de productos y servicios. Consultarlo basta con tener sesión —quien factura
/// necesita elegir producto—; modificarlo pide <c>configurar_empresa</c>.
/// </summary>
public static class ProductosEndpoints
{
    /// <summary>Un CSV de cinco mil productos no llega a esto; el tope acota el abuso.</summary>
    private const long TopeDeSubida = 5 * 1024 * 1024;

    public static void MapProductos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/productos").WithTags("Productos");

        grupo.MapGet("/", Listar).RequireAuthorization();
        grupo.MapGet("/exportar", Exportar).RequireAuthorization();
        grupo.MapGet("/plantilla-csv", PlantillaCsv).RequireAuthorization();
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization();

        grupo.MapPost("/", Crear).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo).RequireAuthorization(Permisos.ConfigurarEmpresa);

        // Analizar y confirmar van separados a propósito: el primero no toca la base, y es
        // lo que permite enseñar la vista previa antes de que el usuario se comprometa.
        grupo.MapPost("/importar/analizar", AnalizarCsv)
            .RequireAuthorization(Permisos.ConfigurarEmpresa)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDeSubida));

        grupo.MapPost("/importar/confirmar", ConfirmarImportacion)
            .RequireAuthorization(Permisos.ConfigurarEmpresa);
    }

    private static async Task<IResult> Listar(
        ServicioDeProductos productos, CancellationToken ct,
        string? texto = null, bool? activos = true,
        int pagina = 0, int tamano = 25, string? orden = null, bool descendente = false)
        => Results.Ok(await productos.ListarAsync(
            texto, activos, Math.Max(pagina, 0), Math.Clamp(tamano, 1, 100), orden, descendente, ct));

    private static async Task<IResult> Obtener(Guid id, ServicioDeProductos productos, CancellationToken ct)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        return producto is null ? Results.NotFound() : Results.Ok(producto);
    }

    private static async Task<IResult> Crear(
        PeticionGuardarProducto peticion, ServicioDeProductos productos, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await productos.CrearAsync(peticion, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Actualizar(
        Guid id, PeticionGuardarProducto peticion, ServicioDeProductos productos,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await productos.ActualizarAsync(id, peticion, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id, PeticionCambiarActivoProducto peticion, ServicioDeProductos productos,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await productos.CambiarActivoAsync(id, peticion.Activo, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    /// <summary>Analiza el CSV y devuelve la vista previa. No guarda nada.</summary>
    private static async Task<IResult> AnalizarCsv(
        IFormFile archivo, ValidadorDeProducto validador, CancellationToken ct)
    {
        using var flujo = archivo.OpenReadStream();
        var vista = LectorDeCsvDeProductos.Analizar(flujo);

        // El lector solo comprueba la forma del renglón; las claves del SAT las valida el
        // mismo validador que usa el alta manual, para que la vista previa diga la verdad y
        // no aparezcan errores nuevos al confirmar.
        var revisados = new List<RenglonDeImportacion>(vista.Renglones.Count);

        foreach (var renglon in vista.Renglones)
        {
            if (renglon.Producto is null) { revisados.Add(renglon); continue; }

            var error = await validador.ValidarAsync(renglon.Producto, ct);
            revisados.Add(error is null ? renglon : renglon with { Error = error.Mensaje });
        }

        return Results.Ok(new VistaPreviaDeImportacion(
            revisados,
            revisados.Count(r => r.Error is null),
            revisados.Count(r => r.Error is not null)));
    }

    /// <summary>
    /// Guarda los renglones que el usuario confirmó. Recibe los renglones ya revisados y no
    /// el archivo otra vez: así se guarda exactamente lo que se vio en la vista previa.
    /// </summary>
    private static async Task<IResult> ConfirmarImportacion(
        PeticionConfirmarImportacion peticion, ServicioDeProductos productos, CancellationToken ct)
    {
        var validos = peticion.Renglones.Where(r => r.Error is null && r.Producto is not null).ToList();

        return Results.Ok(await productos.ImportarAsync(validos, ct));
    }

    private static async Task<IResult> Exportar(
        ServicioDeProductos productos, CancellationToken ct, bool? activos = true)
    {
        var lista = await productos.ListarParaExportarAsync(activos, ct);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',',
            new[] { "Codigo" }.Concat(LectorDeCsvDeProductos.Columnas).Append("Activo")));

        foreach (var p in lista)
        {
            // Se exporta con las mismas columnas que se importan: el archivo que sale se
            // puede corregir y volver a entrar sin traducir nada.
            var traslado = p.Impuestos.FirstOrDefault(i => !i.EsRetencion && i.Impuesto == "002");

            var iva = traslado is null
                ? string.Empty
                : traslado.TipoFactor == "Exento"
                    ? "exento"
                    : (traslado.TasaOCuota * 100m)?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

            csv.AppendLine(string.Join(',',
                p.CodigoInterno.ToString(CultureInfo.InvariantCulture),
                Campo(p.ClaveProdServ), Campo(p.ClaveUnidad), Campo(p.UnidadTexto), Campo(p.Descripcion),
                p.ValorUnitario.ToString("0.######", CultureInfo.InvariantCulture),
                p.PesoKg?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty,
                Campo(p.ObjetoImp), Campo(iva),
                p.Activo ? "Sí" : "No"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", $"productos-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>Un CSV vacío con los encabezados y un renglón de ejemplo, para no adivinar el formato.</summary>
    private static IResult PlantillaCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', LectorDeCsvDeProductos.Columnas));
        csv.AppendLine("01010101,H87,Pieza,Ejemplo de producto,100.00,1.5,02,16");
        csv.AppendLine("01010101,E48,Servicio,Ejemplo de servicio exento,250.00,,02,exento");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", "plantilla-productos.csv");
    }

    private static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;

        var necesitaComillas = valor.Contains(',') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r');

        return necesitaComillas ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }
}

/// <summary>Alta o baja lógica de un producto.</summary>
public sealed record PeticionCambiarActivoProducto(bool Activo);

/// <summary>Los renglones que el usuario aprobó en la vista previa.</summary>
public sealed record PeticionConfirmarImportacion(IReadOnlyList<RenglonDeImportacion> Renglones);
