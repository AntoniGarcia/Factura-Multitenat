using System.Net.Mail;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Alta y edición del servidor SMTP propio de la empresa activa (AGENTS.md §11).
/// <para>
/// La contraseña es de solo escritura: entra cifrada y nunca sale. Una petición sin contraseña
/// conserva la guardada, para que editar el puerto no obligue a volver a escribirla.
/// </para>
/// </summary>
public sealed class ServicioDeConfiguracionDeCorreo(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IProtectorDeContrasenaSmtp protector,
    IServicioDeBitacora bitacora)
{
    private const int LongitudMaximaUsuario = 254;
    private const int LongitudMaximaContrasena = 512;
    private const int LongitudMaximaNombre = 128;

    public async Task<CorreoDeEmpresaDto> ObtenerAsync(CancellationToken ct)
    {
        var correo = await baseDeDatos.CorreosDeEmpresa.AsNoTracking().FirstOrDefaultAsync(ct);

        return correo is null
            ? new CorreoDeEmpresaDto(false, null, PuertosSmtp.StartTls, null, false, null, null, null, null)
            : ADto(correo);
    }

    public async Task<Resultado<CorreoDeEmpresaDto>> GuardarAsync(
        PeticionGuardarCorreoDeEmpresa peticion, CancellationToken ct)
    {
        var servidor = Limpiar(peticion.Servidor)?.ToLowerInvariant();
        var usuario = Limpiar(peticion.Usuario);
        var remitenteNombre = Limpiar(peticion.RemitenteNombre);
        var remitenteCorreo = Limpiar(peticion.RemitenteCorreo);
        var nombreRemitenteSistema = Limpiar(peticion.NombreRemitenteSistema);
        var responderA = Limpiar(peticion.ResponderA);
        var contrasenaNueva = string.IsNullOrEmpty(peticion.Contrasena) ? null : peticion.Contrasena;

        var correo = await baseDeDatos.CorreosDeEmpresa.FirstOrDefaultAsync(ct);
        var tieneContrasena = contrasenaNueva is not null || !string.IsNullOrEmpty(correo?.ContrasenaCifrada);

        var error = Validar(peticion, servidor, usuario, contrasenaNueva, remitenteNombre, remitenteCorreo, tieneContrasena)
                    ?? ValidarRemitenteDelSistema(nombreRemitenteSistema, responderA);
        if (error is not null) return error;

        // Solo se exige un destino alcanzable cuando se va a usar: guardar los datos con el
        // switch apagado no tiene por qué depender del DNS.
        if (peticion.Habilitado)
        {
            var destino = await DestinoSmtp.ResolverAsync(servidor!, ct);
            if (destino.EsFallo) return destino.Error!;
        }

        var antes = correo is null ? null : ADto(correo);

        if (correo is null)
        {
            // EmpresaId lo pone el sellado de empresa, igual que en ConfiguracionEmpresa.
            correo = new CorreoDeEmpresa();
            baseDeDatos.CorreosDeEmpresa.Add(correo);
        }

        correo.Habilitado = peticion.Habilitado;
        correo.Servidor = servidor;
        correo.Puerto = peticion.Puerto;
        correo.Usuario = usuario;
        correo.RemitenteNombre = remitenteNombre;
        correo.RemitenteCorreo = remitenteCorreo;
        correo.NombreRemitenteSistema = nombreRemitenteSistema;
        correo.ResponderA = responderA;

        if (contrasenaNueva is not null)
            correo.ContrasenaCifrada = protector.Cifrar(contexto.EmpresaId, contrasenaNueva);

        var despues = ADto(correo);

        // La bitácora dice si la contraseña cambió, nunca cuál es.
        bitacora.Registrar(
            EntidadesDeBitacora.CorreoDeEmpresa, contexto.EmpresaId.ToString(),
            AccionesDeBitacora.CorreoDeEmpresaActualizado,
            antes, new { Datos = despues, ContrasenaCambiada = contrasenaNueva is not null });

        await baseDeDatos.SaveChangesAsync(ct);

        return despues;
    }

    private static ErrorNegocio? Validar(
        PeticionGuardarCorreoDeEmpresa peticion, string? servidor, string? usuario, string? contrasena,
        string? remitenteNombre, string? remitenteCorreo, bool tieneContrasena)
    {
        if (!PuertosSmtp.EsAdmitido(peticion.Puerto))
            return ErrorNegocio.Validacion("smtp-puerto-no-admitido",
                $"El puerto va en {PuertosSmtp.SslDirecto} (SSL directo) o {PuertosSmtp.StartTls} (STARTTLS).");

        if (servidor is not null && !DestinoSmtp.EsNombreValido(servidor))
            return ErrorNegocio.Validacion("smtp-servidor-invalido", "El servidor SMTP no tiene un formato válido.");

        if (usuario is not null && (usuario.Length > LongitudMaximaUsuario || TieneSaltoDeLinea(usuario)))
            return ErrorNegocio.Validacion("smtp-usuario-invalido",
                $"El usuario admite hasta {LongitudMaximaUsuario} caracteres en una sola línea.");

        if (contrasena is not null && contrasena.Length > LongitudMaximaContrasena)
            return ErrorNegocio.Validacion("smtp-contrasena-invalida",
                $"La contraseña admite hasta {LongitudMaximaContrasena} caracteres.");

        // Un salto de línea en el nombre del remitente es cómo se inyectan encabezados.
        if (remitenteNombre is not null && (remitenteNombre.Length > LongitudMaximaNombre || TieneSaltoDeLinea(remitenteNombre)))
            return ErrorNegocio.Validacion("smtp-remitente-nombre-invalido",
                $"El nombre del remitente admite hasta {LongitudMaximaNombre} caracteres en una sola línea.");

        if (remitenteCorreo is not null && !EsCorreoValido(remitenteCorreo))
            return ErrorNegocio.Validacion("smtp-remitente-invalido",
                "El correo del remitente no es válido. Escribe solo la dirección, como facturas@miempresa.com.");

        if (!peticion.Habilitado) return null;

        if (servidor is null)
            return ErrorNegocio.Validacion("smtp-servidor-requerido", "Escribe el servidor SMTP.");

        if (usuario is null)
            return ErrorNegocio.Validacion("smtp-usuario-requerido", "Escribe el usuario del servidor de correo.");

        if (!tieneContrasena)
            return ErrorNegocio.Validacion("smtp-contrasena-requerida", "Escribe la contraseña del servidor de correo.");

        if (remitenteCorreo is null)
            return ErrorNegocio.Validacion("smtp-remitente-requerido", "Escribe el correo del remitente.");

        return null;
    }

    private static ErrorNegocio? ValidarRemitenteDelSistema(string? nombre, string? responderA)
    {
        // Sin «@»: con la dirección del sistema detrás, un nombre como «pagos@banco.com» se lee
        // en la bandeja como si fuera el remitente real.
        if (nombre is not null && (nombre.Length > LongitudMaximaNombre || TieneSaltoDeLinea(nombre) || nombre.Contains('@')))
            return ErrorNegocio.Validacion("remitente-sistema-nombre-invalido",
                $"El nombre del remitente admite hasta {LongitudMaximaNombre} caracteres en una sola línea y sin «@».");

        if (responderA is not null && !EsCorreoValido(responderA))
            return ErrorNegocio.Validacion("responder-a-invalido",
                "El correo para responder no es válido. Escribe solo la dirección, como contacto@miempresa.com.");

        return null;
    }

    /// <summary>
    /// Solo la dirección, sin nombre ni ángulos: el nombre va en su propio campo. Comparar contra
    /// lo que interpretó <see cref="MailAddress"/> descarta las formas que admite y no se quieren.
    /// </summary>
    private static bool EsCorreoValido(string correo)
        => correo.Length <= 254 && !TieneSaltoDeLinea(correo) &&
           MailAddress.TryCreate(correo, out var direccion) && direccion.Address == correo;

    private static bool TieneSaltoDeLinea(string texto) => texto.Contains('\r') || texto.Contains('\n');

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static CorreoDeEmpresaDto ADto(CorreoDeEmpresa c) => new(
        c.Habilitado, c.Servidor, c.Puerto, c.Usuario,
        !string.IsNullOrEmpty(c.ContrasenaCifrada), c.RemitenteNombre, c.RemitenteCorreo,
        c.NombreRemitenteSistema, c.ResponderA);
}
