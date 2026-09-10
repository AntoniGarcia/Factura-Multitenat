using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Pruebas;

public static class ComprobanteDeEjemplo
{
    public static Comprobante Crear()
    {
        var comprobanteId = Guid.NewGuid();
        var empresaId = Guid.NewGuid();

        var concepto1 = new Concepto
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            ComprobanteId = comprobanteId,
            Orden = 1,
            ClaveProdServ = "43211508",
            ClaveUnidad = "H87",
            Descripcion = "Laptop 15 pulgadas, 16GB RAM",
            Cantidad = 2,
            ValorUnitario = 12500.00m,
            Importe = 25000.00m,
            Descuento = 0m,
            ObjetoImp = "02",
            Impuestos =
            [
                new ImpuestoConcepto
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    Impuesto = "002",
                    TipoFactor = "Tasa",
                    TasaOCuota = 0.160000m,
                    Base = 25000.00m,
                    Importe = 4000.00m,
                    EsRetencion = false
                }
            ]
        };

        var concepto2 = new Concepto
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            ComprobanteId = comprobanteId,
            Orden = 2,
            ClaveProdServ = "81112101",
            ClaveUnidad = "E48",
            Descripcion = "Servicio de soporte técnico mensual",
            Cantidad = 1,
            ValorUnitario = 3000.00m,
            Importe = 3000.00m,
            Descuento = 0m,
            ObjetoImp = "02",
            Impuestos =
            [
                new ImpuestoConcepto
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    Impuesto = "002",
                    TipoFactor = "Tasa",
                    TasaOCuota = 0.160000m,
                    Base = 3000.00m,
                    Importe = 480.00m,
                    EsRetencion = false
                },
                new ImpuestoConcepto
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    Impuesto = "002",
                    TipoFactor = "Tasa",
                    TasaOCuota = 0.106667m,
                    Base = 3000.00m,
                    Importe = 320.00m,
                    EsRetencion = true
                }
            ]
        };

        return new Comprobante
        {
            Id = comprobanteId,
            EmpresaId = empresaId,

            Estatus = EstatusComprobante.Timbrado.ACadena(),

            TipoDeComprobante = "I",
            FechaEmisionUtc = DateTime.UtcNow,
            LugarExpedicion = "42000",
            Moneda = "MXN",
            TipoCambio = null,
            FormaPago = "03",
            MetodoPago = "PUE",
            Exportacion = "01",

            EmisorRfc = "CPR010101AAA",
            EmisorNombre = "COMERCIALIZADORA DE PRUEBA SA DE CV",
            EmisorRegimenFiscal = "601",

            ReceptorRfc = "XAXX010101000",
            ReceptorNombre = "CLIENTE DE PRUEBA SA DE CV",
            ReceptorRegimenFiscal = "616",
            ReceptorDomicilioFiscal = "42080",
            ReceptorUsoCfdi = "G03",

            Serie = "A",
            Folio = 1024,
            Uuid = Guid.Parse("3AB25E4E-2C7A-4B7F-9C6D-1234567890AB"),
            NoCertificadoEmisor = "00001000000504465028",
            NoCertificadoSat = "00001000000504470606",
            FechaTimbradoUtc = DateTime.UtcNow,

            SubTotal = 28000.00m,
            Descuento = 0m,
            TotalImpuestosTrasladados = 4480.00m,
            TotalImpuestosRetenidos = 320.00m,
            Total = 32160.00m,

            SelloCfd = "MIIB...(sello de prueba simulado)...==",
            SelloSat = "NzY1...(sello SAT de prueba simulado)...==",
            CadenaOriginalSat = "||1.1|3AB25E4E-2C7A-4B7F-9C6D-1234567890AB|2026-09-09T12:00:00|SAT970701NN3|...||",

            CreadoUtc = DateTime.UtcNow,
            CreadoPorUsuarioId = Guid.NewGuid(),

            Conceptos = [concepto1, concepto2]
        };
    }
}