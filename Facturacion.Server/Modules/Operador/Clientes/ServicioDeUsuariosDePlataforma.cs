using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Los usuarios de todas las cuentas, con las dos operaciones de soporte que el proveedor
/// necesita: restablecer una contraseña y dar de baja un acceso.
///
/// <para><b>Esto es poder de suplantación, y conviene decirlo</b></para>
/// Quien restablece la contraseña de un usuario puede entrar como él y emitir comprobantes a
/// su nombre. En el panel del inquilino esa operación está limitada a los usuarios que creó
/// un administrador —quien se dio de alta solo administra su propia contraseña—, y aquí esa
/// limitación no aplica: el proveedor puede hacerlo con cualquiera.
/// <para>
/// Por eso: exige la contraseña del propio operador en cada llamada, cierra todas las
/// sesiones del usuario afectado, y deja en la bitácora quién lo hizo y sobre quién. La
/// contraseña nueva no entra a la bitácora en ningún caso.
/// </para>
/// </summary>
public sealed class ServicioDeUsuariosDePlataforma(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeRefreshTokens refrescos,
    IServicioDeBitacora bitacora)
{
    public async Task<PaginaDeUsuariosDePlataforma> ListarAsync(
        string? texto, Guid? cuentaId, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = baseDeDatos.Users.AsNoTracking().AsQueryable();

        if (cuentaId is { } id)
            consulta = consulta.Where(u => u.CuentaId == id);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";

            consulta = consulta.Where(u =>
                EF.Functions.Like(u.Nombre, patron) ||
                EF.Functions.Like(u.Email!, patron));
        }

        var total = await consulta.CountAsync(ct);

        var crudos = await consulta
            .OrderBy(u => u.Nombre)
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                Correo = u.Email ?? string.Empty,
                u.Activo,
                u.FechaAltaUtc,
                u.CuentaId,
                CuentaNombre = baseDeDatos.Cuentas
                    .Where(c => c.Id == u.CuentaId)
                    .Select(c => c.Nombre)
                    .FirstOrDefault() ?? string.Empty,
                Empresas = baseDeDatos.UsuariosEmpresas.Count(ue => ue.UsuarioId == u.Id),
                u.CreadoPorAdministrador
            })
            .ToListAsync(ct);

        var elementos = crudos
            .Select(u => new UsuarioDePlataformaDto(
                u.Id, u.Nombre, u.Correo, u.Activo, u.FechaAltaUtc,
                u.CuentaId, u.CuentaNombre, u.Empresas, u.CreadoPorAdministrador))
            .ToList();

        return new PaginaDeUsuariosDePlataforma(elementos, total);
    }

    /// <summary>
    /// Pone una contraseña nueva a un usuario de cualquier cuenta y cierra todas sus sesiones.
    /// </summary>
    public async Task<Resultado<bool>> RestablecerContrasenaAsync(
        Guid operadorId,
        Guid usuarioId,
        PeticionRestablecerContrasenaDeUsuario peticion,
        CancellationToken ct)
    {
        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion(
                "contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no existe.");

        // Con el token de restablecimiento, igual que el panel del inquilino: aplica las
        // reglas de Identity en un paso y no deja al usuario un instante sin contraseña.
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, token, peticion.ContrasenaNueva);

        if (!resultado.Succeeded)
            return ErrorNegocio.Validacion(
                "contrasena-invalida",
                "La contraseña no cumple los requisitos.",
                new Dictionary<string, string[]>
                {
                    ["contrasena"] = [.. resultado.Errors.Select(e => e.Description)]
                });

        await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(
            usuarioId, "contraseña restablecida por el operador del servicio", ct);

        // Qué se hizo y sobre quién, nunca a qué: la contraseña no entra a la bitácora.
        bitacora.Registrar(
            EntidadesDeBitacora.Usuario,
            usuarioId.ToString(),
            AccionesDeBitacora.ContrasenaCambiada,
            despues: new
            {
                PorOperadorDelServicio = true,
                Usuario = usuario.Nombre,
                usuario.Email,
                SesionesCerradas = true
            },
            usuarioId: usuarioId,
            cuentaId: usuario.CuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Quita el acceso de un usuario a una empresa concreta. No se borra nada y no toca la
    /// cuenta: el <c>UsuarioEmpresa</c> pasa a <c>Activo = false</c> y el usuario deja de
    /// poder entrar a esa empresa.
    /// <para>
    /// Si era su única empresa activa, la cuenta completa se apaga y se cierran sesiones,
    /// igual que hace el panel del inquilino cuando alguien queda sin accesos: una persona
    /// sin ninguna empresa no debe poder seguir en sesión viendo nada.
    /// </para>
    /// </summary>
    public async Task<Resultado<bool>> EliminarAccesoAEmpresaAsync(
        Guid operadorId, Guid usuarioId, Guid empresaId, PeticionEliminarAccesoDeUsuario peticion, CancellationToken ct)
    {
        var miembro = await baseDeDatos.UsuariosEmpresas
            .Include(ue => ue.Usuario)
            .FirstOrDefaultAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == empresaId, ct);

        if (miembro is null)
            return ErrorNegocio.NoEncontrado(
                "acceso-no-encontrado", "Ese usuario no tiene acceso a esta empresa.");

        if (!miembro.Activo) return true;

        miembro.Activo = false;

        var tieneOtraEmpresaActiva = await baseDeDatos.UsuariosEmpresas
            .AsNoTracking()
            .AnyAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId != empresaId && ue.Activo, ct);

        if (!tieneOtraEmpresaActiva)
        {
            miembro.Usuario.Activo = false;

            await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(
                usuarioId, "acceso eliminado: sin empresas activas", ct);
        }

        bitacora.Registrar(
            EntidadesDeBitacora.UsuarioEmpresa,
            $"{usuarioId}·{empresaId}",
            AccionesDeBitacora.AccesoAEmpresaEliminadoPorOperador,
            despues: new
            {
                PorOperadorDelServicio = true,
                Usuario = miembro.Usuario.Nombre,
                miembro.Usuario.Email,
                EmpresaId = empresaId,
                Motivo = peticion.Motivo,
                CuentaApagada = !tieneOtraEmpresaActiva
            },
            usuarioId: usuarioId,
            cuentaId: miembro.Usuario.CuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Da de alta o de baja el acceso de un usuario. Al darlo de baja se cierran sus sesiones:
    /// el token que ya tenga expira solo, en quince minutos como mucho.
    /// </summary>
    public async Task<Resultado<bool>> CambiarActivoAsync(
        Guid operadorId, Guid usuarioId, PeticionCambiarActivoDeUsuario peticion, CancellationToken ct)
    {
        var usuario = await baseDeDatos.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);

        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no existe.");

        if (usuario.Activo == peticion.Activo) return true;

        usuario.Activo = peticion.Activo;

        if (!peticion.Activo)
            await refrescos.InvalidarTodasLasFamiliasDelUsuarioAsync(
                usuarioId, "acceso dado de baja por el operador del servicio", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.Usuario,
            usuarioId.ToString(),
            peticion.Activo ? AccionesDeBitacora.UsuarioReactivado : AccionesDeBitacora.UsuarioDesactivado,
            despues: new
            {
                PorOperadorDelServicio = true,
                Usuario = usuario.Nombre,
                Motivo = peticion.Motivo
            },
            usuarioId: usuarioId,
            cuentaId: usuario.CuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Cambia el correo de un usuario de cualquier cuenta. Toca la identidad de la persona y
    /// quien entra al sistema con ese correo, así que exige la contraseña del operador y deja
    /// registro de quién lo hizo y sobre quién.
    /// </summary>
    public async Task<Resultado<bool>> CambiarCorreoAsync(
        Guid operadorId, Guid usuarioId, PeticionCambiarCorreoDeUsuario peticion, CancellationToken ct)
    {
        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion(
                "contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var usuario = await baseDeDatos.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);

        if (usuario is null)
            return ErrorNegocio.NoEncontrado("usuario-no-encontrado", "Ese usuario no existe.");

        if (!EsCorreoValido(peticion.CorreoNuevo))
            return ErrorNegocio.Validacion("correo-invalido", "Escribe un correo válido.");

        var correo = peticion.CorreoNuevo.Trim().ToLowerInvariant();

        var correoOcupado = await baseDeDatos.Users.AnyAsync(
            u => u.NormalizedEmail == correo.ToUpperInvariant() && u.Id != usuario.Id, ct);

        if (correoOcupado)
            return ErrorNegocio.Validacion("correo-en-uso", "Ese correo ya lo usa otro usuario.");

        if (string.Equals(usuario.Email, correo, StringComparison.OrdinalIgnoreCase))
            return true;

        var antes = new { usuario.Email };

        // Con el gestor de Identity se actualizan a la vez Email y NormalizedEmail, y se aplican
        // las validaciones que el propio Identity exige para los correos.
        var resultado = await usuarios.SetEmailAsync(usuario, correo);

        if (!resultado.Succeeded)
            return ErrorNegocio.Validacion(
                "correo-invalido",
                "El correo no es válido.",
                new Dictionary<string, string[]>
                {
                    ["correo"] = [.. resultado.Errors.Select(e => e.Description)]
                });

        bitacora.Registrar(
            EntidadesDeBitacora.Usuario,
            usuarioId.ToString(),
            AccionesDeBitacora.CorreoDeUsuarioCambiadoPorOperador,
            antes: antes,
            despues: new
            {
                PorOperadorDelServicio = true,
                Usuario = usuario.Nombre,
                correo
            },
            usuarioId: usuarioId,
            cuentaId: usuario.CuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    private static bool EsCorreoValido(string? correo)
        => correo is not null
           && correo.Length <= 254
           && new System.Net.Mail.MailAddress(correo.Trim()).Address == correo.Trim();

    private async Task<bool> ContrasenaDelOperadorEsIncorrecta(
        Guid operadorId, string contrasena, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

        if (operador is null) return true;

        return hasher.VerifyHashedPassword(operador, operador.HashContrasena, contrasena)
            is PasswordVerificationResult.Failed;
    }
}
