namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Un alta de cuenta a medio camino: los datos ya se escribieron, pero todavía nadie ha
/// demostrado que controle el buzón. Guarda lo tecleado en el asistente y el código que se
/// mandó por correo, y desaparece en cuanto se canjea o caduca.
///
/// <para><b>Por qué existe la tabla</b></para>
/// La cuenta no se crea hasta que el código vuelve. Si se creara antes, cualquiera podría
/// llenar la base de cuentas con correos ajenos, y el dueño de cada uno recibiría una
/// contraseña que nunca pidió.
///
/// <para><b>El código no se guarda en claro</b></para>
/// Son seis dígitos: mil ciclos de un procesador cualquiera los recorren todos. Por eso no
/// se guarda con SHA-256 como el token de refresco —que sí puede, porque son 32 bytes al
/// azar— sino con el mismo PBKDF2 de las contraseñas, que está hecho justo para secretos
/// cortos que una persona teclea.
///
/// <para><b>Fuera del filtro de empresa</b></para>
/// No implementa <c>IEntidadDeEmpresa</c> y no puede: quien se está dando de alta todavía
/// no tiene cuenta, ni empresa, ni nada que filtrar.
/// </summary>
public sealed class AltaPendiente
{
    public Guid Id { get; set; }

    /// <summary>Correo normalizado. Es por donde se busca al verificar.</summary>
    public required string Correo { get; set; }

    public required string Nombre { get; set; }

    /// <summary>Nombre de la cuenta que se creará. Nunca vacío: si no lo dieron, es el de la persona.</summary>
    public required string NombreCuenta { get; set; }

    /// <summary>Resultado de <c>IPasswordHasher</c> sobre el código. El código en claro no se guarda.</summary>
    public required string HashCodigo { get; set; }

    public DateTime CreadoUtc { get; set; }

    /// <summary>
    /// Pasado este momento el código no sirve. Corto a propósito: un código que vive horas
    /// es un código que se queda olvidado en una bandeja abierta.
    /// </summary>
    public DateTime ExpiraUtc { get; set; }

    /// <summary>
    /// Códigos fallidos. Al pasar del tope el alta se quema, para que no se pueda recorrer
    /// el millón de combinaciones a base de insistir.
    /// </summary>
    public int Intentos { get; set; }

    /// <summary>Momento en que se canjeó. Un alta consumida ya no vale para nada.</summary>
    public DateTime? ConsumidoUtc { get; set; }

    public string? IpCreacion { get; set; }
}
