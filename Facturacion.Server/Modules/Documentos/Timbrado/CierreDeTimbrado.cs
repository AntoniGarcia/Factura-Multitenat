using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>
/// El paso 3 del timbrado: la transacción corta que confirma o revierte.
///
/// <para><b>Por qué vive aparte de <see cref="ServicioDeTimbrado"/></b></para>
/// Lo necesitan dos caminos. El timbrado normal lo llama cuando el PAC contesta, y la
/// conciliación lo llama horas después, cuando averigua qué pasó con un comprobante que
/// quedó en el limbo. Si estuviera dentro del servicio de timbrado, la conciliación tendría
/// que arrastrar el generador de XML y el CSD para no usarlos, o —peor— reimplementar el
/// cierre y arriesgarse a que las dos versiones se separen.
/// </summary>
public sealed class CierreDeTimbrado(
    AppDbContext baseDeDatos,
    IServicioFolios folios,
    IServicioTimbres timbres,
    IAlmacenDeArchivos almacen,
    ILogger<CierreDeTimbrado> registro)
{
    /// <summary>Cierra bien. Es la única ruta que consume el timbre y confirma el folio.</summary>
    public async Task ConfirmarAsync(
        Comprobante comprobante, IntentoTimbrado intento, RespuestaDePac respuesta, CancellationToken ct)
    {
        // Fuera de la transacción y antes de abrirla: escribir en disco es E/S lenta, y
        // hacerlo con la transacción abierta la alarga sin motivo. Si el guardado falla, el
        // comprobante no se marca timbrado y la conciliación lo vuelve a intentar.
        var rutaXml = await GuardarXmlAsync(comprobante, respuesta, ct);

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        comprobante.Estatus = EstatusComprobante.Timbrado.ACadena();
        comprobante.Uuid = respuesta.Uuid;
        comprobante.FechaTimbradoUtc = respuesta.FechaTimbradoUtc ?? DateTime.UtcNow;
        comprobante.NoCertificadoSat = respuesta.NoCertificadoSat;
        comprobante.SelloSat = respuesta.SelloSat;
        comprobante.CadenaOriginalSat = respuesta.CadenaOriginalSat;
        comprobante.RutaXml = rutaXml ?? comprobante.RutaXml;
        comprobante.ModificadoUtc = DateTime.UtcNow;

        Cerrar(intento, ResultadosDeIntento.Timbrado, null, null);

        await baseDeDatos.SaveChangesAsync(ct);

        if (await ReservaDeTimbreAsync(comprobante.Id, ct) is { } timbre)
            await timbres.ConfirmarAsync(timbre, ct);

        if (await ReservaDeFolioAsync(comprobante, ct) is { } folio)
            await folios.ConfirmarAsync(folio, comprobante.Id, ct);

        await transaccion.CommitAsync(ct);

        registro.LogInformation(
            "Comprobante {Comprobante} timbrado con UUID {Uuid}.", comprobante.Id, respuesta.Uuid);
    }

    /// <summary>
    /// Cierra mal. Devuelve el timbre —no se gastó— pero <b>no</b> recicla el folio:
    /// ARQUITECTURA.md §5 lo prohíbe. El hueco en la numeración queda explicado por la reserva
    /// abandonada, que es justo para lo que sirve.
    /// </summary>
    public async Task RevertirAsync(
        Comprobante comprobante, IntentoTimbrado intento, string? codigo, string? mensaje, CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        comprobante.Estatus = EstatusComprobante.Error.ACadena();
        comprobante.ModificadoUtc = DateTime.UtcNow;

        Cerrar(intento, ResultadosDeIntento.Rechazado, codigo, mensaje);

        await baseDeDatos.SaveChangesAsync(ct);

        if (await ReservaDeTimbreAsync(comprobante.Id, ct) is { } timbre)
            await timbres.DevolverAsync(timbre, codigo ?? "timbrado-fallido", ct);

        if (await ReservaDeFolioAsync(comprobante, ct) is { } folio)
            await folios.LiberarSiNoUsadoAsync(folio, ct);

        await transaccion.CommitAsync(ct);

        registro.LogWarning(
            "Comprobante {Comprobante} quedó en error: {Codigo} {Mensaje}", comprobante.Id, codigo, mensaje);
    }

    /// <summary>
    /// Guarda el CFDI timbrado en el almacén cifrado, fuera de <c>wwwroot</c> (ARQUITECTURA.md §4).
    /// Es el archivo que el usuario descarga y el que vale ante el SAT.
    ///
    /// <para>
    /// Devuelve <c>null</c> cuando el PAC no mandó el XML. Pasa en un caso concreto y legítimo:
    /// la conciliación descubre que el comprobante ya estaba timbrado, y el PAC contesta el
    /// UUID del timbre previo sin volver a mandar el documento. El comprobante queda timbrado
    /// y correcto —que es lo que importa— pero sin XML descargable hasta recuperarlo del PAC.
    /// </para>
    /// </summary>
    private async Task<string?> GuardarXmlAsync(
        Comprobante comprobante, RespuestaDePac respuesta, CancellationToken ct)
    {
        if (respuesta.XmlTimbrado is not { Length: > 0 } xml)
        {
            registro.LogWarning(
                "El comprobante {Comprobante} se timbró pero el PAC no devolvió el XML. Queda sin archivo " +
                "descargable; hay que recuperarlo del PAC.", comprobante.Id);

            return null;
        }

        return await almacen.GuardarAsync(
            comprobante.EmpresaId, CategoriasDeArchivo.XmlTimbrado, Encoding.UTF8.GetBytes(xml), ct);
    }

    private static void Cerrar(IntentoTimbrado intento, string resultado, string? codigo, string? mensaje)
    {
        intento.Resultado = resultado;
        intento.TerminadoUtc = DateTime.UtcNow;
        intento.DuracionMs = (int)(intento.TerminadoUtc.Value - intento.IniciadoUtc).TotalMilliseconds;
        intento.CodigoError = codigo;
        intento.MensajeError = mensaje;
    }

    private async Task<Guid?> ReservaDeTimbreAsync(Guid comprobanteId, CancellationToken ct)
        => await baseDeDatos.ReservasTimbre
            .Where(r => r.ComprobanteId == comprobanteId && r.ResueltaUtc == null)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<Guid?> ReservaDeFolioAsync(Comprobante comprobante, CancellationToken ct)
        => comprobante.Folio is null
            ? null
            : await baseDeDatos.ReservasFolio
                .Where(r => r.SerieId == comprobante.SerieId && r.Folio == comprobante.Folio)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
}
