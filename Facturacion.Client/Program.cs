using System.Globalization;
using Facturacion.Client;
using Facturacion.Client.Servicios.Documentos;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Cultura fija, no la del navegador. En WebAssembly, CurrentCulture sale de la
// configuración de idioma del equipo: un navegador en español de España formateaba los
// precios de los timbres en euros. Con la fecha pasa lo mismo y se nota menos: en inglés
// 03/10 es 10 de marzo y aquí 3 de octubre, y una factura mal fechada no salta a la vista.
// Un sistema de CFDI mexicano no puede dejar que el idioma del navegador decida eso.
//
// Funciona porque Directory.Build.props tiene InvariantGlobalization en false: con la
// globalización invariante, "C" no tendría de dónde sacar el símbolo y saldría un ¤.
var mexico = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = mexico;
CultureInfo.DefaultThreadCurrentUICulture = mexico;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var origen = new Uri(builder.HostEnvironment.BaseAddress);

// La sesión y el tema son únicos para toda la aplicación: el token vive en memoria y el
// tema aplicado al DOM no depende de qué componente esté montado.
builder.Services.AddSingleton<ServicioDeSesion>();
builder.Services.AddSingleton<ServicioDeTema>();
builder.Services.AddSingleton<EstadoDeEncabezado>();

builder.Services.AddScoped<IBusquedaDeCatalogo, BusquedaDeCatalogo>();
builder.Services.AddSingleton<ServicioDeCatalogos>();
builder.Services.AddScoped<ServicioDeEmpresa>();
builder.Services.AddScoped<ServicioDeClientes>();
builder.Services.AddScoped<ServicioDeProductos>();
builder.Services.AddScoped<ServicioDeTimbres>();
builder.Services.AddScoped<ServicioDeUsuarios>();
builder.Services.AddScoped<ServicioDePerfil>();
builder.Services.AddScoped<ServicioDeTablero>();
builder.Services.AddScoped<ServicioDeEmision>();
builder.Services.AddScoped<ServicioDeListado>();
builder.Services.AddScoped<ServicioDeCancelacion>();
builder.Services.AddScoped<ServicioDePagos>();

// El mismo objeto atiende las dos cosas: la barra superior lo ve como IIndicadorDeTimbres y
// las pantallas de timbres como ServicioDeTimbres. Se resuelve del contenedor y no se
// registra dos veces para que ambos usos compartan instancia dentro de una misma petición.
builder.Services.AddScoped<IIndicadorDeTimbres>(sp => sp.GetRequiredService<ServicioDeTimbres>());

builder.Services.AddScoped<IServicioDeConfirmacion, ServicioDeConfirmacion>();
builder.Services.AddScoped<IServicioModalCatalogo, ServicioModalCatalogo>();
builder.Services.AddSingleton<IServicioDeVersion, ServicioDeVersion>();
builder.Services.AddSingleton<ServicioDeInstalacion>();

// Las mismas seis políticas que el Server, desde la misma lista: el Client las necesita para
// enrutar y para ocultar lo que no aplica. Es comodidad visual, NO protección — el Server
// rechaza igual la operación aunque alguien llegue a la ruta a mano (ARQUITECTURA.md §4).
builder.Services.AddAuthorizationCore(opciones =>
{
    foreach (var permiso in Permisos.Todos)
        opciones.AddPolicy(permiso, politica => politica
            .RequireAuthenticatedUser()
            .RequireClaim(ClavesDeClaim.Permiso, permiso));
});
builder.Services.AddScoped<AuthenticationStateProvider, ProveedorEstadoAutenticacion>();
builder.Services.AddTransient<ManejadorDeAutenticacion>();

// MudBlazor no trae paleta propia aquí: lee las variables de mudblazor-tema.css, que a su
// vez apuntan a las nuestras (ARQUITECTURA.md §8). Ver ese archivo para el porqué.
builder.Services.AddMudServices();

// Cliente desnudo: lo usa el propio circuito de identidad. Sin el manejador, para que un
// 401 del refresh no dispare otro refresh.
builder.Services.AddHttpClient(ServicioDeSesion.ClienteDesnudo, cliente => cliente.BaseAddress = origen);

// Cliente general de la API: adjunta el token y reintenta una vez tras refrescar. Es el que
// van a usar el resto de las fases y la mitad B.
builder.Services.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = origen)
    .AddHttpMessageHandler<ManejadorDeAutenticacion>();

await builder.Build().RunAsync();
