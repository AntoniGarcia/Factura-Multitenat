using System.Security.Cryptography;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Alta de una cuenta desde fuera del sistema: el único camino anónimo que crea datos.
///
/// <para><b>Va en dos pasos, y el orden importa</b></para>
/// Primero se manda un código al correo y el alta queda esperando en <see cref="AltaPendiente"/>;
/// la cuenta se crea cuando el código vuelve, y solo entonces sale la contraseña. Así la
/// contraseña nunca viaja a un buzón que nadie ha demostrado controlar, y nadie puede llenar
/// la base de cuentas a nombre de correos ajenos.
///
/// <para><b>La contraseña viaja por correo, y eso tiene un costo</b></para>
/// El correo no es un canal seguro: queda en la bandeja de quien se registra, en el servidor
/// que lo entregó y en cualquier respaldo de ambos. Se hizo así por decisión explícita. El
/// código de verificación reduce el problema —ahora se sabe que el buzón es suyo— pero no lo
/// quita: la alternativa sigue siendo mandar un enlace de un solo uso para que la fije él, y
/// si algún día se cambia, se cambia aquí y en la plantilla, no en más sitios.
///
/// <para><b>No se puede averiguar quién tiene cuenta</b></para>
/// La respuesta del primer paso es idéntica exista o no el correo. Si el correo ya estaba
/// registrado no se crea ningún alta pendiente, pero <b>sí se le manda un aviso</b>: así el
/// dueño legítimo siempre recibe algo que explica lo que pasó, y quien esté probando correos
/// ajenos no aprende nada de la pantalla. En el segundo paso, un código equivocado y un correo
/// sin alta pendiente dan el mismo error, por lo mismo.
///
/// <para><b>Sin empresa</b></para>
/// Una cuenta recién creada no tiene empresa emisora ni, por tanto, permisos: los permisos
/// se otorgan por empresa. La primera empresa se da de alta ya dentro, y es ahí donde el
/// dueño recibe los seis permisos.
/// </summary>
public sealed class ServicioDeRegistro(
    AppDbContext baseDeDatos,
    UserManager<Usuario> usuarios,
    IPasswordHasher<AltaPendiente> hasher,
    IServicioDeCorreo correo,
    IServicioDeBitacora bitacora,
    IControlDeIntentos intentos,
    ILogger<ServicioDeRegistro> registro)
{
    /// <summary>
    /// Lo que dura el código. Veinte minutos dan de sobra para abrir el correo y volver, y no
    /// tanto como para que el mensaje se quede olvidado en una bandeja abierta toda la tarde.
    /// </summary>
    private static readonly TimeSpan VigenciaDelCodigo = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Códigos fallidos antes de quemar el alta. Con seis dígitos y cinco tiros, la
    /// probabilidad de acertar a ciegas es de cinco entre un millón.
    /// </summary>
    private const int IntentosMaximos = 5;

    /// <summary>
    /// El mismo texto para los dos caminos del primer paso. Vive en una constante para que
    /// nadie lo cambie en un sitio y no en el otro, que es como se filtra la diferencia sin
    /// querer.
    /// </summary>
    private const string MensajeUnico =
        "Si el correo es válido, te llegará un código de seis dígitos en unos minutos. " +
        "Revisa también la carpeta de correo no deseado.";

    /// <summary>
    /// El mismo error para «código equivocado», «código caducado», «alta ya usada» y «este
    /// correo no pidió nada». Separarlos diría cuál de las cuatro cosas pasó.
    /// </summary>
    private const string MensajeCodigoInvalido =
        "El código no es válido o ya caducó. Vuelve a empezar para pedir uno nuevo.";

    // ── Paso 1: pedir el código ─────────────────────────────────────────────────────────

    public async Task<Resultado<RespuestaRegistro>> RegistrarAsync(
        PeticionRegistro peticion, string? ip, CancellationToken ct)
    {
        // El límite es por IP  y no por correo: limitarlo por correo dejaría que alguien
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
                "Alta rechazada: el correo ya tiene cuenta. Se avisó al titular por correo.");

            return new RespuestaRegistro(MensajeUnico);
        }
        // El nombre de la cuenta es el que dio en el alta; si no dio ninguno, el suyo.
        var nombreCuenta = string.IsNullOrWhiteSpace(peticion.NombreCuenta)
            ? nombre
            : peticion.NombreCuenta.Trim();

        // Las anteriores de este correo se van: pedir el código otra vez invalida el de antes,
        // que es lo que espera quien vuelve a empezar porque el primero no le llegó. De paso
        // se barren las caducadas de cualquiera —son basura y esta es la única operación que
        // escribe en la tabla, así que no hace falta un servicio en segundo plano solo para eso.
        await baseDeDatos.AltasPendientes
            .Where(a => a.Correo == correoNormalizado || a.ExpiraUtc <= DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        var codigo = GenerarCodigo();
        var ahora = DateTime.UtcNow;

        var alta = new AltaPendiente
        {
            Id = Guid.NewGuid(),
            Correo = correoNormalizado,
            Nombre = nombre,
            NombreCuenta = nombreCuenta,
            HashCodigo = string.Empty,
            CreadoUtc = ahora,
            ExpiraUtc = ahora.Add(VigenciaDelCodigo),
            IpCreacion = ip
        };

        // El hasher recibe la entidad para poder salar con ella, así que el hash se asigna
        // después de construirla. El código en claro no se guarda en ningún campo.
        alta.HashCodigo = hasher.HashPassword(alta, codigo);

        baseDeDatos.AltasPendientes.Add(alta);
        await baseDeDatos.SaveChangesAsync(ct);

        await EnviarCodigoAsync(correoNormalizado, nombre, codigo, ct);

        return new RespuestaRegistro(MensajeUnico);
    }

    // ── Paso 2: canjear el código y crear la cuenta ─────────────────────────────────────

    public async Task<Resultado<RespuestaAltaVerificada>> VerificarAsync(
        PeticionVerificarAlta peticion, string? ip, CancellationToken ct)
    {
        var claveIp = $"verificacion:{ip ?? "desconocida"}";

        if (intentos.BloqueoRestante(claveIp) is { } espera)
            return ErrorNegocio.LimiteExcedido(
                "demasiados-intentos",
                $"Demasiados intentos desde esta red. Vuelve a intentar en {Redondear(espera)}.");

        var correoNormalizado = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var codigo = (peticion.Codigo ?? string.Empty).Trim();

        intentos.RegistrarFallo(claveIp);

        // Con varias del mismo correo se toma la última: es la del código que acaba de llegar.
        var alta = await baseDeDatos.AltasPendientes
            .Where(a => a.Correo == correoNormalizado && a.ConsumidoUtc == null)
            .OrderByDescending(a => a.CreadoUtc)
            .FirstOrDefaultAsync(ct);

        if (alta is null || alta.ExpiraUtc <= DateTime.UtcNow || alta.Intentos >= IntentosMaximos)
            return ErrorNegocio.Validacion("codigo-invalido", MensajeCodigoInvalido);

        if (hasher.VerifyHashedPassword(alta, alta.HashCodigo, codigo) == PasswordVerificationResult.Failed)
        {
            alta.Intentos++;
            await baseDeDatos.SaveChangesAsync(ct);

            var restantes = IntentosMaximos - alta.Intentos;

            return ErrorNegocio.Validacion(
                "codigo-invalido",
                restantes > 0
                    ? $"El código no coincide. Te quedan {restantes} intentos."
                    : MensajeCodigoInvalido);
        }

        // Alguien pudo haberse quedado con el correo entre que se pidió el código y ahora.
        if (await usuarios.FindByEmailAsync(correoNormalizado) is not null)
        {
            await MarcarConsumidaAsync(alta.Id, ct);
            return ErrorNegocio.Validacion("codigo-invalido", MensajeCodigoInvalido);
        }

        // Se marca consumida ANTES de crear nada, y con la condición dentro del UPDATE: si dos
        // peticiones llegan con el mismo código a la vez, solo una toca fila y la otra se
        // encuentra con cero. Sin esto, un doble clic crearía dos cuentas con el mismo correo.
        if (await MarcarConsumidaAsync(alta.Id, ct) == 0)
            return ErrorNegocio.Validacion("codigo-invalido", MensajeCodigoInvalido);

        var creacion = await CrearCuentaAsync(alta, ct);

        if (creacion.Error is { } fallo) return fallo;

        intentos.Limpiar(claveIp);

        return new RespuestaAltaVerificada(
            "Tu cuenta ya está lista. Te mandamos la contraseña por correo.");
    }

    /// <summary>
    /// Cierra el alta pendiente solo si seguía abierta. Devuelve las filas tocadas: cero
    /// significa que otra petición se le adelantó.
    /// </summary>
    private Task<int> MarcarConsumidaAsync(Guid altaId, CancellationToken ct)
        => baseDeDatos.AltasPendientes
            .Where(a => a.Id == altaId && a.ConsumidoUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ConsumidoUtc, DateTime.UtcNow), ct);

    private async Task<Resultado> CrearCuentaAsync(AltaPendiente alta, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;

        var cuenta = new Cuenta
        {
            Id = Guid.NewGuid(),
            Nombre = alta.NombreCuenta,
            CorreoContacto = alta.Correo,
            FechaAltaUtc = ahora
        };

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = alta.Nombre,
            CuentaId = cuenta.Id,
            UserName = alta.Correo,
            Email = alta.Correo,
            // Confirmado de verdad: para llegar hasta aquí hubo que leer un código que solo
            // estaba en ese buzón.
            EmailConfirmed = true,
            FechaAltaUtc = ahora,
            // Se registró él mismo, así que puede cambiar su contraseña desde su perfil.
            CreadoPorAdministrador = false
        };

        var contrasena = GenerarContrasena();

        baseDeDatos.Cuentas.Add(cuenta);
        await baseDeDatos.SaveChangesAsync(ct);

        var resultado = await usuarios.CreateAsync(usuario, contrasena);

        if (!resultado.Succeeded)
        {
            // La cuenta quedó sin usuario: se borra para no dejar cuentas huérfanas que
            // nadie puede usar ni encontrar.
            baseDeDatos.Cuentas.Remove(cuenta);
            await baseDeDatos.SaveChangesAsync(ct);

            registro.LogError(
                "No se pudo crear el usuario del alta verificada: {Errores}",
                string.Join("; ", resultado.Errors.Select(e => e.Description)));

            return ErrorNegocio.Validacion(
                "registro-fallido", "No se pudo completar el alta. Inténtalo más tarde.");
        }

        // Ni la contraseña ni el código entran a la bitácora, ni enteros ni recortados.
        bitacora.Registrar(
            EntidadesDeBitacora.Usuario, usuario.Id.ToString(), AccionesDeBitacora.CuentaRegistrada,
            despues: new { correo = alta.Correo, nombre = alta.Nombre, CuentaId = cuenta.Id },
            usuarioId: usuario.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        await EnviarContrasenaAsync(alta.Correo, alta.Nombre, contrasena, ct);

        return Resultado.Exito();
    }

    /// <summary>
    /// Código de seis dígitos, con la fuente criptográfica y no con <c>Random</c>. Se eligió
    /// numérico porque se teclea desde el teléfono con el correo abierto al lado, donde una
    /// cadena con mayúsculas y símbolos se equivoca más de lo que aporta: la fuerza real la
    /// dan los cinco intentos y los veinte minutos, no la longitud.
    /// </summary>
    private static string GenerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

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

    private async Task EnviarCodigoAsync(string destinatario, string nombre, string codigo, CancellationToken ct)
    {
        var cuerpo =
            $"""
             <p>Hola, {Escapar(nombre)}:</p>
             <p>Para terminar de crear tu cuenta, escribe este código en la pantalla de alta:</p>
             <p style="font-family: monospace; font-size: 28px; letter-spacing: 6px">
               <strong>{Escapar(codigo)}</strong>
             </p>
             <p>
               Caduca en {VigenciaDelCodigo.TotalMinutes:0} minutos. Cuando lo escribas te
               mandamos la contraseña en otro mensaje.
             </p>
             <p>
               Si no fuiste tú quien pidió esto, ignora este mensaje: sin el código no se crea
               ninguna cuenta con tu correo.
             </p>
             """;

        await EnviarSinTumbarElAltaAsync(destinatario, "Tu código de verificación", cuerpo, ct);
    }

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
    /// Un fallo de correo no puede tirar la operación: lo que la disparó ya quedó guardado, y
    /// devolver un error después dejaría al usuario creyendo que no pasó nada cuando sí pasó.
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
