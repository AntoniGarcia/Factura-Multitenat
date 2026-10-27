using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Infra.Seguridad;

internal static class ConfirmacionDeTitular
{
    internal static async Task<Resultado<Usuario>> ValidarAsync(
        UserManager<Usuario> usuarios, IContextoEmpresaInterno contexto,
        IControlDeIntentos intentos, string contrasena)
    {
        var claveIntentos = $"confirmacion-titular:{contexto.UsuarioActual}";
        if (intentos.BloqueoRestante(claveIntentos) is not null)
            return ErrorNegocio.LimiteExcedido("demasiados-intentos", "Espera antes de volver a confirmar tu contraseña.");

        var titular = contexto.UsuarioActual is { } id ? await usuarios.FindByIdAsync(id.ToString()) : null;
        if (titular is null || !titular.Activo || !titular.EsTitular ||
            titular.CuentaId != contexto.CuentaActual || await usuarios.IsLockedOutAsync(titular))
            return ErrorNegocio.Regla("acceso-no-autorizado", "Solo el titular puede autorizar esta eliminación.");

        if (string.IsNullOrEmpty(contrasena) || contrasena.Length > 128 ||
            !await usuarios.CheckPasswordAsync(titular, contrasena))
        {
            intentos.RegistrarFallo(claveIntentos);
            await usuarios.AccessFailedAsync(titular);
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña no es correcta.");
        }
        intentos.Limpiar(claveIntentos);
        return titular;
    }
}
