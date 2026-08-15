using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Plataforma;

/// <summary>Un cliente completo, para el formulario y el detalle.</summary>
public sealed record ClienteDto(
    Guid Id,
    int ClaveInterna,
    string Rfc,
    string Nombre,
    string RegimenFiscal,
    string DomicilioFiscalCp,
    string? ResidenciaFiscal,
    string? NumRegIdTrib,
    string? UsoCfdiPreferido,
    string? MetodoPagoPreferido,
    string? FormaPagoPreferida,
    string? Telefono,
    string? CorreoPrincipal,
    string? Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string? Colonia,
    string? Localidad,
    string? Referencia,
    string? Municipio,
    string? Estado,
    string? Pais,
    string? CodigoPostal,
    bool Activo);

/// <summary>Renglón de la lista. Solo lo que se ve en la rejilla, para no traer de más.</summary>
public sealed record ClienteEnListaDto(
    Guid Id,
    int ClaveInterna,
    string Rfc,
    string Nombre,
    string RegimenFiscal,
    string DomicilioFiscalCp,
    string? CorreoPrincipal,
    bool Activo);

/// <summary>
/// Alta y edición. No trae <c>Id</c> ni <c>ClaveInterna</c>: el primero va en la ruta y la
/// segunda la asigna el servidor.
/// </summary>
public sealed record PeticionGuardarCliente(
    string Rfc,
    string Nombre,
    string RegimenFiscal,
    string DomicilioFiscalCp,
    string? ResidenciaFiscal,
    string? NumRegIdTrib,
    string? UsoCfdiPreferido,
    string? MetodoPagoPreferido,
    string? FormaPagoPreferida,
    string? Telefono,
    string? CorreoPrincipal,
    string? Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string? Colonia,
    string? Localidad,
    string? Referencia,
    string? Municipio,
    string? Estado,
    string? Pais,
    string? CodigoPostal,
    bool Activo);

/// <summary>
/// Respuesta al guardar: el cliente ya guardado y qué se le cambió al nombre para cumplir
/// con CFDI 4.0, para explicárselo al usuario en vez de corregirlo a escondidas.
/// </summary>
public sealed record RespuestaGuardarCliente(
    ClienteDto Cliente,
    NombreFiscalNormalizado Nombre);

/// <summary>Una página de clientes con el total, para que la tabla sepa cuántas páginas hay.</summary>
public sealed record PaginaDeClientes(
    IReadOnlyList<ClienteEnListaDto> Elementos,
    int Total);
