using Facturacion.Server.Data;
using Facturacion.Server.Infra.Almacen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Resuelve la configuración global persistida por el operador. Mientras aún no exista el
/// renglón único, usa appsettings o variables de entorno como valores iniciales.
/// </summary>
public interface IProveedorDeConfiguracionDelSistema
{
    Task<ConfiguracionEfectivaDelSistema> ObtenerAsync(CancellationToken ct);
}

public sealed record ConfiguracionEfectivaDelSistema(
    OpcionesDeCorreo Correo,
    OpcionesDeMensajes Mensajes,
    string NombreDelSistema,
    bool ContrasenaSmtpConfigurada);

public sealed class ProveedorDeConfiguracionDelSistema(
    AppDbContext baseDeDatos,
    IOptionsMonitor<OpcionesDeCorreo> correoInicial,
    IOptionsMonitor<OpcionesDeMensajes> mensajesIniciales,
    IConfiguration configuracion,
    IProtectorDeSecretosDelSistema protector) : IProveedorDeConfiguracionDelSistema
{
    public async Task<ConfiguracionEfectivaDelSistema> ObtenerAsync(CancellationToken ct)
    {
        var guardada = await baseDeDatos.ConfiguracionesDelSistema
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == 1, ct);

        if (guardada is null)
        {
            var correo = correoInicial.CurrentValue;
            return new ConfiguracionEfectivaDelSistema(
                correo,
                mensajesIniciales.CurrentValue,
                configuracion["Sistema:Nombre"] ?? "Sistema de facturación",
                !string.IsNullOrEmpty(correo.Contrasena));
        }

        var contrasena = guardada.ContrasenaSmtpCifrada is null
            ? correoInicial.CurrentValue.Contrasena
            : protector.Descifrar(guardada.ContrasenaSmtpCifrada);

        var correoGuardado = new OpcionesDeCorreo
        {
            Servidor = guardada.ServidorSmtp,
            Puerto = guardada.PuertoSmtp,
            Usuario = guardada.UsuarioSmtp,
            Contrasena = contrasena,
            RemitenteCorreo = guardada.RemitenteCorreo,
            RemitenteNombre = guardada.RemitenteNombre,
            UsarTls = guardada.UsarTls
        };

        var mensajesGuardados = new OpcionesDeMensajes
        {
            AsuntoVerificacion = guardada.AsuntoVerificacion,
            CuerpoVerificacion = guardada.CuerpoVerificacion,
            AsuntoContrasena = guardada.AsuntoContrasena,
            CuerpoContrasena = guardada.CuerpoContrasena
        };

        return new ConfiguracionEfectivaDelSistema(
            correoGuardado,
            mensajesGuardados,
            guardada.NombreDelSistema,
            !string.IsNullOrEmpty(contrasena));
    }
}
