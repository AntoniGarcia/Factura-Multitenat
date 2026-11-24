using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Modules.Plataforma.Usuarios;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma;

// Límites de usuarios y empresas por cuenta: los fija el operador y aquí se aplican.
internal static class LimitesDeCuenta
{
    public const int Maximo = 999;

    // Bloqueo de actualización hasta el fin de la transacción del alta: la segunda alta cuenta a la primera.
    public static Task<Cuenta?> BloquearAsync(AppDbContext baseDeDatos, Guid cuentaId, CancellationToken ct)
        => baseDeDatos.Cuentas
            .FromSqlInterpolated($"SELECT * FROM [Cuentas] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {cuentaId}")
            .FirstOrDefaultAsync(ct);

    // Ocupa lugar quien tiene acceso activo a alguna empresa, el titular también.
    public static IQueryable<Usuario> UsuariosQueCuentan(AppDbContext baseDeDatos, Guid cuentaId)
        => baseDeDatos.Users.Where(u => u.CuentaId == cuentaId && u.Activo &&
            baseDeDatos.UsuariosEmpresas.Any(ue => ue.UsuarioId == u.Id && ue.Activo));

    // Una empresa desactivada por el operador sigue ocupando lugar.
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
