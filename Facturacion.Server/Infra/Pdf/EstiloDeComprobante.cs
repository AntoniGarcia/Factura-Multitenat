using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Infra.Pdf;

public static class EstiloDeComprobante
{
    public const string Azul = "#006B91";
    public const string Tinta = "#102552";
    public const string Texto = "#20262C";
    public const string Secundario = "#59636D";
    public const string Borde = "#A7AFB7";
    public const string Blanco = "#FFFFFF";

    public static IContainer Cabecera(IContainer celda)
        => celda.Background(Azul).PaddingHorizontal(4).PaddingVertical(4)
            .DefaultTextStyle(t => t.FontColor(Blanco).Bold().FontSize(7));

    public static IContainer Celda(IContainer celda)
        => celda.BorderBottom(0.4f).BorderRight(0.4f).BorderColor(Borde).Padding(4);

    public static void Seccion(IContainer contenedor, string texto)
        => Cabecera(contenedor).Text(texto);
}
