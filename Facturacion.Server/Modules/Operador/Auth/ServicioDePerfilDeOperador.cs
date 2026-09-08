using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Lo que el operador puede cambiar de sí mismo: su nombre, su correo de acceso y su
/// contraseña.
///
/// <para><b>Los tres cambios piden la contraseña actual salvo el nombre</b></para>
/// El correo es la llave con la que se entra y la contraseña es el secreto: cambiarlos desde
/// una sesión que alguien dejó abierta equivaldría a quedarse con la cuenta. Pedir la
/// contraseña actual convierte "tener la pantalla delante" en "saber la credencial".
/// </summary>
public sealed class ServicioDePerfilDeOperador(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeRefreshTokensDeOperador refrescos,
    IServicioDeBitacora bitacora)
{
    public async Task<Resultado<SesionDeOperadorDto>> ActualizarNombreAsync(
        Guid operadorId, PeticionActualizarPerfilOperador peticion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre))
            return ErrorNegocio.Validacion("nombre-requerido", "Escribe tu nombre.");

        var operador = await Buscar(operadorId, ct);

        if (operador is null) return NoEncontrado();

        operador.Nombre = peticion.Nombre.Trim();

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operadorId.ToString(),
            AccionesDeBitacora.PerfilActualizado,
            despues: new { operador.Nombre },
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(operador);
    }

    public async Task<Resultado<SesionDeOperadorDto>> CambiarCorreoAsync(
        Guid operadorId, PeticionCambiarCorreoOperador peticion, CancellationToken ct)
    {
        var operador = await Buscar(operadorId, ct);

        if (operador is null) return NoEncontrado();

        if (!ContrasenaCorrecta(operador, peticion.ContrasenaActual))
            return ErrorNegocio.Validacion(
                "contrasena-incorrecta", "La contraseña actual no es correcta.");

        var normalizado = peticion.Correo.Trim().ToUpperInvariant();

        if (normalizado == operador.CorreoNormalizado)
            return ADto(operador);

        var ocupado = await baseDeDatos.OperadoresPlataforma
            .AnyAsync(o => o.CorreoNormalizado == normalizado && o.Id != operadorId, ct);

        if (ocupado)
            return ErrorNegocio.Regla("correo-ocupado", "Ya hay otro operador con ese correo.");

        var anterior = operador.Correo;

        operador.Correo = peticion.Correo.Trim();
        operador.CorreoNormalizado = normalizado;

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operadorId.ToString(),
            AccionesDeBitacora.CorreoCambiado,
            antes: new { Correo = anterior },
            despues: new { operador.Correo },
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(operador);
    }

    /// <summary>
    /// Cambia la contraseña y <b>cierra las demás sesiones</b>: si la cambia porque cree que
    /// alguien se la sabe, dejar vivas las sesiones abiertas con la anterior no serviría de
    /// nada. La sesión desde la que se hace el cambio también cae, y se vuelve a entrar.
    /// </summary>
    public async Task<Resultado<bool>> CambiarContrasenaAsync(
        Guid operadorId, PeticionCambiarContrasenaOperador peticion, CancellationToken ct)
    {
        if (peticion.ContrasenaNueva.Length < 12)
            return ErrorNegocio.Validacion(
                "contrasena-corta", "La contraseña nueva necesita al menos 12 caracteres.");

        var operador = await Buscar(operadorId, ct);

        if (operador is null) return NoEncontrado();

        if (!ContrasenaCorrecta(operador, peticion.ContrasenaActual))
            return ErrorNegocio.Validacion(
                "contrasena-incorrecta", "La contraseña actual no es correcta.");

        operador.HashContrasena = hasher.HashPassword(operador, peticion.ContrasenaNueva);

        var familias = await baseDeDatos.RefreshTokensOperador
            .Where(t => t.OperadorId == operadorId && t.RevocadoUtc == null)
            .Select(t => t.FamiliaId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var familia in familias)
            await refrescos.InvalidarFamiliaAsync(familia, "cambio de contraseña", ct);

        bitacora.Registrar(
            EntidadesDeBitacora.OperadorPlataforma,
            operadorId.ToString(),
            AccionesDeBitacora.ContrasenaCambiada,
            despues: new { SesionesCerradas = familias.Count },
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    private Task<OperadorPlataforma?> Buscar(Guid operadorId, CancellationToken ct)
        => baseDeDatos.OperadoresPlataforma
            .Include(o => o.Permisos)
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

    private static SesionDeOperadorDto ADto(OperadorPlataforma operador)
        => new(operador.Id, operador.Nombre, operador.Correo, [.. operador.Permisos.Select(p => p.Permiso)]);

    private bool ContrasenaCorrecta(OperadorPlataforma operador, string contrasena)
        => hasher.VerifyHashedPassword(operador, operador.HashContrasena, contrasena)
            is not PasswordVerificationResult.Failed;

    private static ErrorNegocio NoEncontrado()
        => ErrorNegocio.NoEncontrado("operador-no-encontrado", "No se encontró tu usuario.");
}
