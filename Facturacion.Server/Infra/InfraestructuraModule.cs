using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Facturacion.Server.Data;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Facturacion.Server.Infra;

/// <summary>
/// Todo lo transversal: tenencia, base de datos, errores, cabeceras y versión.
/// <c>Program.cs</c> solo llama a estos tres métodos (REPARTO-EQUIPO.md §5).
/// </summary>
public static class InfraestructuraModule
{
    public static IServiceCollection AddInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion, IHostEnvironment entorno)
    {
        servicios.AddHttpContextAccessor();

        // Una sola instancia por petición sirve a las dos interfaces: la de la mitad B
        // (congelada) y la extendida que usa la mitad A.
        servicios.AddScoped<IContextoEmpresaInterno, ContextoEmpresaHttp>();
        servicios.AddScoped<IContextoEmpresa>(sp => sp.GetRequiredService<IContextoEmpresaInterno>());

        // La otra identidad del sistema: el operador del SaaS. Va aparte del contexto de
        // empresa a propósito; las dos nunca están presentes en la misma petición.
        servicios.AddScoped<IContextoDeOperador, ContextoDeOperadorHttp>();

        // Por petición, no transitorio: memoriza el huso de la empresa para no volver a
        // consultarlo en cada corte de fechas de la misma pantalla.
        servicios.AddScoped<HusoDeEmpresa>();

        // AddDbContext y no AddDbContextPool: el contexto captura la empresa activa al
        // construirse, y un contexto reciclado del pool podría arrastrar la empresa de la
        // petición anterior.
        servicios.AddDbContext<AppDbContext>((sp, opciones) => opciones.Configurar(
            configuracion.GetConnectionString("BaseDeDatos"),
            sp.GetRequiredService<IContextoEmpresaInterno>()));

        servicios.AddScoped<IServicioDeBitacora, ServicioDeBitacora>();

        servicios.AgregarAlmacenCifrado(configuracion);
        servicios.AgregarCorreo(configuracion, entorno);

        servicios.AddMemoryCache();
        servicios.AddSingleton<IControlDeIntentos, ControlDeIntentos>();

        servicios.AddHostedService<PurgaDeClavesIdempotencia>();

        servicios.AddOpenApi();

        return servicios;
    }

    public static WebApplication UsePipelineDeInfraestructura(this WebApplication aplicacion)
    {
        // El más externo: nada de lo que venga después puede escapar sin convertirse en
        // Problem Details.
        aplicacion.UseMiddleware<MiddlewareDeExcepciones>();

        // Antes de los archivos estáticos: index.html y el WebAssembly son justamente lo que
        // más necesita la CSP, y UseStaticFiles corta la tubería al responder. La política
        // para el HTML se calcula sola, la primera vez que este middleware ve una respuesta
        // HTML de verdad; ver CabecerasDeSeguridad.
        aplicacion.UseMiddleware<CabecerasDeSeguridad>();

        if (!aplicacion.Environment.IsDevelopment())
            aplicacion.UseHsts();

        aplicacion.UseHttpsRedirection();
        aplicacion.UseSerilogRequestLogging();

        aplicacion.UseRouting();

        // Antes del enlace de modelo: para cuando corre el filtro de idempotencia, el cuerpo
        // ya se leyó y no se puede rebobinar si nadie lo pidió antes. Ver
        // MiddlewareDeBufferDeIdempotencia.
        aplicacion.UseMiddleware<MiddlewareDeBufferDeIdempotencia>();

        aplicacion.UseAuthentication();
        aplicacion.UseAuthorization();

        return aplicacion;
    }

    public static WebApplication MapInfraestructura(this WebApplication aplicacion)
    {
        // Reemplaza a UseBlazorFrameworkFiles()+UseStaticFiles(): esos dos sirven el archivo
        // físico tal cual está en wwwroot, sin negociar compresión por Accept-Encoding.
        // MapStaticAssets() sí lo hace, para todo lo que trae el Client referenciado
        // (_framework, _content, css, manifest...) excepto index.html: ese archivo en
        // concreto no forma parte de su manifiesto —se comprobó: cero rutas ".html" en
        // el endpoints.json publicado— y sigue sirviéndose como archivo físico, vía
        // MapFallbackToFile en Program.cs. Por qué eso hace falta arreglar aparte:
        // el Target CorregirMarcadoresDeIndexHtml en Facturacion.Server.csproj.
        aplicacion.MapStaticAssets();

        // La consulta el Client al arrancar: si la versión del servidor no coincide con la
        // suya, fuerza la recarga. Es anónima porque se consulta antes de iniciar sesión.
        aplicacion.MapGet("/api/version", () => Results.Ok(new { version = VersionCompilada() }))
            .AllowAnonymous()
            .WithName("Version");

        if (aplicacion.Environment.IsDevelopment())
            aplicacion.MapOpenApi();

        return aplicacion;
    }

    /// <summary>
    /// Data Protection con el llavero persistido fuera del repositorio y protegido por el
    /// certificado de <c>Almacen:LlaveMaestraPfx</c> (ARQUITECTURA.md §4).
    /// <para>
    /// Sin esto, el llavero por omisión de ASP.NET Core queda en claro y atado a la máquina:
    /// al reiniciar en otro contenedor, los CSD ya cargados dejarían de poder descifrarse.
    /// </para>
    /// </summary>
    private static IServiceCollection AgregarAlmacenCifrado(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection(OpcionesDeAlmacen.Seccion).Get<OpcionesDeAlmacen>() ?? new OpcionesDeAlmacen();

        // Se falla al arrancar, como con la clave de firma del JWT: descubrir que no hay
        // clave maestra el día que alguien sube su primer certificado es demasiado tarde.
        if (string.IsNullOrWhiteSpace(opciones.LlaveMaestraPfx))
            throw new InvalidOperationException(
                "Falta 'Almacen:LlaveMaestraPfx'. Genera una con " +
                "'dotnet run -- --generar-llave-maestra' y ponla en appsettings.Development.json " +
                "o en una variable de entorno; nunca en el repositorio.");

        servicios.AddOptions<OpcionesDeAlmacen>()
            .Bind(configuracion.GetSection(OpcionesDeAlmacen.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var llavero = new DirectoryInfo(opciones.RutaLlavero);
        llavero.Create();

        servicios.AddDataProtection()
            // Fijo y explícito: el propósito criptográfico no debe cambiar porque cambie el
            // nombre del ensamblado o del contenedor, o lo cifrado ayer no se lee hoy.
            .SetApplicationName("Facturacion.Plataforma")
            .PersistKeysToFileSystem(llavero)
            .ProtectKeysWithCertificate(LlaveMaestra(opciones.LlaveMaestraPfx));

        servicios.AddSingleton<IAlmacenDeArchivos, AlmacenDeArchivos>();
        servicios.AddSingleton<IProtectorDeSecretos, ProtectorDeSecretos>();

        return servicios;
    }

    /// <summary>
    /// El remitente es del SaaS, nunca del cliente (ARQUITECTURA.md §6). Sin <c>Correo:Servidor</c>
    /// fuera de <c>Development</c> no se arranca, igual que sin clave de firma del JWT: una
    /// invitación o un aviso de factura que nunca sale es un defecto que hay que ver el día
    /// del despliegue, no el día que un usuario se queja de no recibir nada.
    /// </summary>
    private static IServiceCollection AgregarCorreo(
        this IServiceCollection servicios, IConfiguration configuracion, IHostEnvironment entorno)
    {
        servicios.AddOptions<OpcionesDeCorreo>()
            .Bind(configuracion.GetSection(OpcionesDeCorreo.Seccion))
            .ValidateOnStart();

        servicios.AddOptions<OpcionesDeMensajes>()
            .Bind(configuracion.GetSection(OpcionesDeMensajes.Seccion))
            .ValidateOnStart();

        var servidorConfigurado = !string.IsNullOrWhiteSpace(
            configuracion[$"{OpcionesDeCorreo.Seccion}:{nameof(OpcionesDeCorreo.Servidor)}"]);

        if (!servidorConfigurado && !entorno.IsDevelopment())
            throw new InvalidOperationException(
                "Falta 'Correo:Servidor'. Se configura en variables de entorno; nunca en el repositorio.");

        // En Development siempre se registra Smtp: el operador puede configurar el servidor
        // desde la UI en cualquier momento. Si el servidor está vacío, SmtpClient fallará al
        // enviar, que es el comportamiento esperado (se registra en bitácora y se ignora).
        if (servidorConfigurado || entorno.IsDevelopment())
            servicios.AddScoped<IServicioDeCorreo, ServicioDeCorreoSmtp>();
        else
            servicios.AddScoped<IServicioDeCorreo, ServicioDeCorreoConsola>();

        return servicios;
    }

    private static X509Certificate2 LlaveMaestra(string pfxEnBase64)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pfxEnBase64), password: null);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "'Almacen:LlaveMaestraPfx' no es un certificado válido en base 64. " +
                "Vuelve a generarla con 'dotnet run -- --generar-llave-maestra'.", ex);
        }
    }

    private static string VersionCompilada()
    {
        var informativa = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(informativa))
            return "desconocida";

        // La versión informativa trae el hash del commit después de un '+'.
        var separador = informativa.IndexOf('+');
        return separador < 0 ? informativa : informativa[..separador];
    }
}
