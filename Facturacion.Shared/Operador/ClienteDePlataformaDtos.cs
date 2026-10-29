namespace Facturacion.Shared.Operador;

/// <summary>
/// Una cuenta contratante en el listado del panel.
///
/// <para><b>Qué NO lleva, a propósito</b></para>
/// Ningún dato fiscal del inquilino: ni sus clientes, ni sus productos, ni sus comprobantes.
/// El proveedor del SaaS necesita saber cuánto le vende a cada cuenta y si sigue activa, no
/// a quién le factura ella. Es la línea que separa administrar el servicio de mirar dentro
/// del negocio ajeno.
/// </summary>
public sealed record CuentaEnListaDto(
    Guid Id,
    string Nombre,
    string CorreoContacto,
    bool Activa,
    DateTime FechaAltaUtc,
    int Empresas,
    int Usuarios,
    int TimbresDisponibles,
    string? MembresiaEstado,
    DateTime? MembresiaFinUtc,
    int Pendientes);

/// <summary>Una empresa emisora vista desde el panel: identidad fiscal y consumo, nada más.</summary>
public sealed record EmpresaDeCuentaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    bool Activa,
    DateTime FechaAltaUtc,
    int ComprasPendientes);

/// <summary>Un usuario de la cuenta. Sin contraseñas ni permisos: solo quién tiene acceso.</summary>
public sealed record UsuarioDeCuentaDto(
    Guid Id,
    string Nombre,
    string Correo,
    bool Activo,
    DateTime FechaAltaUtc);

/// <summary>
/// La ficha de una cuenta contratante: sus datos, sus empresas, su bolsa de timbres y sus
/// paquetes exclusivos, que son de la cuenta. Los usuarios con acceso y las compras de cada
/// empresa se ven en la ficha de esa empresa, no mezclados aquí.
/// </summary>
public sealed record DetalleDeCuentaDto(
    CuentaEnListaDto Cuenta,
    IReadOnlyList<EmpresaDeCuentaDto> Empresas,
    LimitesDeCuentaDto Limites,
    int TimbresReservados,
    IReadOnlyList<PaquetePersonalizadoDto> PaquetesPersonalizados);

/// <summary>
/// Cuántos usuarios activos y cuántas empresas admite la cuenta, y cuántos usa. Un usuario
/// cuenta una sola vez aunque entre a varias empresas.
/// </summary>
public sealed record LimitesDeCuentaDto(
    int LimiteDeUsuarios,
    int UsuariosActivos,
    int LimiteDeEmpresas,
    int Empresas);

/// <summary>Nuevos límites de una cuenta. No pueden quedar por debajo de lo que ya usa.</summary>
public sealed record PeticionCambiarLimitesDeCuenta(
    [property: System.ComponentModel.DataAnnotations.Range(1, 999)] int LimiteDeUsuarios,
    [property: System.ComponentModel.DataAnnotations.Range(1, 999)] int LimiteDeEmpresas,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);

/// <summary>Una página de cuentas y cuántas hay en total con ese filtro.</summary>
public sealed record PaginaDeCuentas(
    IReadOnlyList<CuentaEnListaDto> Elementos,
    int Total);

/// <summary>
/// Alta o baja lógica de una empresa desde el panel. El motivo es obligatorio al desactivar:
/// es lo único que explicará después por qué se le quitó el servicio a esa exportación, como
/// el motivo de una baja de cliente.
/// </summary>
public sealed record PeticionCambiarActivoDeEmpresa(
    bool Activo,
    [property: System.ComponentModel.DataAnnotations.MaxLength(300)] string? Motivo,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);

/// <summary>
/// Nuevo correo de contacto de una cuenta. Es un dato administrativo del SaaS, no un inicio de
/// sesión: cambiar aquí el correo no cambia las credenciales de nadie.
/// </summary>
public sealed record PeticionCambiarCorreoDeContactoDeCuenta(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    [property: System.ComponentModel.DataAnnotations.MaxLength(254)]
    string CorreoNuevo,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);
