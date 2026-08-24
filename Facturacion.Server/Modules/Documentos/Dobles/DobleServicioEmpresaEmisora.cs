using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioEmpresaEmisora"/> para pruebas en Development.
/// Devuelve un emisor fiscal válido con RFC, nombre y régimen de prueba.
/// El CSD devuelto es ficticio pero con estructura de datos realista.
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

/// <summary>
/// Doble de <see cref="IProveedorCsdParaTimbrado"/> para pruebas en Development.
/// Devuelve un CSD ficticio pero con estructura de datos realista.
/// </summary>
public sealed class DobleProveedorCsdParaTimbrado : IProveedorCsdParaTimbrado
{
    public Task<CsdDescifradoDto> ObtenerAsync(CancellationToken ct)
    {
        // Certificado del SAT (bytes vacíos para prueba). En producción estos serían
        // certificados reales cifrados y descifrados por Data Protection.
        var certificadoCer = new byte[] { 0x30, 0x82 }; // Mimicry de estructura DER
        var llavePrivadaKey = new byte[] { 0x30, 0x82 }; // Mimicry de estructura DER

        var resultado = new CsdDescifradoDto(
            NumeroSerie: "20001000000000008268",
            CertificadoCer: certificadoCer,
            LlavePrivadaKey: llavePrivadaKey,
            ContrasenaLlave: "12345678",
            VigenciaDesdeUtc: new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            VigenciaHastaUtc: new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        );

        return Task.FromResult(resultado);
    }
}
