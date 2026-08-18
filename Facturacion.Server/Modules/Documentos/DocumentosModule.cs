using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Server.Modules.Documentos.Timbrado;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Punto de entrada de la mitad B: emisión, impuestos, timbrado, PAC, XML, PDF, correo,
/// cancelación y complemento de pagos.
/// <para>
/// Todo registro y todo endpoint de documentos se declara aquí dentro, nunca en
/// <c>Program.cs</c> (REPARTO-EQUIPO.md §5).
/// </para>
/// </summary>
public static class DocumentosModule
{
    public static IServiceCollection AddDocumentos(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        // Implementación real, no un doble: lee comprobantes de verdad. Es el único contrato
        // que va de B hacia A —lo consume el tablero— y hasta la fase B0 no existía, así que
        // la verificación de arranque lo reportaba como ausente en cada arranque.
        servicios.AddScoped<IResumenDocumentos, ResumenDocumentos>();

        servicios.AddOptions<OpcionesDeEsquemasSat>().Bind(configuracion.GetSection(OpcionesDeEsquemasSat.Seccion));

        // Singleton: compilar el XSD y el XSLT del SAT cuesta cientos de milisegundos y no
        // cambian mientras el proceso viva. Hacerlo en cada timbrado sería pagarlo justo en
        // la operación con más prisa.
        servicios.AddSingleton<EsquemasSat>();

        // Licencia Community de QuestPDF: gratuita mientras la facturación anual del producto
        // quede por debajo de un millón de dólares (ver Directory.Packages.props). Se declara
        // una sola vez y antes de generar cualquier documento.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        servicios.AddScoped<GeneradorDeXmlCfdi>();
        servicios.AddScoped<GeneradorDePdfCfdi>();
        servicios.AddScoped<ServicioDeXmlCfdi>();

        servicios.AddScoped<CierreDeTimbrado>();
        servicios.AddScoped<ServicioDeTimbrado>();

        // Saca de 'timbrando' a los comprobantes cuya llamada al PAC nunca volvió. Sin esto,
        // un corte de red deja facturas en un limbo del que nadie las saca.
        servicios.AddHostedService<ConciliacionDeTimbrados>();

        return servicios;
    }

    public static WebApplication MapDocumentos(this WebApplication aplicacion) => aplicacion;
}
