using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Pruebas;

public static class ComprobanteDeEjemplo
{
    /*
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
        */

        ///*
        public static Comprobante CrearPago()
    {
        var comprobanteId = Guid.NewGuid();
        var empresaId = Guid.NewGuid();

        // El documento que este pago abona: una factura ya emitida antes, de la que solo
        // necesitamos su UUID/serie/folio y sus saldos (no su Comprobante completo).
        var facturaOriginalUuid = Guid.Parse("3AB25E4E-2C7A-4B7F-9C6D-1234567890AB");

        var documentoPagado = new DocumentoPagado
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            IdDocumento = facturaOriginalUuid,
            Serie = "A",
            Folio = "1024",
            MonedaDR = "MXN",
            EquivalenciaDR = 1m,
            NumParcialidad = 1,
            ImpSaldoAnt = 32160.00m,
            ImpPagado = 32160.00m,
            ImpSaldoInsoluto = 0m,
            ObjetoImpDR = "02",
            Impuestos =
            [
                new ImpuestoDocumentoPagado
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    Impuesto = "002",
                    TipoFactor = "Tasa",
                    TasaOCuota = 0.160000m,
                    Base = 28000.00m,
                    Importe = 4480.00m,
                    EsRetencion = false
                },
                new ImpuestoDocumentoPagado
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

        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            ComprobanteId = comprobanteId,
            FechaPagoUtc = DateTime.UtcNow,
            FormaDePagoP = "03",
            MonedaP = "MXN",
            TipoCambioP = null,
            Monto = 32160.00m,
            NumOperacion = "0001234567",
            RfcEmisorCtaOrd = "BBVA860912XXX", // banco del ordenante, texto de ejemplo
            CtaOrdenante = "012180001234567890",
            CtaBeneficiario = "012180009876543210",
            Documentos = [documentoPagado]
        };
        //*/

        /*
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
        */

        ///*
        return new Comprobante
        {
            Id = comprobanteId,
            EmpresaId = empresaId,

            Estatus = EstatusComprobante.Timbrado.ACadena(),

            TipoDeComprobante = "P",
            FechaEmisionUtc = DateTime.UtcNow,
            LugarExpedicion = "42000",
            Moneda = "XXX", // "sin moneda", tal como fuerza GeneradorDeXmlPago
            TipoCambio = null,
            FormaPago = null,
            MetodoPago = null,
            Exportacion = "01",

            EmisorRfc = "CPR010101AAA",
            EmisorNombre = "COMERCIALIZADORA DE PRUEBA SA DE CV",
            EmisorRegimenFiscal = "601",

            ReceptorRfc = "XAXX010101000",
            ReceptorNombre = "CLIENTE DE PRUEBA SA DE CV",
            ReceptorRegimenFiscal = "616",
            ReceptorDomicilioFiscal = "42080",
            ReceptorUsoCfdi = "CP01", // fijo para pagos, como confirmamos en ServicioDePagos.cs

            Serie = "P",
            Folio = 5,
            Uuid = Guid.NewGuid(),
            NoCertificadoEmisor = "00001000000504465028",
            NoCertificadoSat = "00001000000504470606",
            FechaTimbradoUtc = DateTime.UtcNow,

            SubTotal = 0m,
            Descuento = 0m,
            TotalImpuestosTrasladados = 0m,
            TotalImpuestosRetenidos = 0m,
            Total = 0m,

            SelloCfd = "MIIB...(sello de prueba simulado)...==",
            SelloSat = "NzY1...(sello SAT de prueba simulado)...==",
            CadenaOriginalSat = "||1.1|...cadena de pago simulada...||",

            CreadoUtc = DateTime.UtcNow,
            CreadoPorUsuarioId = Guid.NewGuid(),

            Pagos = [pago]
        };
        //*/
    }
}


/*
 Emmpresa:
        nombre = _empresa.NombreFiscal;
        regimenFiscal = _empresa.RegimenFiscal;
        cpExpedicion = _empresa.CodigoPostalExpedicion;
        zonaHoraria = _empresa.ZonaHoraria;
        calle = _empresa.Calle;
        numeroExterior = _empresa.NumeroExterior;
        numeroInterior = _empresa.NumeroInterior;
        referencia = _empresa.Referencia;
        colonia = _empresa.Colonia;
        localidad = _empresa.Localidad;
        municipio = _empresa.Municipio;
        estado = _empresa.Estado;
        pais = _empresa.Pais;
        codigoPostal = _empresa.CodigoPostal;
        telefono = _empresa.Telefono;
        correoContacto = _empresa.CorreoContacto;
        licNotarios = _empresa.Licencias.Notarios;
        licObras = _empresa.Licencias.Obras;
        licComercio = _empresa.Licencias.Comercio;
        licIne = _empresa.Licencias.Ine;


 Cliente:
            claveInterna = "CLI-001",
            rfc = "XAXX010101000",
            nombre = "Empresa de Prueba S.A. de C.V.",
            regimenFiscal = "601",
            domicilioFiscalCp = "06600",
            residenciaFiscal = null,
            numRegIdTrib = null,
            usoCfdi = "G03",
            metodoPago = "PUE",
            formaPago = "03",
            correo = "cliente.prueba@example.com",
            telefono = "5551234567",
            calle = "Avenida Insurgentes Sur",
            numeroExterior = "123",
            numeroInterior = "Piso 4, Int 401",
            colonia = "Juárez",
            localidad = "Ciudad de México",
            referencia = "Entre calle Nápoles y Dinamarca",
            municipio = "Cuauhtémoc",
            estado = "CMX",
            pais = "MEX",
            codigoPostal = "06600",
            activo = true; claveInterna = "CLI-001",
            rfc = "XAXX010101000",
            nombre = "Empresa de Prueba S.A. de C.V.",
            regimenFiscal = "601",
            domicilioFiscalCp = "06600",
            residenciaFiscal = null,
            numRegIdTrib = null,
            usoCfdi = "G03",
            metodoPago = "PUE",
            formaPago = "03",
            correo = "cliente.prueba@example.com",
            telefono = "5551234567",
            calle = "Avenida Insurgentes Sur",
            numeroExterior = "123",
            numeroInterior = "Piso 4, Int 401",
            colonia = "Juárez",
            localidad = "Ciudad de México",
            referencia = "Entre calle Nápoles y Dinamarca",
            municipio = "Cuauhtémoc";
            estado = "CMX",
            pais = "MEX",
            codigoPostal = "06600",
            activo = true,

 Producto:
        No se de donde salio o donde se use:
           ProductoId = concepto.ProductoId,
           ClaveProdServ = concepto.ClaveProdServ,
           ClaveUnidad = concepto.ClaveUnidad,
           UnidadTexto = concepto.UnidadTexto,
           Descripcion = concepto.Descripcion,
           ObjetoImp = concepto.ObjetoImp,
           ValorUnitario = concepto.ValorUnitario,
           Cantidad = concepto.Cantidad,
           Descuento = concepto.Descuento,
           Impuestos = concepto.Impuestos

        Formulario:
           Identificación = Descripción
           Unidad de medida = (Kilogramo, Litro, etc)
           Clave del SAT =
           Clave de unidad =
           Precio =
           Peso =
           Impuestos = (Sí/No)
                       ("IVA 16 %", "Tasa", 0.16m),
                       ("IVA 8 % frontera", "Tasa", 0.08m),
                       ("IVA 0 %", "Tasa", 0m),
                       ("Exento de IVA", "Exento", null)
 
Nueva factura:
           
         General:
           Tipo de documento = (Factura)
           Folio =
           Serie =
           Moneda =
           Tipo de cambio =

         Datos del cliente:
            R.F.C. =
            Nombre =
            Domicilio fiscal = (C. P.)
            Método de pago =
            Forma de pago =
            Uso de CFDI =

         Conceptos:
            Código = (Código Sat Producto)
            Descripción =
            Unidad =
            Cantidad =
            Precio unitario =
            Descuento =
            Importe = 


         Totales
            Subtotal =
            Descuento =
            Impuestos trasladados = 
            Impuestos retenidos =
            Total =
            Condiciones de pago = (texto opcional)
            Observaciones = (texto opcional)





*/