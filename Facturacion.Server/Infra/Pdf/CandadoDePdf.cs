using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Infra.Pdf;

/// <summary>
/// Serializa la generación de PDFs con QuestPDF para mitigar fallos nativos intermitentes
/// (0xC0000005 en Skia/HarfBuzz) cuando coinciden varias peticiones o pruebas en paralelo.
/// </summary>
public static class CandadoDePdf
{
    private static readonly Lock Sincronizacion = new();

    public static byte[] Generar(IDocument documento)
    {
        lock (Sincronizacion)
            return documento.GeneratePdf();
    }
}
