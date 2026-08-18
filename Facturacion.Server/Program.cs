using Facturacion.Server.Infra;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Integracion;
using Facturacion.Server.Infra.Registro;
using Facturacion.Server.Modules.Documentos;
using Facturacion.Server.Modules.Plataforma;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Server.Modules.Plataforma.Timbres;

// Este archivo no crece. Todo registro nuevo va dentro del *Module.cs que corresponda
// (REPARTO-EQUIPO.md §5): es uno de los cuatro archivos donde escriben las dos mitades.

// Antes de construir nada: solo imprime un certificado nuevo y termina. No necesita base de
// datos ni configuración, que es justo lo que hace falta cuando todavía no hay clave maestra.
if (args is ["--generar-llave-maestra"])
{
    GeneracionDeLlaveMaestraCli.Ejecutar();
    return;
}

var constructor = WebApplication.CreateBuilder(args);

constructor.AgregarRegistro();

constructor.Services.AddInfraestructura(constructor.Configuration, constructor.Environment);
constructor.Services.AddPlataforma(constructor.Configuration);
constructor.Services.AddDocumentos(constructor.Configuration, constructor.Environment);

var aplicacion = constructor.Build();

// Comando de carga de catálogos del SAT (CLAUDE.md §7): no levanta el servidor, solo usa el
// contenedor de dependencias ya armado para llegar a la base. La lógica vive en
// CargaDeCatalogosCli, no aquí.
// Con un tercer argumento se recarga solo ese catálogo; sin él, los veintiuno.
if (args is ["--cargar-catalogos", var rutaCatalogos, ..])
{
    await CargaDeCatalogosCli.EjecutarAsync(
        aplicacion, rutaCatalogos, args.Length > 2 ? args[2] : null);
    return;
}

// Acreditación manual del pago de una compra de timbres (fase 7). Es una operación del
// operador del SaaS, no del inquilino: crea saldo, así que no puede vivir como endpoint
// mientras no exista una identidad de operador. Ver AcreditacionDeCompraCli.
if (args is ["--compras-pendientes"])
{
    Environment.ExitCode = await AcreditacionDeCompraCli.ListarPendientes(aplicacion.Services);
    return;
}

if (args is ["--acreditar-compra", var compraId])
{
    Environment.ExitCode = await AcreditacionDeCompraCli.Acreditar(aplicacion.Services, compraId);
    return;
}

// Antes de atender la primera petición: si un doble de prueba quedó registrado fuera de
// Development, aquí revienta. Un doble que llega a producción no se nota — el sistema
// responde y los datos son inventados (fase 9).
aplicacion.VerificarContratos();

aplicacion.UsePipelineDeInfraestructura();

aplicacion.MapInfraestructura();
aplicacion.MapPlataforma();
aplicacion.MapDocumentos();

// Siempre al final: todo lo que no sea un endpoint de la API lo atiende el WebAssembly.
// WithStaticAssets() existe pero está atado a RazorComponentsEndpointConventionBuilder
// (MapRazorComponents), no a MapFallbackToFile: no aplica a este hosting clásico de WASM.
// index.html no forma parte del manifiesto de MapStaticAssets() —se comprobó: cero rutas
// ".html" en él—, así que sigue sirviéndose como archivo físico; ver el Target
// CorregirMarcadoresDeIndexHtml en Facturacion.Server.csproj para el porqué hace falta.
aplicacion.MapFallbackToFile("index.html");

await SembradoDeDesarrollo.SembrarAsync(aplicacion);

aplicacion.Run();
