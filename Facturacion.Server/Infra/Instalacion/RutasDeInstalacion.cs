using Microsoft.IdentityModel.Tokens;

namespace Facturacion.Server.Infra.Instalacion;

public sealed record RutasDeInstalacion(string Conexion, string Interna)
{
    public static RutasDeInstalacion Obtener(WebApplicationBuilder constructor)
    {
        var configuracion = constructor.Configuration;
        var juntoAlServidor = configuracion.GetValue<bool>("Instalacion:JuntoAlServidorRevision");
        if (juntoAlServidor && (!constructor.Environment.IsStaging()
            || !configuracion.GetValue<bool>("Instalacion:ConexionSinCifrarRevision")))
            throw new InvalidOperationException("La configuración junto al servidor requiere Staging y conexión legible de revisión.");
        if (juntoAlServidor && !string.IsNullOrWhiteSpace(configuracion["Instalacion:Directorio"]))
            throw new InvalidOperationException("No combines la configuración junto al servidor con una ruta de instalación externa.");
         var sitio = Path.GetFullPath(constructor.Environment.ContentRootPath);
        
         var conexion = juntoAlServidor ? Path.Combine(sitio, "cnf") : Path.GetFullPath(
            configuracion["Instalacion:Directorio"] ?? Path.Combine(sitio, "cnf"));



        var interna = juntoAlServidor ? Path.Combine(sitio, "cnf") : conexion;
        var publico = Path.GetFullPath(constructor.Environment.WebRootPath ?? Path.Combine(sitio, "wwwroot"))
            .TrimEnd(Path.DirectorySeparatorChar);
        foreach (var ruta in new[] { conexion, interna })
            if (ruta.Equals(publico, StringComparison.OrdinalIgnoreCase)
                || ruta.StartsWith(publico + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La configuración debe quedar fuera de wwwroot.");
        return new(conexion, interna);
    }
}
