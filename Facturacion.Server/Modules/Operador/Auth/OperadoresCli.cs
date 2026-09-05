using System.Security.Cryptography;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Alta de operadores del SaaS desde la consola.
///
/// <para><b>Por qué consola y no una pantalla</b></para>
/// Un operador puede acreditar pagos, o sea crear saldo de la nada. Quien decide que exista
/// otro es el dueño del sistema, y su credencial para hacerlo es tener acceso al servidor:
/// exactamente quien debe tenerla. Una pantalla de alta —aunque estuviera dentro del panel—
/// convertiría el robo de una sesión de operador en el robo permanente de la plataforma.
///
/// <para>
/// La contraseña se genera aquí y se imprime una sola vez. No se guarda en claro ni se envía
/// por correo; quien la recibe la cambia en cuanto entre.
/// </para>
/// </summary>
public static class OperadoresCli
{
    public static async Task<int> CrearAsync(IServiceProvider servicios, string correo, string nombre)
    {
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
        {
            Console.Error.WriteLine("El correo no es válido.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            Console.Error.WriteLine("Falta el nombre del operador.");
            return 1;
        }

        using var ambito = servicios.CreateScope();
        var proveedor = ambito.ServiceProvider;

        var baseDeDatos = proveedor.GetRequiredService<AppDbContext>();
        var hasher = proveedor.GetRequiredService<IPasswordHasher<OperadorPlataforma>>();

        var normalizado = correo.Trim().ToUpperInvariant();

        if (await baseDeDatos.OperadoresPlataforma.AnyAsync(o => o.CorreoNormalizado == normalizado))
        {
            Console.Error.WriteLine($"Ya existe un operador con el correo {correo}.");
            return 1;
        }

        var contrasena = GenerarContrasena();

        var operador = new OperadorPlataforma
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Correo = correo.Trim(),
            CorreoNormalizado = normalizado,
            HashContrasena = string.Empty,
            Activo = true,
            FechaAltaUtc = DateTime.UtcNow,
            // Quien se da de alta por consola es el dueño del SaaS: nace con los seis permisos,
            // igual que el principal. Si hiciera falta un operador con menos, se le quitan luego
            // desde el panel (ARQUITECTURA.md §4).
            Permisos = Permisos.Todos.Select(p => new PermisoOperador { Permiso = p }).ToList()
        };

        operador.HashContrasena = hasher.HashPassword(operador, contrasena);

        baseDeDatos.OperadoresPlataforma.Add(operador);
        await baseDeDatos.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("Operador creado.");
        Console.WriteLine($"  Correo:     {operador.Correo}");
        Console.WriteLine($"  Contraseña: {contrasena}");
        Console.WriteLine();
        Console.WriteLine("Esta contraseña no se vuelve a mostrar. Entrégala por un medio seguro.");
        Console.WriteLine();

        return 0;
    }

    /// <summary>
    /// Veinticuatro caracteres de un alfabeto sin parecidos visuales: nadie tiene que teclear
    /// esto adivinando si es un uno o una ele.
    /// </summary>
    private static string GenerarContrasena()
    {
        const string alfabeto = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789#$%+=?";

        return RandomNumberGenerator.GetString(alfabeto, 24);
    }
}
