using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Correo;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Configuracion;

/// <summary>
/// Administra la configuración global del SaaS. Se persiste en SQL Server para que funcione
/// igual en desarrollo y en Azure; la contraseña SMTP se cifra y nunca se devuelve al Client.
/// </summary>
public sealed class ServicioDeConfiguracionDelSistema(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    IProveedorDeConfiguracionDelSistema proveedor,
    IProtectorDeSecretosDelSistema protector)
{
    public async Task<ConfiguracionDelSistemaDto> ObtenerAsync(CancellationToken ct)
    {
        var efectiva = await proveedor.ObtenerAsync(ct);
        var correo = efectiva.Correo;
        var mensajes = efectiva.Mensajes;

        return new ConfiguracionDelSistemaDto(
            correo.Servidor,
            correo.Puerto,
            correo.Usuario,
            efectiva.ContrasenaSmtpConfigurada,
            correo.RemitenteCorreo,
            correo.RemitenteNombre,
            correo.UsarTls,
            efectiva.NombreDelSistema,
            mensajes.AsuntoVerificacion,
            mensajes.CuerpoVerificacion,
            mensajes.AsuntoContrasena,
            mensajes.CuerpoContrasena);
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

        if (peticion.Servidor.Trim().Length > 253
            || peticion.Usuario.Trim().Length > 254
            || peticion.RemitenteNombre.Trim().Length is 0 or > 128
            || peticion.NombreDelSistema.Trim().Length is 0 or > 128
            || peticion.AsuntoVerificacion.Trim().Length is 0 or > 128
            || peticion.AsuntoContrasena.Trim().Length is 0 or > 128
            || peticion.CuerpoVerificacion.Trim().Length > 8000
            || peticion.CuerpoContrasena.Trim().Length > 8000
            || peticion.ContrasenaSmtpNueva?.Length > 512)
            return ErrorNegocio.Validacion(
                "configuracion-demasiado-larga",
                "Uno o más valores de la configuración exceden la longitud permitida.");

        if (string.IsNullOrWhiteSpace(peticion.CuerpoVerificacion)
            || !peticion.CuerpoVerificacion.Contains(OpcionesDeMensajes.MarcadorCodigo, StringComparison.Ordinal))
            return ErrorNegocio.Validacion(
                "codigo-sin-marcador",
                "El mensaje del correo de verificación debe conservar la palabra CODIGO: es donde el sistema escribe el número.");

        if (string.IsNullOrWhiteSpace(peticion.CuerpoContrasena)
            || !peticion.CuerpoContrasena.Contains(OpcionesDeMensajes.MarcadorCorreo, StringComparison.Ordinal))
            return ErrorNegocio.Validacion(
                "bienvenida-sin-marcador",
                "El mensaje de bienvenida debe conservar la palabra CORREO.");

        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var guardada = await baseDeDatos.ConfiguracionesDelSistema
            .SingleOrDefaultAsync(c => c.Id == 1, ct);

        if (guardada is null)
        {
            guardada = new ConfiguracionDelSistema
            {
                Id = 1,
                ServidorSmtp = string.Empty,
                UsuarioSmtp = string.Empty,
                RemitenteCorreo = string.Empty,
                RemitenteNombre = string.Empty,
                NombreDelSistema = string.Empty,
                AsuntoVerificacion = string.Empty,
                CuerpoVerificacion = string.Empty,
                AsuntoContrasena = string.Empty,
                CuerpoContrasena = string.Empty
            };
            baseDeDatos.ConfiguracionesDelSistema.Add(guardada);
        }

        guardada.ServidorSmtp = peticion.Servidor.Trim();
        guardada.PuertoSmtp = peticion.Puerto;
        guardada.UsuarioSmtp = peticion.Usuario.Trim();
        guardada.RemitenteCorreo = peticion.RemitenteCorreo.Trim();
        guardada.RemitenteNombre = peticion.RemitenteNombre.Trim();
        guardada.UsarTls = peticion.UsarTls;
        guardada.NombreDelSistema = peticion.NombreDelSistema.Trim();
        guardada.AsuntoVerificacion = peticion.AsuntoVerificacion.Trim();
        guardada.CuerpoVerificacion = peticion.CuerpoVerificacion.Trim();
        guardada.AsuntoContrasena = peticion.AsuntoContrasena.Trim();
        guardada.CuerpoContrasena = peticion.CuerpoContrasena.Trim();
        guardada.ActualizadaUtc = DateTime.UtcNow;
        guardada.ActualizadaPorOperadorId = operadorId;

        if (!string.IsNullOrWhiteSpace(peticion.ContrasenaSmtpNueva))
            guardada.ContrasenaSmtpCifrada = protector.Cifrar(peticion.ContrasenaSmtpNueva);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
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
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > 254) return false;

        try
        {
            var normalizado = correo.Trim();
            return new System.Net.Mail.MailAddress(normalizado).Address == normalizado;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
