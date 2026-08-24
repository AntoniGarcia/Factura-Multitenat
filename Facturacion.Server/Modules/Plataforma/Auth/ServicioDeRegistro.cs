using System.Security.Cryptography;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Alta de una cuenta desde fuera del sistema: el único camino anónimo que crea datos.
///
/// <para><b>La contraseña viaja por correo, y eso tiene un costo</b></para>
/// El correo no es un canal seguro: queda en la bandeja de quien se registra, en el servidor
/// que lo entregó y en cualquier respaldo de ambos. Se hizo así por decisión explícita. La
/// alternativa —mandar un enlace de un solo uso para que la fije él— cuesta lo mismo de
/// construir y evita que la contraseña exista fuera de su cabeza; si algún día se cambia,
/// se cambia aquí y en la plantilla, no en más sitios.
///
/// <para><b>No se puede averiguar quién tiene cuenta</b></para>
/// La respuesta es idéntica exista o no el correo. Si el correo ya estaba registrado no se
/// crea nada, pero <b>sí se le manda un aviso</b>: así el dueño legítimo siempre recibe algo
/// que explica lo que pasó, y quien esté probando correos ajenos no aprende nada de la
/// pantalla.
///
/// <para><b>Sin empresa</b></para>
/// Una cuenta recién creada no tiene empresa emisora ni, por tanto, permisos: los permisos
/// se otorgan por empresa. La primera empresa se da de alta ya dentro, y es ahí donde el
/// dueño recibe los seis permisos.
/// </summary>
public sealed class ServicioDeRegistro(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IServicioDeCorreo correo,
    IServicioDeBitacora bitacora,
    IControlDeIntentos intentos,
    ILogger<ServicioDeRegistro> registro)
{
    /// <summary>
    /// El mismo texto para los dos caminos. Vive en una constante para que nadie lo cambie
    /// en un sitio y no en el otro, que es como se filtra la diferencia sin querer.
    /// </summary>
    private const string MensajeUnico =
        "Si el correo es válido, te llegará un mensaje con tus datos de acceso en unos minutos. " +
        "Revisa también la carpeta de correo no deseado.";

    public async Task<Resultado<RespuestaRegistro>> RegistrarAsync(
        PeticionRegistro peticion, string? ip, CancellationToken ct)
    {
        // El límite es por IP y no por correo: limitarlo por correo dejaría que alguien
        // creara cuentas sin freno cambiando de dirección en cada intento.
        var claveIp = $"registro:{ip ?? "desconocida"}";

        if (intentos.BloqueoRestante(claveIp) is { } espera)
            return ErrorNegocio.LimiteExcedido(
                "demasiados-intentos",
                $"Demasiadas altas desde esta red. Vuelve a intentar en {Redondear(espera)}.");

        var correoNormalizado = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var nombre = (peticion.Nombre ?? string.Empty).Trim();

        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(correoNormalizado) || !EsCorreoValido(correoNormalizado))
            errores["correo"] = ["Escribe un correo válido."];

        if (string.IsNullOrWhiteSpace(nombre))
            errores["nombre"] = ["Escribe tu nombre."];

        if (errores.Count > 0)
        {
            // Un formulario mal llenado cuenta como intento: si no contara, bastaría con
            // mandar basura para sondear sin gastar el límite.
            intentos.RegistrarFallo(claveIp);
            return ErrorNegocio.Validacion("datos-invalidos", "Revisa los datos.", errores);
        }

        intentos.RegistrarFallo(claveIp);

        var existente = await usuarios.FindByEmailAsync(correoNormalizado);

        if (existente is not null)
        {
            await AvisarQueYaExisteAsync(correoNormalizado, ct);

            registro.LogInformation(
                "Registro rechazado: el correo ya tiene cuenta. Se avisó al titular por correo.");

            return new RespuestaRegistro(MensajeUnico);
        }

        var ahora = DateTime.UtcNow;

        var cuenta = new Cuenta
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            CorreoContacto = correoNormalizado,
            FechaAltaUtc = ahora
        };

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            CuentaId = cuenta.Id,
            UserName = correoNormalizado,
            Email = correoNormalizado,
            // Nadie ha comprobado el correo todavía. Se marca confirmado porque el sistema no
            // tiene un circuito de confirmación, y recibir la contraseña ES la comprobación:
            // quien no controle el buzón no llega a entrar.
            EmailConfirmed = true,
            FechaAltaUtc = ahora,
            // Se registró él mismo, así que puede cambiar su contraseña desde su perfil.
            CreadoPorAdministrador = false
        };

        var contrasena = GenerarContrasena();

        baseDeDatos.Cuentas.Add(cuenta);
        await baseDeDatos.SaveChangesAsync(ct);

        var alta = await usuarios.CreateAsync(usuario, contrasena);

        if (!alta.Succeeded)
        {
            // La cuenta quedó sin usuario: se borra para no dejar cuentas huérfanas que
            // nadie puede usar ni encontrar.
            baseDeDatos.Cuentas.Remove(cuenta);
            await baseDeDatos.SaveChangesAsync(ct);

            registro.LogError(
                "No se pudo crear el usuario del registro: {Errores}",
                string.Join("; ", alta.Errors.Select(e => e.Description)));

            return ErrorNegocio.Validacion("registro-fallido", "No se pudo completar el alta. Inténtalo más tarde.");
        }

        // La contraseña no entra a la bitácora, ni entera ni recortada.
        bitacora.Registrar(
            EntidadesDeBitacora.Usuario, usuario.Id.ToString(), AccionesDeBitacora.CuentaRegistrada,
            despues: new { correo = correoNormalizado, nombre, CuentaId = cuenta.Id },
            usuarioId: usuario.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        await EnviarContrasenaAsync(correoNormalizado, nombre, contrasena, ct);

        intentos.Limpiar(claveIp);

        return new RespuestaRegistro(MensajeUnico);
    }

    /// <summary>
    /// Contraseña generada. Arma una de cada clase exigida por Identity y rellena el resto al
    /// azar, para no depender de que un azar puro acierte a incluirlas; después baraja, porque
    /// dejar las obligatorias siempre al principio reduce el espacio de búsqueda real.
    /// </summary>
    private static string GenerarContrasena()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string digitos = "23456789";
        const string simbolos = "@#$%&*+-";

        // Sin I, l, 1, O ni 0: esta contraseña se lee de un correo y se teclea a mano, y esos
        // cinco caracteres son los que se confunden al copiarlos.
        var alfabeto = mayusculas + minusculas + digitos + simbolos;

        var caracteres = new List<char>
        {
            Elegir(mayusculas), Elegir(minusculas), Elegir(digitos), Elegir(simbolos)
        };

        while (caracteres.Count < 16) caracteres.Add(Elegir(alfabeto));

        // Barajado de Fisher-Yates con la fuente criptográfica, no con Random.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string([.. caracteres]);
    }

    private static char Elegir(string fuente) => fuente[RandomNumberGenerator.GetInt32(fuente.Length)];

    private async Task EnviarContrasenaAsync(string destinatario, string nombre, string contrasena, CancellationToken ct)
    {
        var cuerpo =
            $"""
             <p>Hola, {Escapar(nombre)}:</p>
             <p>Tu cuenta ya está lista. Entra con estos datos:</p>
             <p>
               Correo: <strong>{Escapar(destinatario)}</strong><br>
               Contraseña: <strong style="font-family: monospace; font-size: 16px">{Escapar(contrasena)}</strong>
             </p>
             <p>
               Cámbiala en cuanto entres, desde tu perfil. Este mensaje contiene tu contraseña:
               bórralo después de guardarla en un lugar seguro.
             </p>
             <p>El siguiente paso es dar de alta tu empresa emisora para poder facturar.</p>
             """;

        await EnviarSinTumbarElAltaAsync(destinatario, "Tus datos de acceso", cuerpo, ct);
    }

    private async Task AvisarQueYaExisteAsync(string destinatario, CancellationToken ct)
    {
        var cuerpo =
            """
            <p>Hola:</p>
            <p>Alguien intentó registrar una cuenta con este correo, y ya tienes una.</p>
            <p>
              No se creó ninguna cuenta nueva ni cambió nada de la tuya. Si fuiste tú, entra con
              la contraseña que ya usabas. Si no fuiste tú, puedes ignorar este mensaje.
            </p>
            """;

        await EnviarSinTumbarElAltaAsync(destinatario, "Ya tienes una cuenta", cuerpo, ct);
    }

    /// <summary>
    /// Un fallo de correo no puede tirar el alta: la cuenta ya está creada y devolver un error
    /// después de haberla creado dejaría al usuario creyendo que no existe cuando sí existe.
    /// Se registra para que el operador lo vea y pueda reenviar a mano.
    /// </summary>
    private async Task EnviarSinTumbarElAltaAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct)
    {
        try
        {
            await correo.EnviarAsync(destinatario, asunto, cuerpo, ct);
        }
        catch (Exception excepcion)
        {
            registro.LogError(excepcion, "No se pudo enviar el correo del registro «{Asunto}»", asunto);
        }
    }

    private static string Escapar(string texto) => System.Net.WebUtility.HtmlEncode(texto);

    private static string Redondear(TimeSpan espera)
        => espera.TotalMinutes >= 1
            ? $"{Math.Ceiling(espera.TotalMinutes):0} minutos"
            : $"{Math.Ceiling(espera.TotalSeconds):0} segundos";

    private static bool EsCorreoValido(string correo)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(correo);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
