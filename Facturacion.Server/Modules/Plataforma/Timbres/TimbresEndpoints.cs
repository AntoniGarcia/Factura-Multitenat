using System.Globalization;
using System.Text;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>
/// Bolsa de timbres, paquetes, compras y membresía.
///
/// <para><b>Consultar el saldo no pide <c>comprar_timbres</c></b></para>
/// El permiso protege gastar dinero, no enterarse de cuánto queda. La tarjeta de saldo vive
/// en la barra superior y la ve cualquiera con sesión: un capturista que no puede comprar sí
/// necesita saber que se están acabando los timbres antes de empezar una factura que no va a
/// poder timbrar.
/// </summary>
public static class TimbresEndpoints
{
    public static void MapTimbres(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/timbres").WithTags("Timbres");

        grupo.MapGet("/saldo", Saldo).RequireAuthorization();
        grupo.MapGet("/membresia", Membresia).RequireAuthorization();

        grupo.MapGet("/paquetes", Paquetes).RequireAuthorization(Permisos.ComprarTimbres);
        grupo.MapGet("/compras", Compras).RequireAuthorization(Permisos.ComprarTimbres);
        grupo.MapGet("/movimientos", Movimientos).RequireAuthorization(Permisos.ComprarTimbres);
        grupo.MapGet("/movimientos/exportar", ExportarMovimientos).RequireAuthorization(Permisos.ComprarTimbres);

        // El único que cobra, y el único con filtro de idempotencia. Sin él, un doble clic o
        // un reintento del navegador dejarían dos compras del mismo paquete (CLAUDE.md §4).
        grupo.MapPost("/comprar", Comprar)
            .RequireAuthorization(Permisos.ComprarTimbres)
            .AddEndpointFilter<FiltroDeIdempotencia>();
    }

    private static async Task<IResult> Saldo(ServicioDeCompras compras, CancellationToken ct)
        => Results.Ok(await compras.SaldoAsync(ct));

    private static async Task<IResult> Paquetes(ServicioDeCompras compras, CancellationToken ct)
        => Results.Ok(await compras.PaquetesAsync(ct));

    private static async Task<IResult> Compras(ServicioDeCompras compras, CancellationToken ct)
        => Results.Ok(await compras.ComprasAsync(ct));

    private static async Task<IResult> Membresia(ServicioDeCompras compras, CancellationToken ct)
    {
        var membresia = await compras.MembresiaAsync(ct);

        return membresia is null ? Results.NoContent() : Results.Ok(membresia);
    }

    private static async Task<IResult> Movimientos(
        ServicioDeCompras compras, HttpContext http, CancellationToken ct,
        string? tipo = null, DateTime? desde = null, DateTime? hasta = null)
        => Results.Ok(await compras.MovimientosAsync(tipo, desde, hasta, ct));

    private static async Task<IResult> Comprar(
        PeticionDeCompra peticion, ServicioDeCompras compras, HttpContext http, CancellationToken ct)
    {
        var resultado = await compras.ComprarAsync(peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(http);
    }

    private static async Task<IResult> ExportarMovimientos(
        ServicioDeCompras compras, CancellationToken ct,
        string? tipo = null, DateTime? desde = null, DateTime? hasta = null)
    {
        var movimientos = await compras.MovimientosAsync(tipo, desde, hasta, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Momento,Tipo,Disponible,Reservado,SaldoDisponible,Motivo");

        foreach (var movimiento in movimientos)
        {
            csv.Append(movimiento.MomentoUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
               .Append(movimiento.Tipo).Append(',')
               .Append(movimiento.DeltaDisponible.ToString(CultureInfo.InvariantCulture)).Append(',')
               .Append(movimiento.DeltaReservado.ToString(CultureInfo.InvariantCulture)).Append(',')
               .Append(movimiento.DisponiblesDespues.ToString(CultureInfo.InvariantCulture)).Append(',')
               .AppendLine(Entrecomillar(movimiento.Motivo));
        }

        // Con preámbulo UTF-8: sin él, Excel en Windows abre el archivo en la página de
        // códigos local y los acentos de los motivos salen rotos.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", $"movimientos-timbres-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Entrecomillar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        return texto.Contains(',') || texto.Contains('"') || texto.Contains('\n')
            ? $"\"{texto.Replace("\"", "\"\"")}\""
            : texto;
    }
}
