using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Modules.Plataforma.Usuarios;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma;

/// <summary>
/// Cuántos usuarios y empresas puede tener una cuenta (AGENTS.md §11). El operador fija los
/// límites en la cuenta; aquí se cuentan y se aplican.
/// </summary>
internal static class LimitesDeCuenta
{
    public const int Maximo = 999;

    /// <summary>
    /// Lee la cuenta con bloqueo de actualización hasta que termine la transacción del alta:
    /// dos altas simultáneas se forman, y la segunda ya cuenta a la primera.
    /// </summary>
    public static Task<Cuenta?> BloquearAsync(AppDbContext baseDeDatos, Guid cuentaId, CancellationToken ct)
        => baseDeDatos.Cuentas
            .FromSqlInterpolated($"SELECT * FROM [Cuentas] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {cuentaId}")
            .FirstOrDefaultAsync(ct);

    /// <summary>Un usuario ocupa lugar mientras tenga acceso activo a alguna empresa. El titular también.</summary>
    public static IQueryable<Usuario> UsuariosQueCuentan(AppDbContext baseDeDatos, Guid cuentaId)
        => baseDeDatos.Users.Where(u => u.CuentaId == cuentaId && u.Activo &&
            baseDeDatos.UsuariosEmpresas.Any(ue => ue.UsuarioId == u.Id && ue.Activo));

    /// <summary>Una empresa desactivada por el operador sigue ocupando lugar.</summary>
    public static Task<int> EmpresasEnUsoAsync(AppDbContext baseDeDatos, Guid cuentaId, CancellationToken ct)
        => baseDeDatos.Empresas.CountAsync(e => e.CuentaId == cuentaId, ct);

    public static ErrorNegocio ErrorDeUsuarios(int limite, OpcionesDeSoporte soporte)
        => ErrorNegocio.LimiteExcedido(
            "limite-de-usuarios",
            $"Tu cuenta ya tiene el máximo de {limite} {(limite == 1 ? "usuario activo" : "usuarios activos")}, " +
            $"contándote a ti. Para agregar más, contacta a soporte.{Contacto(soporte)}");

    public static ErrorNegocio ErrorDeEmpresas(int limite, OpcionesDeSoporte soporte)
        => ErrorNegocio.LimiteExcedido(
            "limite-de-empresas",
            $"Tu cuenta ya tiene el máximo de {limite} {(limite == 1 ? "empresa" : "empresas")}. " +
            $"Para agregar otra, contacta a soporte.{Contacto(soporte)}");

    private static string Contacto(OpcionesDeSoporte soporte) => (soporte.Correo, soporte.Telefono) switch
    {
        ({ } correo, { } telefono) => $" Comunícate a {correo} o al {telefono}.",
        ({ } correo, null) => $" Comunícate a {correo}.",
        (null, { } telefono) => $" Comunícate al {telefono}.",
        _ => string.Empty
    };
}
