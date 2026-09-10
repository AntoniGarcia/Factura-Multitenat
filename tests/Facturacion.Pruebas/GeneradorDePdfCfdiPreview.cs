using Facturacion.Server.Modules.Documentos.Salidas;
using QuestPDF.Companion;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace Facturacion.Pruebas;

public class GeneradorDePdfCfdiPreview
{
    public GeneradorDePdfCfdiPreview()
    {
        // Necesario si tu proyecto usa la licencia Community de QuestPDF
        // y no está configurado globalmente en otro lado.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]//(Skip = "Herramienta manual de diseño: correr solo a mano desde el IDE, con el QuestPDF Companion abierto.")]
    public void Previsualizar_Factura_Normal()
    {
        var comprobante = ComprobanteDeEjemplo.Crear();
        var datos = new DatosDelPdf(DateTime.Now, 2, Logo: null);

        var generador = new GeneradorDePdfCfdi();
        var documento = generador.Construir(comprobante, datos); // ver nota abajo
        
        documento.ShowInCompanion();
    }
}