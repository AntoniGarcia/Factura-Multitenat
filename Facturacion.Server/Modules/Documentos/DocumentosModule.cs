using Facturacion.Server.Modules.Documentos.Cancelacion;
using Facturacion.Server.Modules.Documentos.Dobles;
using Facturacion.Server.Modules.Documentos.Emision;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Pac;
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
        this IServiceCollection servicios, IConfiguration configuracion, IHostEnvironment entorno)
    {
        // Dobles de prueba (REPARTO-EQUIPO.md §7): solo en Development para que B pueda probar
        // sin esperar a que A termine sus módulos. Sin estos, B queda bloqueado desde octubre.
        if (entorno.IsDevelopment())
        {
            servicios.AddScoped<IServicioCatalogosSat, DobleServicioCatalogosSat>();
            servicios.AddScoped<IServicioClientes, DobleServicioClientes>();
            servicios.AddScoped<IServicioProductos, DobleServicioProductos>();
            servicios.AddScoped<IServicioEmpresaEmisora, DobleServicioEmpresaEmisora>();
            servicios.AddScoped<IServicioFolios, DobleServicioFolios>();
            servicios.AddScoped<IServicioTimbres, DobleServicioTimbres>();
            servicios.AddScoped<IProveedorCsdParaTimbrado, DobleProveedorCsdParaTimbrado>();
        }

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
        servicios.AddScoped<Emision.ServicioDeEmision>();
        servicios.AddScoped<ServicioDeCancelacion>();
        servicios.AddScoped<ServicioDePagos>();

        servicios.AgregarPac(configuracion, entorno);

        // Saca de 'timbrando' a los comprobantes cuya llamada al PAC nunca volvió. Sin esto,
        // un corte de red deja facturas en un limbo del que nadie las saca.
        servicios.AddHostedService<ConciliacionDeTimbrados>();

        // Recoge los borradores que deja atrás abrir «Nueva factura» y cerrar la pantalla.
        servicios.AddHostedService<BarridoDeBorradoresHuerfanos>();

        return servicios;
    }

    /// <summary>
    /// Registra el proveedor de timbrado solo si hay credenciales. Sin ellas, el sistema
    /// arranca y todo lo demás funciona: <see cref="ServicioDeTimbrado"/> rechaza el timbrado
    /// antes de apartar folio o timbre, y lo dice con esas palabras.
    ///
    /// <para>
    /// Fuera de <c>Development</c> la ausencia impide arrancar, igual que el correo o la clave
    /// de firma del JWT: un despliegue de producción que no puede timbrar no es un despliegue
    /// degradado, es uno roto, y hay que verlo el día del despliegue y no cuando el primer
    /// usuario intente facturar.
    /// </para>
    /// </summary>
    private static IServiceCollection AgregarPac(
        this IServiceCollection servicios, IConfiguration configuracion, IHostEnvironment entorno)
    {
        servicios.AddOptions<OpcionesDePac>().Bind(configuracion.GetSection(OpcionesDePac.Seccion));

        var opciones = configuracion.GetSection(OpcionesDePac.Seccion).Get<OpcionesDePac>() ?? new OpcionesDePac();

        if (!opciones.EstaConfigurado)
        {
            if (!entorno.IsDevelopment())
                throw new InvalidOperationException(
                    "Falta la configuración de 'Pac' (UrlBase, Usuario, Contrasena). Sin ella no se puede " +
                    "timbrar. Se configura en variables de entorno; nunca en el repositorio.");

            return servicios;
        }

        servicios.AddHttpClient(ProveedorPacSwSapien.ClienteHttp, cliente =>
        {
            cliente.BaseAddress = new Uri(opciones.UrlBase);
            cliente.Timeout = TimeSpan.FromSeconds(opciones.TiempoDeEsperaSegundos);
        });

        // Singleton porque guarda el token del PAC, que dura dos horas: pedir uno nuevo en
        // cada timbrado añadiría un viaje de red al camino con más prisa.
        servicios.AddSingleton<IProveedorPac, ProveedorPacSwSapien>();

        return servicios;
    }

    public static WebApplication MapDocumentos(this WebApplication aplicacion)
    {
        aplicacion.MapEmision();
        aplicacion.MapTimbrado();
        aplicacion.MapCancelacion();
        aplicacion.MapPagos();

        return aplicacion;
    }
}
