using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Traslados;

/// <summary>Genera archivos de revisión del traslado sin persistirlos ni contactar al PAC.</summary>
public sealed class ServicioDeSalidasCartaPorte(AppDbContext db, ServicioDeXmlCfdi xml, GeneradorDePdfCartaPorte pdf, HusoDeEmpresa huso)
{
    public async Task<Resultado<ArchivoFiscal>> PdfAsync(Guid comprobanteId, CancellationToken ct)
    {
        var traslado = await CargarAsync(comprobanteId, ct);
        if (traslado is null) return ErrorNegocio.NoEncontrado("traslado-no-encontrado", "No se encontró ese traslado Carta Porte.");
        if (traslado.Comprobante.Estatus is not ("borrador" or "error")) return ErrorNegocio.Conflicto("traslado-no-es-borrador", "La vista previa solo está disponible para borradores.");
        return new ArchivoFiscal($"borrador-carta-porte-{comprobanteId:N}.pdf", "application/pdf", pdf.Generar(traslado.Comprobante, traslado, await huso.ObtenerAsync(ct)));
    }

    public async Task<Resultado<ArchivoFiscal>> XmlAsync(Guid comprobanteId, CancellationToken ct)
    {
        var traslado = await CargarAsync(comprobanteId, ct);
        if (traslado is null) return ErrorNegocio.NoEncontrado("traslado-no-encontrado", "No se encontró ese traslado Carta Porte.");
        if (traslado.Comprobante.Estatus is not ("borrador" or "error")) return ErrorNegocio.Conflicto("traslado-no-es-borrador", "El XML de revisión solo está disponible para borradores.");
        var resultado = await xml.GenerarAsync(traslado.Comprobante, ct);
        return resultado.EsFallo ? resultado.Error! : new ArchivoFiscal($"borrador-carta-porte-{comprobanteId:N}.xml", "application/xml", Encoding.UTF8.GetBytes(resultado.Valor.Xml));
    }

    private Task<TrasladoCartaPorte?> CargarAsync(Guid comprobanteId, CancellationToken ct) => db.TrasladosCartaPorte
        .Include(x => x.Comprobante).ThenInclude(x => x.Conceptos).ThenInclude(x => x.Impuestos)
        .Include(x => x.Comprobante).ThenInclude(x => x.Relacionados)
        .Include(x => x.Ubicaciones).Include(x => x.Mercancias).FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
}
