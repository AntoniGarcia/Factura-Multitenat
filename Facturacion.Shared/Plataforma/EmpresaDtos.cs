using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Plataforma;

/// <summary>Datos de la empresa activa tal como se muestran y se editan.</summary>
public sealed record EmpresaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    string CodigoPostalExpedicion,
    string ZonaHoraria,
    string? Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string? Referencia,
    string? Colonia,
    string? Localidad,
    string? Municipio,
    string? Estado,
    string? Pais,
    string? CodigoPostal,
    string? Telefono,
    string? CorreoContacto,
    bool TieneLogo,
    string? LogoNombreOriginal,
    LicenciasDto Licencias,
    DomicilioSatDto? OrigenCartaPorte = null);

/// <summary>Domicilio nacional con claves del catálogo SAT, independiente del domicilio informativo.</summary>
public sealed record DomicilioSatDto(
    string Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string Estado,
    string Municipio,
    string CodigoPostal);

/// <summary>
/// Licencias de los módulos especiales. El operador las administra y el servidor valida
/// las correspondientes al usar Notaría, Obras y Comercio Exterior.
/// </summary>
public sealed record LicenciasDto(
    bool Notarios,
    bool Obras,
    bool Comercio,
    bool Ine);

/// <summary>
/// Lo que el <c>Client</c> manda al guardar. No trae <c>Id</c>: la empresa es la del claim
/// del token, nunca un identificador que llegue del navegador (ARQUITECTURA.md §4).
/// </summary>
public sealed record PeticionGuardarEmpresa(
    string NombreFiscal,
    string RegimenFiscal,
    string CodigoPostalExpedicion,
    string ZonaHoraria,
    string? Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string? Referencia,
    string? Colonia,
    string? Localidad,
    string? Municipio,
    string? Estado,
    string? Pais,
    string? CodigoPostal,
    string? Telefono,
    string? CorreoContacto,
    LicenciasDto Licencias,
    DomicilioSatDto? OrigenCartaPorte = null);

/// <summary>
/// Respuesta al guardar: la empresa ya guardada y qué se le cambió al nombre para cumplir
/// con CFDI 4.0, para poder explicárselo al usuario en vez de corregirlo a escondidas
/// (ARQUITECTURA.md §7).
/// </summary>
public sealed record RespuestaGuardarEmpresa(
    EmpresaDto Empresa,
    NombreFiscalNormalizado Nombre);

/// <summary>Valores por omisión de la empresa. Las tasas viajan como fracción: 0.16 es 16 %.</summary>
/// <param name="TasaIshPorDefecto">Impuesto local; hoy ningún comprobante lo aplica.</param>
public sealed record ConfiguracionEmpresaDto(
    decimal TasaIvaPorDefecto,
    decimal TasaIepsPorDefecto,
    decimal TasaRetencionIvaPorDefecto,
    decimal TasaRetencionIsrPorDefecto,
    decimal TasaRetencionIepsPorDefecto,
    decimal TasaIshPorDefecto,
    int DiasAvisoCaducidadCertificado);

/// <summary>
/// Servidor SMTP propio de la empresa tal como sale del servidor: sin la contraseña, solo si
/// hay una guardada (AGENTS.md §11).
/// </summary>
public sealed record CorreoDeEmpresaDto(
    bool Habilitado,
    string? Servidor,
    int Puerto,
    string? Usuario,
    bool TieneContrasena,
    string? RemitenteNombre,
    string? RemitenteCorreo,
    string? NombreRemitenteSistema,
    string? ResponderA);

/// <param name="Contrasena">Vacía o nula conserva la guardada.</param>
/// <param name="NombreRemitenteSistema">Solo aplica cuando sale del remitente del sistema.</param>
/// <param name="ResponderA">Solo aplica cuando sale del remitente del sistema.</param>
public sealed record PeticionGuardarCorreoDeEmpresa(
    bool Habilitado,
    string? Servidor,
    int Puerto,
    string? Usuario,
    string? Contrasena,
    string? RemitenteNombre,
    string? RemitenteCorreo,
    string? NombreRemitenteSistema,
    string? ResponderA);

/// <summary>
/// Los dos únicos puertos admitidos para el SMTP de una empresa. El puerto decide el cifrado:
/// no hay modo sin cifrar, y el 25 de salida lo bloquea Azure.
/// </summary>
public static class PuertosSmtp
{
    public const int SslDirecto = 465;
    public const int StartTls = 587;

    public static bool EsAdmitido(int puerto) => puerto is SslDirecto or StartTls;
}

/// <summary>
/// Certificado de sello digital, en la única forma en la que puede salir del servidor:
/// metadatos. Ni el <c>.cer</c>, ni el <c>.key</c>, ni la contraseña vuelven nunca al
/// <c>Client</c> (ARQUITECTURA.md §4).
/// </summary>
/// <param name="DiasParaCaducar">Negativo si ya caducó.</param>
/// <param name="PorCaducar">Verdadero si entra en la ventana de aviso configurada.</param>
public sealed record CertificadoCsdDto(
    Guid Id,
    string NumeroSerie,
    DateTime VigenciaDesdeUtc,
    DateTime VigenciaHastaUtc,
    bool Activo,
    DateTime FechaCargaUtc,
    int DiasParaCaducar,
    bool PorCaducar);

/// <summary>Una serie de folios de la empresa.</summary>
/// <param name="FolioActual">
/// Último folio entregado. Se muestra en la administración de series —el contador necesita
/// saber por dónde va— pero <b>no</b> se le enseña al capturista antes de timbrar
/// (ARQUITECTURA.md §5).
/// </param>
public sealed record SerieDto(
    Guid Id,
    string Prefijo,
    int FolioInicial,
    int FolioActual,
    string TipoComprobante,
    bool Activa);

/// <summary>
/// Una serie tal como la ve quien va a emitir, no quien la administra: sin
/// <see cref="SerieDto.FolioActual"/>, que revela cuánto ha facturado la empresa y que
/// ARQUITECTURA.md §5 reserva para la administración.
/// </summary>
public sealed record SerieParaEmisionDto(
    Guid Id,
    string Prefijo,
    string TipoComprobante);

/// <summary>Alta y edición de una serie. El folio actual no se edita: lo mueve solo la reserva.</summary>
public sealed record PeticionGuardarSerie(
    string Prefijo,
    int FolioInicial,
    string TipoComprobante,
    bool Activa);

/// <summary>
/// Alta de una empresa emisora. Solo los datos sin los que no se puede timbrar; el domicilio
/// y el resto se completan después en «Datos fiscales».
///
/// <para>
/// El RFC va aquí y no en <see cref="PeticionGuardarEmpresa"/> porque se fija una sola vez:
/// cambiarlo después convertiría a la empresa en otra, y las facturas ya emitidas llevan el
/// anterior congelado dentro.
/// </para>
/// </summary>
public sealed record PeticionCrearEmpresa(
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    string CodigoPostalExpedicion,
    string ZonaHoraria);
