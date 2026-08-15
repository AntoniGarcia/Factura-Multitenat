using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Implementación de <see cref="IServicioEmpresaEmisora"/> del contrato congelado
/// (REPARTO-EQUIPO.md §5): lo que la mitad B necesita del emisor para timbrar y para el PDF.
/// <para>
/// Deliberadamente <b>no</b> expone el CSD. Esa es la razón de que el contrato lo haya
/// separado en <see cref="IProveedorCsdParaTimbrado"/>: mientras la llave privada viviera en
/// la misma interfaz que el RFC, cualquier código que necesitara un dato inocente obtenía de
/// paso el material del sello.
/// </para>
/// </summary>
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
            empresa.ZonaHoraria);
    }

    public Task<ArchivoDto?> ObtenerLogoAsync(CancellationToken ct) => logos.ObtenerAsync(ct);
}
