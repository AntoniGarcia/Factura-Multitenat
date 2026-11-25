using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Emision;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;
using Facturacion.Shared.Obras;
using Microsoft.EntityFrameworkCore;
using Facturacion.Shared.Contratos;
using System.Text.Json;

namespace Facturacion.Server.Modules.Documentos.Obras;

public sealed class ServicioDeObras(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora,
    IServicioCatalogosSat catalogos)
{
    public async Task<Resultado<DatosObraDto?>> ObtenerAsync(Guid comprobanteId, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await baseDeDatos.Comprobantes.AsNoTracking()
            .Include(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");

        var datos = await baseDeDatos.DatosObra.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        return datos is null ? null : Calcular(comprobante, datos);
    }

    public async Task<Resultado<ConciliacionFiscalObraDto>> ConciliarFiscalmenteAsync(
        Guid comprobanteId, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await baseDeDatos.Comprobantes.AsNoTracking()
            .Include(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(x => x.Relacionados)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");

        var datos = await baseDeDatos.DatosObra.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        if (datos is null)
            return ErrorNegocio.NoEncontrado("obra-no-encontrada", "Guarda primero el cálculo de obra.");

        var error = ValidadorFiscalDeObra.Validar(comprobante, datos);
        return error is null
            ? new ConciliacionFiscalObraDto(true,
                "El cálculo de obra coincide con los importes fiscales. Antes de emitir se validarán también los demás datos del CFDI.")
            : new ConciliacionFiscalObraDto(false, error.Mensaje);
    }

    public async Task<Resultado<DatosObraDto>> GuardarAsync(
        Guid comprobanteId, PeticionGuardarDatosObra peticion, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await baseDeDatos.Comprobantes
            .Include(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(x => x.Relacionados)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("obra-no-editable", "Los datos de obra solo se editan en un borrador.");
        if (ReglaDeVariante.Exigir(comprobante, VariantesDeFactura.Obra, "factura de obra") is { } variante)
            return variante;
        if (PermisoDeEmision.ExigirEdicion(contexto, comprobante) is { } sinPermiso)
            return sinPermiso;
        if (comprobante.SubTotal <= 0)
            return ErrorNegocio.Validacion("obra-sin-trabajos", "Guarda primero los conceptos de la factura.");
        if (peticion.TipoObra is not (TiposDeObra.Publica or TiposDeObra.Privada))
            return ErrorNegocio.Validacion("obra-tipo-invalido", "Indica si el contrato es de obra pública o privada.");
        if (peticion.ServicioParcial is { } parcial)
        {
            if (ServicioParcialConstruccion.Validar(parcial) is { } errorParcial) return errorParcial;
            if (await catalogos.ResolverAsync("c_CodigoPostal", parcial.CodigoPostal, ct) is not { Vigente: true })
                return ErrorNegocio.Validacion("servicio-parcial-cp", "El código postal del inmueble no está vigente en el catálogo SAT.");
        }

        if (peticion.Deducciones is null || peticion.Deducciones.Count != 4 ||
            !TasaValida(peticion.PorcentajeAmortizacion) ||
            !TasaValida(peticion.PorcentajeRetenciones) || !TasaValida(peticion.PorcentajeDevoluciones) ||
            peticion.Deducciones.Any(x => !TasaValida(x.Porcentaje) ||
                string.IsNullOrWhiteSpace(x.Nombre) || x.Nombre.Trim().Length > 60))
            return ErrorNegocio.Validacion("obra-datos-invalidos", "Revisa porcentajes, importes y nombres de las cuatro deducciones.");

        var retenciones = Porcentaje(comprobante.SubTotal, peticion.PorcentajeRetenciones);
        var devoluciones = Porcentaje(comprobante.SubTotal, peticion.PorcentajeDevoluciones);
        var decimalesMoneda = await baseDeDatos.SatMonedas.AsNoTracking()
            .Where(x => x.Clave == comprobante.Moneda)
            .Select(x => (int?)x.Decimales)
            .FirstOrDefaultAsync(ct) ?? 2;

        var datos = await baseDeDatos.DatosObra
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        var antes = datos is null ? null : Calcular(comprobante, datos);
        if (datos is null)
        {
            datos = new DatosObra { Id = Guid.NewGuid(), ComprobanteId = comprobanteId };
            baseDeDatos.DatosObra.Add(datos);
        }

        datos.TipoObra = peticion.TipoObra;
        datos.ServicioParcialJson = peticion.ServicioParcial is null ? null : JsonSerializer.Serialize(peticion.ServicioParcial);
        datos.PorcentajeAmortizacion = peticion.PorcentajeAmortizacion;
        datos.PorcentajeRetenciones = peticion.PorcentajeRetenciones;
        datos.Retenciones = retenciones;
        datos.PorcentajeDevoluciones = peticion.PorcentajeDevoluciones;
        datos.Devoluciones = devoluciones;
        datos.NombreDeduccion1 = peticion.Deducciones[0].Nombre.Trim();
        datos.PorcentajeDeduccion1 = peticion.Deducciones[0].Porcentaje;
        datos.NombreDeduccion2 = peticion.Deducciones[1].Nombre.Trim();
        datos.PorcentajeDeduccion2 = peticion.Deducciones[1].Porcentaje;
        datos.NombreDeduccion3 = peticion.Deducciones[2].Nombre.Trim();
        datos.PorcentajeDeduccion3 = peticion.Deducciones[2].Porcentaje;
        datos.NombreDeduccion4 = peticion.Deducciones[3].Nombre.Trim();
        datos.PorcentajeDeduccion4 = peticion.Deducciones[3].Porcentaje;

        if (AmortizacionDeObra.Aplicar(comprobante, datos, decimalesMoneda) is { } sinAplicar)
            return sinAplicar;
        if (await AnticipoDeObra.ValidarAsync(baseDeDatos, comprobante, datos.PorcentajeAmortizacion, ct) is { } anticipo)
            return anticipo;

        var despues = Calcular(comprobante, datos);
        if (despues.SubtotalEstimacion < 0)
            return ErrorNegocio.Validacion("obra-subtotal-negativo", "La amortización y las retenciones superan el importe de los trabajos más devoluciones.");
        if (despues.ImporteLiquido < 0)
            return ErrorNegocio.Validacion("obra-liquido-negativo", "Las deducciones superan el total de la estimación.");

        comprobante.ModificadoUtc = DateTime.UtcNow;
        bitacora.Registrar(EntidadesDeBitacora.DatosObra, datos.Id.ToString(),
            AccionesDeBitacora.DatosObraActualizados, antes, despues);
        await baseDeDatos.SaveChangesAsync(ct);
        return despues;
    }

    private async Task<ErrorNegocio?> ValidarLicenciaAsync(CancellationToken ct)
        => await baseDeDatos.Empresas.AsNoTracking()
            .AnyAsync(x => x.Id == contexto.EmpresaId && x.LicObras, ct)
            ? null
            : ErrorNegocio.Regla("modulo-obras-no-contratado", "Esta empresa no tiene activo el módulo de Constructoras.");

    private static bool TasaValida(decimal valor)
        => valor is >= 0 and <= 100 && TieneEscala(valor, 4);

    private static bool TieneEscala(decimal valor, int decimales)
        => decimal.Round(valor, decimales) == valor;
    private static decimal Porcentaje(decimal baseDeCalculo, decimal tasa)
        => Math.Round(baseDeCalculo * tasa / 100m, 6, MidpointRounding.ToEven);

    // También lo usa el PDF de la factura, para que el papel dé lo mismo que la pestaña Obra.
    public static DatosObraDto Calcular(Comprobante comprobante, DatosObra datos)
    {
        // Regla de estimación de obra:
        // Subtotal = Importe de los trabajos - Amortización del anticipo - Retenciones + Devoluciones
        // IVA = tasa efectiva de IVA del comprobante aplicada al Subtotal
        // Total = Subtotal + IVA
        // Importe líquido = Total - Deducciones (5 al millar, 1% OBS, ICIC, ÚNETE, etc.)
        var importeTrabajos = comprobante.SubTotal;
        var amortizacion = comprobante.Descuento != 0
            ? comprobante.Descuento
            : Porcentaje(importeTrabajos, datos.PorcentajeAmortizacion);
        var subtotal = importeTrabajos - amortizacion - datos.Retenciones + datos.Devoluciones;
        var tasaIva = TasaEfectivaDeIva(comprobante);
        var iva = Math.Round(subtotal * tasaIva, 2, MidpointRounding.ToEven);
        var total = subtotal + iva;
        DeduccionDeObraDto[] deducciones =
        [
            new(datos.NombreDeduccion1, datos.PorcentajeDeduccion1, Porcentaje(importeTrabajos, datos.PorcentajeDeduccion1)),
            new(datos.NombreDeduccion2, datos.PorcentajeDeduccion2, Porcentaje(importeTrabajos, datos.PorcentajeDeduccion2)),
            new(datos.NombreDeduccion3, datos.PorcentajeDeduccion3, Porcentaje(importeTrabajos, datos.PorcentajeDeduccion3)),
            new(datos.NombreDeduccion4, datos.PorcentajeDeduccion4, Porcentaje(importeTrabajos, datos.PorcentajeDeduccion4))
        ];
        return new DatosObraDto(importeTrabajos, datos.PorcentajeAmortizacion, amortizacion,
            datos.PorcentajeRetenciones, datos.Retenciones, datos.PorcentajeDevoluciones,
            datos.Devoluciones, subtotal, iva, total,
            deducciones, total - deducciones.Sum(x => x.Importe),
            datos.TipoObra) { ServicioParcial = ServicioParcialConstruccion.Leer(datos.ServicioParcialJson) };
    }

    internal static decimal TasaEfectivaDeIva(Comprobante comprobante)
    {
        var baseTotal = comprobante.Conceptos.Sum(c => c.Importe);
        if (baseTotal <= 0) return 0m;

        decimal sumaPonderada = 0m;
        foreach (var concepto in comprobante.Conceptos)
        {
            var tasaConcepto = concepto.Impuestos
                .Where(i => !i.EsRetencion && i.Impuesto == "002" && i.TipoFactor == "Tasa")
                .Sum(i => i.TasaOCuota ?? 0m);
            sumaPonderada += concepto.Importe * tasaConcepto;
        }

        return sumaPonderada / baseTotal;
    }
}
