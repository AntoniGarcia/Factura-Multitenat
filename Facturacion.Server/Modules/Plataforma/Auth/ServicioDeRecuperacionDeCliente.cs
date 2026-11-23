using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Facturacion.Server.Infra.Seguridad.RecuperacionDeContrasena;

namespace Facturacion.Server.Modules.Plataforma.Auth;

public sealed class ServicioDeRecuperacionDeCliente(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    ColaDeRecuperacionDeCliente cola,
    ServicioDeRefreshTokens refrescos,
    IServicioDeBitacora bitacora)
{
    private static ErrorNegocio ContrasenaInvalida() => ErrorNegocio.Validacion(
        "contrasena-invalida", "Escribe de 12 a 128 caracteres. Incluye una mayúscula, una minúscula y un número.");

    public async Task<Resultado> RestablecerAsync(PeticionRestablecerContrasena peticion, CancellationToken ct)
    {
        if (peticion.ContrasenaNueva is not { Length: >= 12 and <= 128 } || string.IsNullOrWhiteSpace(peticion.ContrasenaNueva))
            return ContrasenaInvalida();

        var correo = NormalizarCorreo(peticion.Correo);
        if (correo is null || peticion.Codigo?.Trim() is not { Length: 12 } codigo ||
            cola.Validar(correo, codigo.ToUpperInvariant()) is not { } emitido)
            return CodigoInvalido();

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        // Conserva el estado de la cuenta mientras Identity actualiza la fila completa.
        var usuario = await baseDeDatos.Users
            .FromSqlInterpolated($"SELECT * FROM dbo.AspNetUsers WITH (UPDLOCK) WHERE Id = {emitido.Id}")
            .SingleOrDefaultAsync(ct);
        if (usuario is null || !ColaDeRecuperacionDeCliente.AdmiteRecuperacion(usuario) ||
            usuario.NormalizedEmail != correo || usuario.SecurityStamp != emitido.VersionDeCredencial)
            return CodigoInvalido();

        usuario.AccessFailedCount = 0;
        usuario.BloqueosConsecutivos = 0;
        usuario.LockoutEnd = null;

        // Identity valida la política, renueva el sello de seguridad y comprueba concurrencia al guardar.
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var cambio = await usuarios.ResetPasswordAsync(usuario, token, peticion.ContrasenaNueva);
        if (!cambio.Succeeded)
            return cambio.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                ? ContrasenaInvalida()
                : CodigoInvalido();

        var revocados = await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(usuario.Id, "recuperación de contraseña", ct);
        bitacora.Registrar(EntidadesDeBitacora.Usuario, usuario.Id.ToString(), AccionesDeBitacora.ContrasenaCambiada,
            despues: new { PorRecuperacion = true, TokensRevocados = revocados }, usuarioId: usuario.Id, cuentaId: usuario.CuentaId);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return Resultado.Exito();
    }
}
