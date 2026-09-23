using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Timbrado;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace Facturacion.Server.Modules.Documentos.Salidas;

public sealed record ArchivoDescarga(byte[] Contenido, string TipoMime, string NombreArchivo);

/// <summary>
/// Entrega los archivos de un comprobante: el XML tal como lo devolvió el PAC (nunca se
/// regenera: <see cref="GeneradorDeXmlCfdi"/> produce uno con Sello vacío, y el que vale es
/// el que se selló y quedó en el almacén cifrado), el PDF (se genera al vuelo, no se
/// persiste) y un ZIP con los dos.
///
/// <para><b>Aislamiento por empresa, en dos capas</b></para>
/// El comprobante se busca en <c>baseDeDatos.Comprobantes</c>, así que el filtro global de
/// EF Core ya descarta cualquier id de otra empresa antes de que este servicio vea nada.
/// El almacén cifrado es la segunda capa: aun con la ruta en la mano, el archivo no descifra
/// con otra empresa (ver <see cref="IAlmacenDeArchivos"/>).
/// </summary>
public sealed class ServicioDeDescargas(
    AppDbContext baseDeDatos,
    IAlmacenDeArchivos almacen,
    GeneradorDePdfCfdi generadorPdf,
    IServicioEmpresaEmisora empresaEmisora,
    HusoDeEmpresa huso)
{
    public async Task<Resultado<ArchivoDescarga>> ObtenerXmlAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        if (comprobante is null) return NoEncontrado();

        if (comprobante.RutaXml is not { } ruta)
            return SinXml();

        var contenido = await almacen.LeerAsync(comprobante.EmpresaId, CategoriasDeArchivo.XmlTimbrado, ruta, ct);

        return new ArchivoDescarga(contenido, "application/xml", $"{NombreBase(comprobante)}.xml");
    }

    public async Task<Resultado<ArchivoDescarga>> ObtenerPdfAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        if (comprobante is null) return NoEncontrado();

        var pdf = await GenerarPdfAsync(comprobante, ct);

        return new ArchivoDescarga(pdf, "application/pdf", $"{NombreBase(comprobante)}.pdf");
    }

    public async Task<Resultado<ArchivoDescarga>> ObtenerZipAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        if (comprobante is null) return NoEncontrado();

        if (comprobante.RutaXml is not { } ruta)
            return SinXml();

        var xml = await almacen.LeerAsync(comprobante.EmpresaId, CategoriasDeArchivo.XmlTimbrado, ruta, ct);
        var pdf = await GenerarPdfAsync(comprobante, ct);
        var nombreBase = NombreBase(comprobante);

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            await EscribirEntradaAsync(zip, $"{nombreBase}.xml", xml, ct);
            await EscribirEntradaAsync(zip, $"{nombreBase}.pdf", pdf, ct);
        }

        return new ArchivoDescarga(memoria.ToArray(), "application/zip", $"{nombreBase}.zip");
    }

    private async Task<byte[]> GenerarPdfAsync(Comprobante comprobante, CancellationToken ct)
    {
        var decimales = await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == comprobante.Moneda)
            .Select(m => (int?)m.Decimales)
            .FirstOrDefaultAsync(ct) ?? 2;

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(comprobante.FechaEmisionUtc, await huso.ObtenerAsync(ct));
        var logo = await empresaEmisora.ObtenerLogoAsync(ct);

        return generadorPdf.Generar(comprobante, new DatosDelPdf(fechaLocal, decimales, logo?.Contenido));
    }

    private static async Task EscribirEntradaAsync(ZipArchive zip, string nombre, byte[] contenido, CancellationToken ct)
    {
        var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
        await using var flujo = entrada.Open();
        await flujo.WriteAsync(contenido, ct);
    }

    /// <summary>Mismo patrón que FolioInterno/FolioDeDocumento en GeneradorDePdfCfdi: serie y folio pegados.</summary>
    private static string NombreBase(Comprobante c)
        => c.Folio is null ? c.Id.ToString() : $"{c.Serie}{c.Folio}";

    private Task<Comprobante?> CargarAsync(Guid id, CancellationToken ct)
        => baseDeDatos.Comprobantes
            .Include(c => c.Conceptos.OrderBy(x => x.Orden)).ThenInclude(x => x.Impuestos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    private static ErrorNegocio NoEncontrado()
        => ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

    private static ErrorNegocio SinXml()
        => ErrorNegocio.Regla("comprobante-sin-xml", "Este comprobante no tiene un XML timbrado para descargar.");
}