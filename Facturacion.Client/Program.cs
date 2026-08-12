using Facturacion.Client;
using Facturacion.Client.Servicios.Plataforma;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var origen = new Uri(builder.HostEnvironment.BaseAddress);

// La sesión es única para toda la aplicación: el token vive en ella y solo en memoria.
builder.Services.AddSingleton<ServicioDeSesion>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, ProveedorEstadoAutenticacion>();
builder.Services.AddTransient<ManejadorDeAutenticacion>();

// Cliente desnudo: lo usa el propio circuito de identidad. Sin el manejador, para que un
// 401 del refresh no dispare otro refresh.
builder.Services.AddHttpClient(ServicioDeSesion.ClienteDesnudo, cliente => cliente.BaseAddress = origen);

// Cliente general de la API: adjunta el token y reintenta una vez tras refrescar. Es el que
// van a usar el resto de las fases y la mitad B.
builder.Services.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = origen)
    .AddHttpMessageHandler<ManejadorDeAutenticacion>();

await builder.Build().RunAsync();
