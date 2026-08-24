using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioClientes"/> para pruebas en Development.
/// Devuelve siempre el cliente genérico XAXX010101000 (público en general).
/// </summary>
public sealed class DobleServicioClientes : IServicioClientes
{
    public Task<ReceptorFiscalDto?> ObtenerParaTimbradoAsync(Guid clienteId, CancellationToken ct)
    {
        var receptor = new ReceptorFiscalDto(
            ClienteId: clienteId,
            Rfc: "XAXX010101000",
            Nombre: "PUBLICO EN GENERAL",
            RegimenFiscal: "616",
            DomicilioFiscalCp: "06500",
            UsoCfdiPreferido: "S01",
            MetodoPagoPreferido: null,
            FormaPagoPreferida: null,
            CorreoPrincipal: null,
            ResidenciaFiscal: null,
            NumRegIdTrib: null
        );

        return Task.FromResult<ReceptorFiscalDto?>(receptor);
    }
}
