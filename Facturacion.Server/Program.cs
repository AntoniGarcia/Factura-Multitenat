using Facturacion.Server.Infra;
using Facturacion.Server.Infra.Registro;
using Facturacion.Server.Modules.Documentos;
using Facturacion.Server.Modules.Plataforma;
using Facturacion.Server.Modules.Plataforma.Auth;

// Este archivo no crece. Todo registro nuevo va dentro del *Module.cs que corresponda
// (REPARTO-EQUIPO.md §5): es uno de los cuatro archivos donde escriben las dos mitades.

var constructor = WebApplication.CreateBuilder(args);

constructor.AgregarRegistro();

constructor.Services.AddInfraestructura(constructor.Configuration);
constructor.Services.AddPlataforma(constructor.Configuration);
constructor.Services.AddDocumentos(constructor.Configuration);

var aplicacion = constructor.Build();

aplicacion.UsePipelineDeInfraestructura();

aplicacion.MapInfraestructura();
aplicacion.MapPlataforma();
aplicacion.MapDocumentos();

// Siempre al final: todo lo que no sea un endpoint de la API lo atiende el WebAssembly.
aplicacion.MapFallbackToFile("index.html");

await SembradoDeDesarrollo.SembrarAsync(aplicacion);

aplicacion.Run();
