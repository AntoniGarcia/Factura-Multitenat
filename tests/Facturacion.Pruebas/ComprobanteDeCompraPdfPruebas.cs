using Facturacion.Server.Modules.Plataforma.Timbres;

namespace Facturacion.Pruebas;

public sealed class ComprobanteDeCompraPdfPruebas
{
    static ComprobanteDeCompraPdfPruebas()
        => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Fact]
    public void Genera_un_pdf_valido_para_una_compra_pagada()
    {
        var datos = new DatosDeComprobanteDeCompra(
            Guid.Parse("560B0DA8-6050-4996-A77D-198025E371CB"),
            "Facturación Luis",
            "DISTRIBUIDORA DEL CENTRO",
            "EKU9003173C9",
            "Venta directa de timbres",
            1_000,
            1.25m,
            1_077.586207m,
            172.413793m,
            0.16m,
            1_250m,
            new DateTime(2026, 9, 14, 15, 20, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 14, 15, 25, 0, DateTimeKind.Utc),
            new DateTime(2027, 9, 14, 15, 25, 0, DateTimeKind.Utc));

        var pdf = new GeneradorDeComprobanteDeCompraPdf().Generar(datos);

        Assert.Equal("%PDF-"u8.ToArray(), pdf.Take(5).ToArray());
        Assert.True(pdf.Length > 2_000, $"El PDF salió sospechosamente corto: {pdf.Length} bytes.");
    }
}
