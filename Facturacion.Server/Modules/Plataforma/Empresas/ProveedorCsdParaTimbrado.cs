using Facturacion.Server.Data;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>Entrega el CSD descifrado para timbrar y registra el acceso sin guardar el material en logs. Valida la empresa; dentro del mismo proceso la interfaz no impide que otro servicio la invoque.</summary>
public sealed class ProveedorCsdParaTimbrado(
    AppDbContext baseDeDatos,
    IAlmacenDeArchivos almacen,
    IProtectorDeSecretos secretos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora) : IProveedorCsdParaTimbrado
{
    public async Task<CsdDescifradoDto> ObtenerAsync(CancellationToken ct)
    {
        var empresaId = contexto.EmpresaId;

        var certificado = await baseDeDatos.CertificadosCsd
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Activo, ct)
            ?? throw new InvalidOperationException(
                "La empresa no tiene un certificado de sello digital activo.");

        if (DateTime.UtcNow > certificado.VigenciaHastaUtc)
            throw new InvalidOperationException(
                $"El certificado de sello digital caducó el {certificado.VigenciaHastaUtc:dd/MM/yyyy}.");

        var cer = await almacen.LeerAsync(empresaId, CategoriasDeArchivo.CertificadoCer, certificado.RutaCer, ct);
        var key = await almacen.LeerAsync(empresaId, CategoriasDeArchivo.LlavePrivadaKey, certificado.RutaKey, ct);

        // Solo el número de serie: la bitácora dice quién pidió el sello y con qué
        // certificado, nunca el material.
        bitacora.Registrar(
            EntidadesDeBitacora.CertificadoCsd, certificado.Id.ToString(),
            AccionesDeBitacora.CsdEntregadoParaTimbrar,
            despues: new { certificado.NumeroSerie });

        await baseDeDatos.SaveChangesAsync(ct);

        return new CsdDescifradoDto(
            certificado.NumeroSerie,
            cer,
            key,
            secretos.Descifrar(empresaId, certificado.ContrasenaCifrada),
            certificado.VigenciaDesdeUtc,
            certificado.VigenciaHastaUtc);
    }
}
