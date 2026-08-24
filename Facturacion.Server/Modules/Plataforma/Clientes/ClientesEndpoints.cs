using System.Globalization;
using System.Text;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Clientes;

/// <summary>
/// Catálogo de clientes. Ningún endpoint recibe identificador de empresa: la empresa es la
/// del claim y el filtro global de EF Core hace el resto (ARQUITECTURA.md §4).
/// <para>
/// Basta con tener sesión para consultarlos —quien factura necesita elegir cliente— y hace
/// falta <c>configurar_empresa</c> para modificarlos, que es el permiso bajo el que vive el
/// resto de los catálogos de la empresa.
/// </para>
/// </summary>
public static class ClientesEndpoints
{
    public static void MapClientes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/clientes").WithTags("Clientes");

        grupo.MapGet("/", Listar).RequireAuthorization();
        grupo.MapGet("/exportar", Exportar).RequireAuthorization();
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization();

        grupo.MapPost("/", Crear).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo).RequireAuthorization(Permisos.ConfigurarEmpresa);
    }

    private static async Task<IResult> Listar(
        ServicioDeClientes clientes, CancellationToken ct,
        string? texto = null, bool? activos = true,
        int pagina = 0, int tamano = 25, string? orden = null, bool descendente = false)
    {
        // Tope duro al tamaño de página: sin él, alguien pide un millón de renglones y se
        // lleva el catálogo completo de una sentada.
        var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
        var paginaEfectiva = Math.Max(pagina, 0);

        return Results.Ok(await clientes.ListarAsync(
            texto, activos, paginaEfectiva, tamanoEfectivo, orden, descendente, ct));
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
    /// CSV del catálogo. Se arma aquí y no en el <c>Client</c> para no tener que bajar todos
    /// los clientes al navegador solo para volver a escribirlos.
    /// </summary>
    private static async Task<IResult> Exportar(
        ServicioDeClientes clientes, CancellationToken ct, bool? activos = true)
    {
        var lista = await clientes.ListarParaExportarAsync(activos, ct);

        var csv = new StringBuilder();

        csv.AppendLine(string.Join(',',
            "Clave", "RFC", "Nombre", "RegimenFiscal", "CodigoPostalFiscal", "UsoCFDI",
            "MetodoPago", "FormaPago", "Correo", "Telefono", "Activo"));

        foreach (var c in lista)
            csv.AppendLine(string.Join(',',
                Campo(c.ClaveInterna.ToString(CultureInfo.InvariantCulture)),
                Campo(c.Rfc), Campo(c.Nombre), Campo(c.RegimenFiscal), Campo(c.DomicilioFiscalCp),
                Campo(c.UsoCfdiPreferido), Campo(c.MetodoPagoPreferido), Campo(c.FormaPagoPreferida),
                Campo(c.CorreoPrincipal), Campo(c.Telefono), Campo(c.Activo ? "Sí" : "No")));

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
