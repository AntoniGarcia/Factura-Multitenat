using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>Entrega los datos del emisor para XML y PDF sin exponer el CSD. El material de firma utiliza una interfaz separada.</summary>
public sealed class ServicioEmpresaEmisora(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    ServicioDeLogo logos) : IServicioEmpresaEmisora
{
    public async Task<EmisorFiscalDto> ObtenerParaTimbradoAsync(CancellationToken ct)
    {
        var empresa = await baseDeDatos.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct)
            ?? throw new InvalidOperationException("Se pidió el emisor de una empresa que no existe.");

        if (!empresa.Activa)
            throw new InvalidOperationException("La empresa está desactivada y no puede emitir comprobantes.");

        return new EmisorFiscalDto(
            empresa.Id,
            empresa.Rfc,
            empresa.NombreFiscal,
            empresa.RegimenFiscal,
            empresa.CodigoPostalExpedicion,
            empresa.ZonaHoraria,
            // Es el Reply-To de los correos del CFDI: la respuesta del cliente va a la empresa,
            // no al buzón del SaaS.
            empresa.CorreoContacto);
    }

    public Task<ArchivoDto?> ObtenerLogoAsync(CancellationToken ct) => logos.ObtenerAsync(ct);
}
