using Facturacion.Server.Infra;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Integracion;
using Facturacion.Server.Infra.Instalacion;
using Facturacion.Server.Infra.Registro;
using Facturacion.Server.Modules.Documentos;
using Facturacion.Server.Modules.Operador;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Server.Modules.Plataforma.Timbres;

if (args is ["--generar-llave-maestra"])
{
    GeneracionDeLlaveMaestraCli.Ejecutar();
    return;
}

while (true)
{
    var constructor = WebApplication.CreateBuilder(args);
    // En Staging local también se resuelven los estáticos del Client referenciado.
    if (constructor.Environment.IsStaging()) constructor.WebHost.UseStaticWebAssets();

    constructor.AgregarRegistro();

    await ArranqueDeRevision.PrepararAlArrancarAsync(constructor, CancellationToken.None);
    var instalacion = InstalacionModule.Preparar(constructor);                                      /////////////
    if (constructor.Configuration.GetValue<bool>("Instalacion:PrepararRevision"))
    {
        var preparada = await ArranqueDeRevision.PrepararAsync(constructor, RutasDeInstalacion.Obtener(constructor).Interna, CancellationToken.None);
        Console.WriteLine(preparada
            ? "Arranque de revisión guardado y protegido. Reinicia con Staging y el mismo directorio de instalación. PAC deshabilitado; correo pendiente de configurar."
            : "El arranque de revisión ya existe. No se reemplazaron sus claves ni su almacenamiento.");
        return;
    }
    if (!args.Any(a => a.StartsWith("--cargar-", StringComparison.Ordinal)
        || a is "--compras-pendientes" or "--acreditar-compra" or "--crear-operador" or "--cambiar-contrasena"))
        instalacion = await InstalacionModule.ComprobarOperadorAsync(constructor, instalacion);
    if (instalacion.MostrarAsistente)
    {
        if (args.Any(a => a is "--cargar-catalogos" or "--cargar-catalogos-carta-porte"
            or "--cargar-catalogo-comercio" or "--compras-pendientes" or "--acreditar-compra"
            or "--crear-operador" or "--cambiar-contrasena"))
            throw new InvalidOperationException("Completa la conexión desde el asistente antes de ejecutar comandos del sistema.");

        if (await constructor.EjecutarAsistenteAsync(instalacion)) continue;
        return;
    }

    constructor.Services.AddInfraestructura(constructor.Configuration, constructor.Environment);
    constructor.Services.AddPlataforma(constructor.Configuration);
    constructor.Services.AddOperador();
    constructor.Services.AddDocumentos(constructor.Configuration, constructor.Environment);

    var aplicacion = constructor.Build();

    if (args is ["--cargar-catalogos", var rutaCatalogos, ..])
    {
        await CargaDeCatalogosCli.EjecutarAsync(
            aplicacion, rutaCatalogos, args.Length > 2 ? args[2] : null);
        return;
    }

    if (args is ["--cargar-catalogos-carta-porte", var rutaCatalogosCartaPorte, ..])
    {
        await CargaDeCatalogosCartaPorteCli.EjecutarAsync(
            aplicacion, rutaCatalogosCartaPorte, args.Length > 2 ? args[2] : null);
        return;
    }

    if (args is ["--cargar-catalogo-comercio", var catalogoComercio, var rutaCatalogoComercio])
    {
        await CargaDeCatalogosComercioExteriorCli.EjecutarAsync(aplicacion, catalogoComercio, rutaCatalogoComercio);
        return;
    }

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

    if (args is ["--crear-operador", var correoOperador, var nombreOperador])
    {
        Environment.ExitCode = await OperadoresCli.CrearAsync(aplicacion.Services, correoOperador, nombreOperador);
        return;
    }

    if (args is ["--cambiar-contrasena", var correoOperadorCon, var contrasenaOperador])
    {
        Environment.ExitCode = await OperadoresCli.CambiarContrasenaAsync(
            aplicacion.Services, correoOperadorCon, contrasenaOperador);
        return;
    }

    aplicacion.VerificarContratos();

    aplicacion.UsePipelineDeInfraestructura();
    aplicacion.UsePreparacionDeCatalogos();

    aplicacion.MapInfraestructura();
    aplicacion.MapPlataforma();
    aplicacion.MapOperador();
    aplicacion.MapDocumentos();

    // El fallback de Blazor se registra después de todos los endpoints.
    aplicacion.MapFallbackToFile("index.html");

    await SembradoDeDesarrollo.SembrarAsync(aplicacion);

    aplicacion.Run();
    return;
}
