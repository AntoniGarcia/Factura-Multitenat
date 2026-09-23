namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Parte específica de un CFDI con complemento de Notarios Públicos. Conserva una copia por
/// comprobante: modificar la configuración general de la notaría no altera una operación.
/// </summary>
public sealed class DatosNotaria : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ComprobanteId { get; set; }
    public Comprobante Comprobante { get; set; } = null!;
    public int NumeroInstrumentoNotarial { get; set; }
    public DateOnly FechaInstrumentoNotarial { get; set; }
    public decimal MontoOperacion { get; set; }
    public decimal SubtotalOperacion { get; set; }
    public decimal IvaOperacion { get; set; }
    public bool EnajenantesEnCopropiedad { get; set; }
    public bool AdquirentesEnCopropiedad { get; set; }
    public List<InmuebleNotarial> Inmuebles { get; set; } = [];
    public List<ParteNotarial> Partes { get; set; } = [];
}

/// <summary>Descripción de cada inmueble que ampara la misma operación notarial.</summary>
public sealed class InmuebleNotarial : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid DatosNotariaId { get; set; }
    public DatosNotaria DatosNotaria { get; set; } = null!;
    public int Orden { get; set; }
    public required string TipoInmueble { get; set; }
    public required string Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Localidad { get; set; }
    public string? Referencia { get; set; }
    public required string Municipio { get; set; }
    public required string Estado { get; set; }
    public required string Pais { get; set; }
    public required string CodigoPostal { get; set; }
}

/// <summary>Enajenante o adquirente; su rol se guarda para que ambos grupos queden aislados.</summary>
public sealed class ParteNotarial : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid DatosNotariaId { get; set; }
    public DatosNotaria DatosNotaria { get; set; } = null!;
    public required string Rol { get; set; }
    public int Orden { get; set; }
    public required string Nombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public required string Rfc { get; set; }
    public string? Curp { get; set; }
    public decimal? Porcentaje { get; set; }
}
