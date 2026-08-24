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
    LicenciasDto Licencias);

/// <summary>
/// Licencias de los módulos de fase 2. Se guardan y se muestran, pero <b>no habilitan nada</b>:
/// Carta Porte, Comercio Exterior, Notaría e INE están fuera del MVP (REPARTO-EQUIPO.md §1).
/// Viajan aparte para que en la interfaz sea evidente que son un bloque reservado.
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
    LicenciasDto Licencias);

/// <summary>
/// Respuesta al guardar: la empresa ya guardada y qué se le cambió al nombre para cumplir
/// con CFDI 4.0, para poder explicárselo al usuario en vez de corregirlo a escondidas
/// (ARQUITECTURA.md §7).
/// </summary>
public sealed record RespuestaGuardarEmpresa(
    EmpresaDto Empresa,
    NombreFiscalNormalizado Nombre);

/// <summary>Valores por omisión de la empresa. Las tasas viajan como fracción: 0.16 es 16 %.</summary>
public sealed record ConfiguracionEmpresaDto(
    decimal TasaIvaPorDefecto,
    decimal TasaRetencionIvaPorDefecto,
    decimal TasaRetencionIsrPorDefecto,
    int DiasAvisoCaducidadCertificado);

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
