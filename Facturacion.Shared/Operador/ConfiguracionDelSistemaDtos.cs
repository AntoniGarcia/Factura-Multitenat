namespace Facturacion.Shared.Operador;

/// <summary>
/// La configuración actual del sistema: correo saliente (SMTP), nombre que aparece en los
/// correos y documentos, y las plantillas de los mensajes de registro. Solo el operador
/// puede verla y modificarla. La credencial SMTP nunca forma parte de la respuesta: solo se
/// informa si existe una configurada.
/// </summary>
public sealed record ConfiguracionDelSistemaDto(
    string Servidor,
    int Puerto,
    string Usuario,
    bool ContrasenaSmtpConfigurada,
    string RemitenteCorreo,
    string RemitenteNombre,
    bool UsarTls,
    string NombreDelSistema,
    string AsuntoVerificacion,
    string CuerpoVerificacion,
    string AsuntoContrasena,
    string CuerpoContrasena);

/// <summary>
/// Guardar la configuración del sistema. Una contraseña SMTP nueva reemplaza la existente;
/// si se omite, se conserva. La contraseña del operador es obligatoria porque
/// este cambio afecta a todo el SaaS: si alguien roba la sesión del operador, que al menos
/// necesite también la contraseña para reconfigurar el correo.
/// </summary>
public sealed record PeticionGuardarConfiguracionDelSistema(
    string Servidor,
    int Puerto,
    string Usuario,
    string? ContrasenaSmtpNueva,
    string RemitenteCorreo,
    string RemitenteNombre,
    bool UsarTls,
    string NombreDelSistema,
    string AsuntoVerificacion,
    string CuerpoVerificacion,
    string AsuntoContrasena,
    string CuerpoContrasena,
    [property: System.ComponentModel.DataAnnotations.Required]
    string ContrasenaDelOperador);
