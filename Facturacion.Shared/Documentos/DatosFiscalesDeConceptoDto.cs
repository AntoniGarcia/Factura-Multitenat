namespace Facturacion.Shared.Documentos;

public sealed record DatosFiscalesDeConceptoDto(IReadOnlyList<string> Pedimentos,
    IReadOnlyList<string> CuentasPrediales, TerceroDeConceptoDto? Tercero = null);

public sealed record TerceroDeConceptoDto(string Rfc, string Nombre, string RegimenFiscal, string CodigoPostal);
