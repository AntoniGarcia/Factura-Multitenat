using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Obras;

public sealed class ServicioDeObras(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora)
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
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("obra-no-editable", "Los datos de obra solo se editan en un borrador.");
        if (comprobante.SubTotal <= 0)
            return ErrorNegocio.Validacion("obra-sin-trabajos", "Guarda primero los conceptos de la factura.");
        if (peticion.TipoObra is not (TiposDeObra.Publica or TiposDeObra.Privada))
            return ErrorNegocio.Validacion("obra-tipo-invalido", "Indica si el contrato es de obra pública o privada.");

        if (peticion.Deducciones is null || peticion.Deducciones.Count != 4 ||
            !TasaValida(peticion.PorcentajeAmortizacion) || !TasaValida(peticion.PorcentajeIva) ||
            !TasaValida(peticion.PorcentajeRetenciones) || !TasaValida(peticion.PorcentajeDevoluciones) ||
            peticion.Deducciones.Any(x => !TasaValida(x.Porcentaje) ||
                string.IsNullOrWhiteSpace(x.Nombre) || x.Nombre.Trim().Length > 60))
            return ErrorNegocio.Validacion("obra-datos-invalidos", "Revisa porcentajes, importes y nombres de las cuatro deducciones.");

        var amortizacion = Porcentaje(comprobante.SubTotal, peticion.PorcentajeAmortizacion);
        var retenciones = Porcentaje(comprobante.SubTotal, peticion.PorcentajeRetenciones);
        var devoluciones = Porcentaje(comprobante.SubTotal, peticion.PorcentajeDevoluciones);
        if (amortizacion + retenciones + devoluciones > comprobante.SubTotal)
            return ErrorNegocio.Validacion("obra-subtotal-negativo", "La amortización, retenciones y devoluciones superan el importe de los trabajos.");

        var datos = await baseDeDatos.DatosObra
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        var antes = datos is null ? null : Calcular(comprobante, datos);
        if (datos is null)
        {
            datos = new DatosObra { Id = Guid.NewGuid(), ComprobanteId = comprobanteId };
            baseDeDatos.DatosObra.Add(datos);
        }

        datos.TipoObra = peticion.TipoObra;
        datos.PorcentajeAmortizacion = peticion.PorcentajeAmortizacion;
        datos.PorcentajeRetenciones = peticion.PorcentajeRetenciones;
        datos.Retenciones = retenciones;
        datos.PorcentajeDevoluciones = peticion.PorcentajeDevoluciones;
        datos.Devoluciones = devoluciones;
        datos.PorcentajeIva = peticion.PorcentajeIva;
        datos.NombreDeduccion1 = peticion.Deducciones[0].Nombre.Trim();
        datos.PorcentajeDeduccion1 = peticion.Deducciones[0].Porcentaje;
        datos.NombreDeduccion2 = peticion.Deducciones[1].Nombre.Trim();
        datos.PorcentajeDeduccion2 = peticion.Deducciones[1].Porcentaje;
        datos.NombreDeduccion3 = peticion.Deducciones[2].Nombre.Trim();
        datos.PorcentajeDeduccion3 = peticion.Deducciones[2].Porcentaje;
        datos.NombreDeduccion4 = peticion.Deducciones[3].Nombre.Trim();
        datos.PorcentajeDeduccion4 = peticion.Deducciones[3].Porcentaje;

        var despues = Calcular(comprobante, datos);
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

    private static DatosObraDto Calcular(Comprobante comprobante, DatosObra datos)
    {
        var importeTrabajos = comprobante.SubTotal;
        var amortizacion = Porcentaje(importeTrabajos, datos.PorcentajeAmortizacion);
        var subtotal = importeTrabajos - amortizacion - datos.Retenciones - datos.Devoluciones;
        var iva = SinAjustesDeBase(datos) && IvaUniforme(comprobante, datos.PorcentajeIva)
            ? comprobante.Conceptos.SelectMany(x => x.Impuestos)
                .Sum(x => Math.Round(x.Importe ?? 0m, 2, MidpointRounding.ToEven))
            : Porcentaje(subtotal, datos.PorcentajeIva);
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
            datos.Devoluciones, subtotal, datos.PorcentajeIva, iva, total,
            deducciones, total - deducciones.Sum(x => x.Importe), datos.TipoObra);
    }

    private static bool SinAjustesDeBase(DatosObra datos)
        => datos.PorcentajeAmortizacion == 0 && datos.PorcentajeRetenciones == 0 &&
           datos.PorcentajeDevoluciones == 0 && datos.Retenciones == 0 && datos.Devoluciones == 0;

    private static bool IvaUniforme(Comprobante comprobante, decimal porcentajeIva)
        => comprobante.Conceptos.Count > 0 &&
           comprobante.Conceptos.All(concepto =>
               (porcentajeIva == 0 && concepto.Impuestos.Count == 0) ||
               (concepto.Impuestos.Count == 1 && concepto.Impuestos[0] is { } impuesto &&
                !impuesto.EsRetencion && impuesto.Impuesto == "002" &&
                impuesto.TipoFactor == "Tasa" &&
                impuesto.TasaOCuota * 100m == porcentajeIva));
}
