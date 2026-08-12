using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Crea una cuenta con dos empresas y un usuario para poder probar el circuito de identidad
/// de punta a punta. El alta real de cuentas y empresas es la fase 4; hasta entonces no hay
/// ninguna forma de entrar al sistema.
///
/// <para><b>Por qué no puede colarse a producción</b></para>
/// Corre solo si el entorno es <c>Development</c> <b>y</b> si existen <c>Sembrado:Correo</c>
/// y <c>Sembrado:Contrasena</c> en configuración. Esas claves viven en
/// <c>appsettings.Development.json</c>, que está en el <c>.gitignore</c>: en un despliegue no
/// existen, así que el sembrado no hace nada aunque alguien forzara el entorno.
///
/// <para>
/// Son dos empresas y no una a propósito: con una sola, el token sale con ella activa y el
/// cambio de empresa nunca se ejercita.
/// </para>
/// </summary>
public static class SembradoDeDesarrollo
{
    public static async Task SembrarAsync(WebApplication aplicacion)
    {
        if (!aplicacion.Environment.IsDevelopment()) return;

        var correo = aplicacion.Configuration["Sembrado:Correo"];
        var contrasena = aplicacion.Configuration["Sembrado:Contrasena"];

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena)) return;

        using var ambito = aplicacion.Services.CreateScope();
        var proveedor = ambito.ServiceProvider;

        var baseDeDatos = proveedor.GetRequiredService<AppDbContext>();
        var registro = proveedor.GetRequiredService<ILogger<Program>>();

        // Idempotente: si ya hay una cuenta, no se toca nada.
        if (await baseDeDatos.Cuentas.AnyAsync()) return;

        var usuarios = proveedor.GetRequiredService<UserManager<Usuario>>();
        var ahora = DateTime.UtcNow;

        var cuenta = new Cuenta
        {
            Id = Guid.NewGuid(),
            Nombre = "Cuenta de desarrollo",
            CorreoContacto = correo,
            FechaAltaUtc = ahora
        };

        var llantera = NuevaEmpresa(cuenta.Id, "LLANTERA DEL CENTRO", "LCE010101AA1", ahora);
        var cementera = NuevaEmpresa(cuenta.Id, "CEMENTERA DEL BAJIO", "CBA010101BB2", ahora);

        baseDeDatos.Cuentas.Add(cuenta);
        baseDeDatos.Empresas.AddRange(llantera, cementera);
        await baseDeDatos.SaveChangesAsync();

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Contador de desarrollo",
            CuentaId = cuenta.Id,
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
            FechaAltaUtc = ahora
        };

        var alta = await usuarios.CreateAsync(usuario, contrasena);

        if (!alta.Succeeded)
        {
            registro.LogWarning(
                "No se pudo sembrar el usuario de desarrollo: {Errores}",
                string.Join("; ", alta.Errors.Select(e => e.Description)));
            return;
        }

        foreach (var empresa in new[] { llantera, cementera })
        {
            baseDeDatos.UsuariosEmpresas.Add(new UsuarioEmpresa
            {
                UsuarioId = usuario.Id,
                EmpresaId = empresa.Id,
                FechaAltaUtc = ahora
            });

            baseDeDatos.UsuariosEmpresasPermisos.AddRange(Permisos.Todos.Select(permiso =>
                new UsuarioEmpresaPermiso
                {
                    UsuarioId = usuario.Id,
                    EmpresaId = empresa.Id,
                    PermisoClave = permiso,
                    OtorgadoUtc = ahora
                }));
        }

        await baseDeDatos.SaveChangesAsync();

        // Sin la contraseña, que la trae quien configuró el sembrado.
        registro.LogInformation(
            "Sembrado de desarrollo listo: cuenta con dos empresas y el usuario {Correo}", correo);
    }

    private static Empresa NuevaEmpresa(Guid cuentaId, string nombre, string rfc, DateTime ahora) => new()
    {
        Id = Guid.NewGuid(),
        CuentaId = cuentaId,
        Rfc = rfc,
        NombreFiscal = nombre,
        RegimenFiscal = "601",
        CodigoPostalExpedicion = "42000",
        ZonaHoraria = "Central Standard Time (Mexico)",
        FechaAltaUtc = ahora
    };
}
