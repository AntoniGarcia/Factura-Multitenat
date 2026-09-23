using System.Text.Json;
using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.ComercioExterior;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Genera una vista previa del borrador sin reservar folio, firmar XML ni consumir timbres.
/// El PDF se produce en memoria y no se guarda: mientras el comprobante siga editable, una
/// copia persistida podría quedar desactualizada respecto de la siguiente edición.
/// </summary>
public sealed class ServicioDePdfBorrador(
    AppDbContext baseDeDatos,
    GeneradorDePdfCfdi generador,
    GeneradorDeXmlComercioExterior generadorComercio,
    HusoDeEmpresa huso,
    ServicioDeLogo logos)
{
    public async Task<Resultado<byte[]>> GenerarAsync(Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Include(c => c.Conceptos.OrderBy(x => x.Orden))
                .ThenInclude(x => x.Impuestos)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado(
                "comprobante-no-encontrado", "Ese comprobante no existe.");

        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-es-borrador",
                "La vista previa sin timbrar solo está disponible para borradores.");

        if (await baseDeDatos.DatosObra.AsNoTracking()
            .AnyAsync(x => x.ComprobanteId == comprobanteId, ct))
            return ErrorNegocio.Regla("obra-pdf-pendiente",
                "La vista previa fiscal de esta estimación de obra aún no está disponible.");

        DatosComercioExteriorDto? comercioPdf = null;
        var comercioGuardado = await baseDeDatos.DatosComercioExterior.AsNoTracking()
            .Where(x => x.ComprobanteId == comprobanteId)
            .Select(x => x.Contenido)
            .FirstOrDefaultAsync(ct);
        if (comprobante.Exportacion == "02" || comercioGuardado is not null)
        {
            if (comercioGuardado is null)
                return ErrorNegocio.Regla("comercio-sin-datos",
                    "Guarda los datos de Comercio Exterior antes de abrir la vista previa.");
            try
            {
                comercioPdf = JsonSerializer.Deserialize<DatosComercioExteriorDto>(comercioGuardado);
            }
            catch (JsonException)
            {
                return ErrorNegocio.Regla("comercio-datos-invalidos",
                    "No se pudieron leer los datos de Comercio Exterior guardados.");
            }
            if (comercioPdf is null)
                return ErrorNegocio.Regla("comercio-datos-invalidos",
                    "No se pudieron leer los datos de Comercio Exterior guardados.");
            try
            {
                var complemento = generadorComercio.GenerarElemento(comprobante, comercioPdf);
                if (complemento.EsFallo) return complemento.Error!;
            }
            catch (FileNotFoundException)
            {
                return ErrorNegocio.Regla("esquemas-sat-incompletos",
                    "Falta el esquema SAT de Comercio Exterior 2.0 para preparar la vista previa.");
            }
        }

        if (string.IsNullOrWhiteSpace(comprobante.ReceptorRfc))
            return ErrorNegocio.Validacion(
                "borrador-sin-receptor", "Selecciona un cliente y guarda el borrador antes de generar el PDF.");

        if (comprobante.Conceptos.Count == 0)
            return ErrorNegocio.Validacion(
                "borrador-sin-conceptos", "Agrega al menos un concepto y guarda el borrador antes de generar el PDF.");

        var decimales = await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == comprobante.Moneda)
            .Select(m => (int?)m.Decimales)
            .FirstOrDefaultAsync(ct) ?? 2;

        var zona = await huso.ObtenerAsync(ct);
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);
        var logo = await logos.ObtenerAsync(ct);

        DatosNotarialesDelPdf? notariaPdf = null;
        var datosNotaria = await baseDeDatos.DatosNotaria
            .AsNoTracking()
            .Include(x => x.Inmuebles)
            .Include(x => x.Partes)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        if (datosNotaria is not null && comercioPdf is not null)
            return ErrorNegocio.Regla("complementos-incompatibles",
                "No se puede combinar Notaría con Comercio Exterior en la misma factura.");
        if (datosNotaria is not null)
        {
            var perfil = await baseDeDatos.ConfiguracionesNotario.AsNoTracking().FirstOrDefaultAsync(ct);
            if (perfil is null)
                return ErrorNegocio.Regla("perfil-notario-incompleto",
                    "Configura el notario antes de generar la vista previa de esta factura.");
            notariaPdf = DatosNotarialesDelPdf.DesdeBorrador(datosNotaria, perfil);
        }

        return generador.Generar(
            comprobante,
            new DatosDelPdf(fechaLocal, decimales, logo?.Contenido, EsBorrador: true,
                Notaria: notariaPdf, ComercioExterior: comercioPdf));
    }
}
