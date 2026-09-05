using System.Text.Json;
using System.Text.Json.Nodes;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Facturacion.Server.Modules.Operador.Configuracion;

/// <summary>
/// Lee y escribe la configuración del sistema (SMTP y nombre) directamente en
/// appsettings.Development.json. Es una operación del operador del SaaS, no del inquilino:
/// solo quien puede acreditar pagos debería poder cambiar el remitente de los correos.
/// <para>
/// Después de escribir el archivo se llama a <c>IConfigurationRoot.Reload()</c> para que los
/// cambios apliquen sin reiniciar el proceso. Los servicios que usan <c>IOptionsMonitor</c>
/// (como <see cref="ServicioDeCorreoSmtp"/>) ven el nuevo valor en la siguiente llamada.
/// </para>
/// </summary>
public sealed class ServicioDeConfiguracionDelSistema(
    AppDbContext baseDeDatos,
    IConfiguration configuracion,
    IPasswordHasher<OperadorPlataforma> hasher,
    IWebHostEnvironment entorno)
{
    public async Task<ConfiguracionDelSistemaDto?> ObtenerAsync(CancellationToken ct)
    {
        var ruta = RutaDelArchivo();
        if (!File.Exists(ruta)) return null;

        var texto = await File.ReadAllTextAsync(ruta, ct);
        var nodo = JsonNode.Parse(texto);
        if (nodo is null) return null;

        var correo = nodo["Correo"] ?? new JsonObject();
        var sistema = nodo["Sistema"] ?? new JsonObject();

        return new ConfiguracionDelSistemaDto(
            Servidor: correo["Servidor"]?.GetValue<string>() ?? string.Empty,
            Puerto: correo["Puerto"]?.GetValue<int>() ?? 587,
            Usuario: correo["Usuario"]?.GetValue<string>() ?? string.Empty,
            Contrasena: correo["Contrasena"]?.GetValue<string>() ?? string.Empty,
            RemitenteCorreo: correo["RemitenteCorreo"]?.GetValue<string>() ?? string.Empty,
            RemitenteNombre: correo["RemitenteNombre"]?.GetValue<string>() ?? "Sistema de facturación",
            UsarTls: correo["UsarTls"]?.GetValue<bool>() ?? true,
            NombreDelSistema: sistema["Nombre"]?.GetValue<string>() ?? "Sistema de facturación");
    }

    public async Task<Resultado<bool>> GuardarAsync(
        Guid operadorId, PeticionGuardarConfiguracionDelSistema peticion, CancellationToken ct)
    {
        if (!EsCorreoValido(peticion.RemitenteCorreo))
            return ErrorNegocio.Validacion("correo-invalido", "El correo del remitente no es válido.");

        if (peticion.Puerto is < 1 or > 65535)
            return ErrorNegocio.Validacion("puerto-invalido", "El puerto debe estar entre 1 y 65535.");

        if (string.IsNullOrWhiteSpace(peticion.Servidor))
            return ErrorNegocio.Validacion("servidor-requerido", "Escribe el servidor SMTP.");

        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var ruta = RutaDelArchivo();
        if (!File.Exists(ruta))
            return ErrorNegocio.Regla("configuracion-no-encontrada", "No se encontró appsettings.Development.json.");

        var texto = await File.ReadAllTextAsync(ruta, ct);
        var nodo = JsonNode.Parse(texto) ?? new JsonObject();

        // Sección Correo
        nodo["Correo"] ??= new JsonObject();
        nodo["Correo"]!["Servidor"] = peticion.Servidor;
        nodo["Correo"]!["Puerto"] = peticion.Puerto;
        nodo["Correo"]!["Usuario"] = peticion.Usuario;
        nodo["Correo"]!["Contrasena"] = peticion.Contrasena;
        nodo["Correo"]!["RemitenteCorreo"] = peticion.RemitenteCorreo;
        nodo["Correo"]!["RemitenteNombre"] = peticion.RemitenteNombre;
        nodo["Correo"]!["UsarTls"] = peticion.UsarTls;

        // Sección Sistema
        nodo["Sistema"] ??= new JsonObject();
        nodo["Sistema"]!["Nombre"] = peticion.NombreDelSistema;

        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var textoNuevo = nodo.ToJsonString(opciones);
        await File.WriteAllTextAsync(ruta, textoNuevo, ct);

        // Recargar la configuración para que los cambios apliquen al vuelo.
        if (configuracion is IConfigurationRoot root)
            root.Reload();

        return true;
    }

    private string RutaDelArchivo()
    {
        // En desarrollo, appsettings.Development.json está junto al bin, no junto al .csproj.
        // Pero el WorkingDirectory del proceso es la raíz del proyecto (o la del publish).
        // La forma más fiable es buscar desde ContentRoot.
        var nombre = "appsettings.Development.json";
        var ruta = Path.Combine(entorno.ContentRootPath, nombre);
        if (File.Exists(ruta)) return ruta;

        // Fallback: subir desde ContentRoot hasta encontrar el archivo.
        var dir = new DirectoryInfo(entorno.ContentRootPath);
        for (var i = 0; i < 5 && dir is not null; i++)
        {
            var candidata = Path.Combine(dir.FullName, nombre);
            if (File.Exists(candidata)) return candidata;
            dir = dir.Parent;
        }

        return ruta;
    }

    private async Task<bool> ContrasenaDelOperadorEsIncorrecta(
        Guid operadorId, string contrasena, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

        if (operador is null) return true;

        return hasher.VerifyHashedPassword(operador, operador.HashContrasena, contrasena)
            is PasswordVerificationResult.Failed;
    }

    private static bool EsCorreoValido(string? correo)
        => correo is not null
           && correo.Length <= 254
           && new System.Net.Mail.MailAddress(correo.Trim()).Address == correo.Trim();
}
