using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioEmpresaEmisora"/> para pruebas en Development.
/// Devuelve un emisor fiscal válido con RFC, nombre y régimen de prueba.
/// </summary>
public sealed class DobleServicioEmpresaEmisora : IServicioEmpresaEmisora
{
    public Task<EmisorFiscalDto> ObtenerParaTimbradoAsync(CancellationToken ct)
    {
        var emisor = new EmisorFiscalDto(
            EmpresaId: Guid.NewGuid(),
            Rfc: "AAA010101BBB",
            Nombre: "EMPRESA PRUEBA SA DE CV",
            RegimenFiscal: "601",
            CodigoPostalExpedicion: "06500",
            ZonaHoraria: "America/Mexico_City"
        );

        return Task.FromResult(emisor);
    }

    public Task<ArchivoDto?> ObtenerLogoAsync(CancellationToken ct)
    {
        // En desarrollo no hay logo, devuelve null.
        return Task.FromResult<ArchivoDto?>(null);
    }
}
