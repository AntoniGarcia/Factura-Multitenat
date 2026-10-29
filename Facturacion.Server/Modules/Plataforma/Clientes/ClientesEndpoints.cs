using System.Globalization;
using System.Text;
using Facturacion.Server.Infra.Errores;
using Microsoft.AspNetCore.Mvc;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Clientes;

/// <summary>
/// Catálogo de clientes de la cuenta. Ningún endpoint recibe identificador de empresa ni de
/// cuenta: la cuenta es la del claim y el filtro global de EF Core hace el resto.
/// <para>
/// Los consulta quien emite cualquier documento —necesita elegir cliente— o quien los
/// administra, y modificarlos pide <c>administrar_clientes</c>.
/// </para>
/// </summary>
public static class ClientesEndpoints
{
    /// <summary>Cinco megabytes, como en productos: miles de clientes caben de sobra.</summary>
    private const long TopeDeSubida = 5 * 1024 * 1024;

    public static void MapClientes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/clientes").WithTags("Clientes");

        grupo.MapGet("/", Listar).RequireAuthorization(Permisos.Politicas.LeerClientesYProductos);
        grupo.MapGet("/exportar", Exportar).RequireAuthorization(Permisos.Politicas.LeerClientesYProductos);
        grupo.MapGet("/plantilla-csv", PlantillaCsv).RequireAuthorization(Permisos.Politicas.EditarClientes);
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization(Permisos.Politicas.LeerClientesYProductos);

        grupo.MapPost("/", Crear).RequireAuthorization(Permisos.ClientesCrear);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(Permisos.ClientesModificar);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo).RequireAuthorization(Permisos.ClientesDesactivar);
        grupo.MapDelete("/{id:guid}", Eliminar).RequireAuthorization(Permisos.ClientesEliminar);

        // Igual que en productos: analizar no toca la base y confirmar guarda lo que se vio.
        grupo.MapPost("/importar/analizar", AnalizarCsv)
            .RequireAuthorization(Permisos.Politicas.EditarClientes)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDeSubida));

        grupo.MapPost("/importar/confirmar", ConfirmarImportacion)
            .RequireAuthorization(Permisos.Politicas.EditarClientes);
    }

    private static async Task<IResult> Eliminar(
        Guid id, ServicioDeClientes clientes, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await clientes.EliminarAsync(id, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    // Sin «activos» se listan todos: así pide el Client el filtro «Todos». Con true por omisión,
    // «Todos» mostraba solo los activos. Los buscadores de la emisión mandan true explícito.
    private static async Task<IResult> Listar(
        ServicioDeClientes clientes, CancellationToken ct,
        string? texto = null, bool? activos = null, Guid? empresa = null,
        int pagina = 0, int tamano = 25, string? orden = null, bool descendente = false)
    {
        // Tope duro al tamaño de página: sin él, alguien pide un millón de renglones y se
        // lleva el catálogo completo de una sentada.
        var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
        var paginaEfectiva = Math.Max(pagina, 0);

        return Results.Ok(await clientes.ListarAsync(
            texto, activos, empresa, paginaEfectiva, tamanoEfectivo, orden, descendente, ct));
    }

    private static async Task<IResult> Obtener(Guid id, ServicioDeClientes clientes, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        return cliente is null ? Results.NotFound() : Results.Ok(cliente);
    }

    private static async Task<IResult> Crear(
        PeticionGuardarCliente peticion, ServicioDeClientes clientes, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await clientes.CrearAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Actualizar(
        Guid id, PeticionGuardarCliente peticion, ServicioDeClientes clientes,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await clientes.ActualizarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id, PeticionCambiarActivo peticion, ServicioDeClientes clientes,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await clientes.CambiarActivoAsync(id, peticion.Activo, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    /// <summary>
    /// Analiza el CSV y devuelve la vista previa. No guarda nada. Cada renglón pasa por el mismo
    /// validador del alta manual, para que no aparezcan errores nuevos al confirmar.
    /// </summary>
    private static async Task<IResult> AnalizarCsv(
        IFormFile archivo, ValidadorDeCliente validador, CancellationToken ct)
    {
        using var flujo = archivo.OpenReadStream();
        var vista = LectorDeCsvDeClientes.Analizar(flujo);

        var revisados = new List<RenglonDeImportacionDeCliente>(vista.Renglones.Count);

        foreach (var renglon in vista.Renglones)
        {
            if (renglon.Cliente is not { } cliente) { revisados.Add(renglon); continue; }

            var error = await validador.ValidarAsync(
                cliente, cliente.Rfc, NombreFiscal.Normalizar(cliente.Nombre).Normalizado, ct);
            revisados.Add(error is null ? renglon : renglon with { Error = error.Mensaje });
        }

        return Results.Ok(new VistaPreviaDeImportacionDeClientes(
            revisados,
            revisados.Count(r => r.Error is null),
            revisados.Count(r => r.Error is not null)));
    }

    private static async Task<IResult> ConfirmarImportacion(
        PeticionConfirmarImportacionDeClientes peticion, ServicioDeClientes clientes, CancellationToken ct)
    {
        var validos = peticion.Renglones.Where(r => r.Error is null && r.Cliente is not null).ToList();

        return Results.Ok(await clientes.ImportarAsync(validos, ct));
    }

    /// <summary>Encabezados y dos renglones de ejemplo, para no adivinar el formato.</summary>
    private static IResult PlantillaCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', LectorDeCsvDeClientes.Columnas));
        csv.AppendLine("EKU9003173C9,ESCUELA KEMPER URGATE,601,42000,G03,PUE,03,compras@ejemplo.mx,7711234567,,");
        csv.AppendLine("XEXX010101000,CLIENTE DEL EXTRANJERO,616,42000,S01,PUE,03,,,USA,123456789");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", "plantilla-clientes.csv");
    }

    /// <summary>
    /// CSV del catálogo. Se arma aquí y no en el <c>Client</c> para no tener que bajar todos
    /// los clientes al navegador solo para volver a escribirlos.
    /// </summary>
    private static async Task<IResult> Exportar(
        ServicioDeClientes clientes, CancellationToken ct, bool? activos = null, Guid? empresa = null)
    {
        var lista = await clientes.ListarParaExportarAsync(activos, empresa, ct);

        var csv = new StringBuilder();

        // Las mismas columnas que se importan: el archivo que sale se corrige y vuelve a entrar.
        csv.AppendLine(string.Join(',',
            new[] { "Clave" }.Concat(LectorDeCsvDeClientes.Columnas).Append("Activo")));

        foreach (var c in lista)
            csv.AppendLine(string.Join(',',
                Campo(c.ClaveInterna.ToString(CultureInfo.InvariantCulture)),
                Campo(c.Rfc), Campo(c.Nombre), Campo(c.RegimenFiscal), Campo(c.DomicilioFiscalCp),
                Campo(c.UsoCfdiPreferido), Campo(c.MetodoPagoPreferido), Campo(c.FormaPagoPreferida),
                Campo(c.CorreoPrincipal), Campo(c.Telefono), Campo(c.ResidenciaFiscal), Campo(c.NumRegIdTrib),
                Campo(c.Activo ? "Sí" : "No")));

        // Con marca de orden de bytes: sin ella Excel abre el CSV en la codificación del
        // sistema y los acentos de los nombres salen rotos, que es justo lo que un contador
        // no debe tener que arreglar a mano.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", $"clientes-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Escapa un campo de CSV. Un nombre con coma —«ACME, S.A.»— partiría el renglón en dos
    /// columnas y correría todo lo demás.
    /// </summary>
    private static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;

        var necesitaComillas = valor.Contains(',') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r');

        return necesitaComillas ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }
}

/// <summary>Alta o baja lógica de un cliente.</summary>
public sealed record PeticionCambiarActivo(bool Activo);
