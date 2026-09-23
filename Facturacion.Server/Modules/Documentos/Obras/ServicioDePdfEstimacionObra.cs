using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Obras;

public sealed class ServicioDePdfEstimacionObra(
    AppDbContext baseDeDatos,
    ServicioDeObras obras,
    GeneradorDePdfEstimacionObra generador,
    HusoDeEmpresa huso)
{
    public async Task<Resultado<byte[]>> GenerarAsync(Guid comprobanteId, CancellationToken ct)
    {
        var datos = await obras.ObtenerAsync(comprobanteId, ct);
        if (datos.EsFallo) return datos.Error!;
        if (datos.Valor is null)
            return ErrorNegocio.Regla("obra-sin-datos", "Guarda el cálculo de obra antes de generar su estimación.");
        if (datos.Valor.SubtotalEstimacion < 0 || datos.Valor.ImporteLiquido < 0)
            return ErrorNegocio.Validacion("obra-calculo-desactualizado",
                "Los conceptos cambiaron y el cálculo de obra ya no es válido. Revisa y guarda la estimación de nuevo.");

        var factura = await baseDeDatos.Comprobantes.AsNoTracking()
            .Include(x => x.Conceptos)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (factura is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (factura.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("obra-no-es-borrador", "Esta estimación ya no está en borrador.");
        if (factura.Conceptos.Count == 0 || string.IsNullOrWhiteSpace(factura.ReceptorRfc))
            return ErrorNegocio.Validacion("obra-incompleta", "Guarda los conceptos y selecciona al cliente.");

        var zona = await huso.ObtenerAsync(ct);
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(factura.FechaEmisionUtc, DateTimeKind.Utc), zona);
        return generador.Generar(factura, datos.Valor, fechaLocal);
    }
}
