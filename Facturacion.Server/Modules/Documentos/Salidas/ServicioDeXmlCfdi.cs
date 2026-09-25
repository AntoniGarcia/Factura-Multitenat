using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.ComercioExterior;
using Facturacion.Server.Modules.Documentos.Obras;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>XML ya sellado, con la cadena original con la que se selló.</summary>
public sealed record CfdiSellado(string Xml, string CadenaOriginal, string Sello, string NoCertificado);

/// <summary>
/// Produce el XML del CFDI: lo arma, calcula su cadena original con el XSLT del SAT, lo
/// sella con el CSD de la empresa y lo valida contra el XSD oficial.
///
/// <para><b>El orden importa y no es arbitrario</b></para>
/// <list type="number">
///   <item><description>Se arma el XML con <c>Sello</c> vacío.</description></item>
///   <item><description>Se calcula la cadena original sobre ese documento.</description></item>
///   <item><description>Se firma la cadena y el resultado se escribe en <c>Sello</c>.</description></item>
///   <item><description>Se valida contra el XSD, ya sellado, que es como viajará al PAC.</description></item>
/// </list>
/// Validar antes de sellar dejaría sin comprobar justamente el documento que se envía. Y
/// sellar antes de calcular la cadena es imposible: la cadena se extrae del XML.
///
/// <para><b>La llave privada no sale de este método</b></para>
/// Se abre, se firma y se desecha en el mismo ámbito. No se registra, no se serializa y no
/// vuelve al cliente (ARQUITECTURA.md §4).
/// </summary>
public sealed class ServicioDeXmlCfdi(
    AppDbContext baseDeDatos,
    GeneradorDeXmlCfdi generador,
    GeneradorDeXmlCartaPorte generadorCartaPorte,
    GeneradorDeXmlNotaria generadorNotaria,
    GeneradorDeXmlComercioExterior generadorComercio,
    EsquemasSat esquemas,
    HusoDeEmpresa huso,
    IProveedorCsdParaTimbrado csd,
    ILogger<ServicioDeXmlCfdi> registro)
{
    public async Task<Resultado<CfdiSellado>> GenerarAsync(Comprobante comprobante, CancellationToken ct)
    {
        var esPago = comprobante.TipoDeComprobante == TiposDeComprobante.Pago;

        // Un CFDI de pago no guarda conceptos: lleva uno fijo que sintetiza el generador. Lo
        // que no puede faltarle es el pago en sí.
        if (esPago)
        {
            if (comprobante.Pagos.Count == 0)
                return ErrorNegocio.Regla("pago-sin-datos", "El comprobante de pago no tiene ningún pago.");
        }
        else if (comprobante.Conceptos.Count == 0)
        {
            return ErrorNegocio.Regla("comprobante-sin-conceptos", "El comprobante no tiene conceptos.");
        }

        // La raíz de un pago va en XXX, que no tiene decimales. Los importes que sí llevan
        // dinero viven dentro del complemento y usan la moneda del pago: tomar los decimales
        // de XXX escribiría «1160» donde debe decir «1160.00».
        var monedaDeImportes = esPago ? comprobante.Pagos[0].MonedaP : comprobante.Moneda;

        var decimales = await DecimalesDeMonedaAsync(monedaDeImportes, ct);

        if (decimales is null)
            return ErrorNegocio.Validacion(
                "moneda-desconocida",
                $"La moneda {monedaDeImportes} no está en el catálogo del SAT. ¿Se cargaron los catálogos?");

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Ingreso &&
            comprobante.Estatus is ("borrador" or "error" or "timbrando") &&
            !ProyeccionMonetariaCfdi.Calcular(comprobante, decimales.Value).CoincideCon(comprobante))
            return ErrorNegocio.Regla("totales-del-borrador-desactualizados",
                "Los totales guardados no coinciden con el XML en los decimales de la moneda. Guarda de nuevo el borrador antes de emitirlo.");

        CsdDescifradoDto material;
        try
        {
            material = await csd.ObtenerAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            registro.LogWarning(ex, "No se pudo obtener un CSD activo para preparar el XML.");
            return ErrorNegocio.Regla("csd-no-disponible",
                "Carga un certificado de sello digital vigente para validar el CFDI completo.");
        }

        var zonaHoraria = await huso.ObtenerAsync(ct);
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(comprobante.FechaEmisionUtc, zonaHoraria);
        var datosDeEmision = new DatosDeEmision(
            fechaLocal,
            decimales.Value,
            material.NumeroSerie,
            Convert.ToBase64String(material.CertificadoCer));

        XDocument documento;
        if (comprobante.TipoDeComprobante == TiposDeComprobante.Traslado)
        {
            var traslado = await baseDeDatos.TrasladosCartaPorte
                .AsNoTracking()
                .Include(x => x.Ubicaciones)
                .Include(x => x.Mercancias)
                .FirstOrDefaultAsync(x => x.ComprobanteId == comprobante.Id, ct);

            if (traslado is null)
                return ErrorNegocio.Regla(
                    "traslado-sin-carta-porte",
                    "El CFDI de traslado no tiene los datos de Carta Porte requeridos.");

            var cartaPorte = generadorCartaPorte.Generar(comprobante, datosDeEmision, traslado, zonaHoraria);
            if (cartaPorte.EsFallo) return cartaPorte.Error!;

            documento = cartaPorte.Valor;
        }
        else if (comprobante.TipoDeComprobante == TiposDeComprobante.Ingreso)
        {
            var contenidoComercio = await baseDeDatos.DatosComercioExterior.AsNoTracking()
                .Where(x => x.ComprobanteId == comprobante.Id)
                .Select(x => x.Contenido)
                .FirstOrDefaultAsync(ct);
            var datosNotaria = await baseDeDatos.DatosNotaria
                .AsNoTracking()
                .Include(x => x.Inmuebles)
                .Include(x => x.Partes)
                .FirstOrDefaultAsync(x => x.ComprobanteId == comprobante.Id, ct);
            var datosObra = await baseDeDatos.DatosObra.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ComprobanteId == comprobante.Id, ct);

            if (datosObra is not null)
            {
                if (ValidadorFiscalDeObra.Validar(comprobante, datosObra) is { } errorObra)
                    return errorObra;
                if (contenidoComercio is not null || datosNotaria is not null)
                    return ErrorNegocio.Regla("complementos-incompatibles",
                        "Una estimación de obra no se puede combinar con Notaría o Comercio Exterior en esta factura.");
            }

            if (datosObra?.TipoObra == Facturacion.Shared.Obras.TiposDeObra.Publica)
            {
                documento = generador.Generar(comprobante, datosDeEmision);
                var raiz = documento.Root!;
                raiz.SetAttributeValue(XNamespace.Xmlns + "implocal", EsquemasSat.EspacioDeNombresImpuestosLocales);
                raiz.SetAttributeValue(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation",
                    $"{EsquemasSat.EspacioDeNombresCfdi} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd " +
                    $"{EsquemasSat.EspacioDeNombresImpuestosLocales} http://www.sat.gob.mx/sitio_internet/cfd/implocal/implocal.xsd");
                raiz.Add(new XElement(XNamespace.Get(EsquemasSat.EspacioDeNombresCfdi) + "Complemento",
                    GeneradorDeXmlImpuestosLocalesDeObra.Generar(comprobante)));
            }
            else if (contenidoComercio is not null || comprobante.Exportacion == "02")
            {
                if (datosNotaria is not null)
                    return ErrorNegocio.Regla("complementos-incompatibles",
                        "No se puede combinar Notaría con Comercio Exterior en esta factura.");
                if (contenidoComercio is null)
                    return ErrorNegocio.Regla("comercio-sin-datos",
                        "Guarda los datos de Comercio Exterior antes de validar la factura.");
                var datosComercio = JsonSerializer.Deserialize<DatosComercioExteriorDto>(contenidoComercio);
                if (datosComercio is null)
                    return ErrorNegocio.Regla("comercio-datos-corruptos",
                        "No se pudieron leer los datos de Comercio Exterior guardados.");
                Resultado<XElement> complemento;
                try
                {
                    complemento = generadorComercio.GenerarElemento(comprobante, datosComercio);
                }
                catch (FileNotFoundException ex)
                {
                    registro.LogError(ex, "Falta el esquema SAT de Comercio Exterior.");
                    return ErrorNegocio.Regla("esquemas-sat-incompletos",
                        "Falta un esquema del SAT para validar Comercio Exterior. Avisa a soporte.");
                }
                if (complemento.EsFallo) return complemento.Error!;
                documento = generador.Generar(comprobante, datosDeEmision);
                var raiz = documento.Root!;
                raiz.SetAttributeValue(XNamespace.Xmlns + "cce20", EsquemasSat.EspacioDeNombresComercioExterior20);
                raiz.SetAttributeValue(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation",
                    $"{EsquemasSat.EspacioDeNombresCfdi} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd " +
                    $"{EsquemasSat.EspacioDeNombresComercioExterior20} http://www.sat.gob.mx/sitio_internet/cfd/ComercioExterior20/ComercioExterior20.xsd");
                raiz.Add(new XElement(XNamespace.Get(EsquemasSat.EspacioDeNombresCfdi) + "Complemento", complemento.Valor));
            }
            else if (datosNotaria is null)
                documento = generador.Generar(comprobante, datosDeEmision);
            else
            {
                var notario = await baseDeDatos.ConfiguracionesNotario.AsNoTracking().FirstOrDefaultAsync(ct);
                var notarial = generadorNotaria.Generar(comprobante, datosDeEmision, datosNotaria, notario);
                if (notarial.EsFallo) return notarial.Error!;
                documento = notarial.Valor;
            }
        }
        else
        {
            documento = generador.Generar(comprobante, datosDeEmision);
        }

        string cadena;

        try
        {
            cadena = CadenaOriginal(documento);
        }
        catch (FileNotFoundException ex)
        {
            // Falta un archivo del SAT: es configuración del despliegue, no culpa del usuario.
            registro.LogError(ex, "No se pudo calcular la cadena original por un artefacto del SAT ausente.");

            return ErrorNegocio.Regla(
                "esquemas-sat-incompletos",
                "Falta un archivo del SAT para calcular la cadena original. Avisa a soporte.");
        }

        var sello = Sellar(cadena, material);
        documento.Root!.SetAttributeValue("Sello", sello);

        try
        {
            if (Validar(documento, comprobante.TipoDeComprobante == TiposDeComprobante.Traslado,
                    documento.Descendants(EsquemasSat.EspacioDeNombresNotariosPublicos + "NotariosPublicos").Any(),
                    documento.Descendants(EsquemasSat.EspacioDeNombresComercioExterior20 + "ComercioExterior").Any(),
                    documento.Descendants(EsquemasSat.EspacioDeNombresImpuestosLocales + "ImpuestosLocales").Any()) is { } error)
                return error;
        }
        catch (FileNotFoundException ex)
        {
            registro.LogError(ex, "Falta un esquema SAT para validar el comprobante.");
            return ErrorNegocio.Regla("esquemas-sat-incompletos", "Falta un esquema del SAT para validar el XML. Avisa a soporte.");
        }

        return new CfdiSellado(Serializar(documento), cadena, sello, material.NumeroSerie);
    }

    private async Task<int?> DecimalesDeMonedaAsync(string moneda, CancellationToken ct)
        => await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == moneda)
            .Select(m => (int?)m.Decimales)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Aplica el XSLT oficial. La cadena original <b>no se construye a mano</b> a propósito:
    /// sus reglas de escapado y de omisión de campos vacíos son del SAT, y reimplementarlas
    /// produce sellos que el PAC rechaza por motivos que no se ven en el XML.
    /// </summary>
    private string CadenaOriginal(XDocument documento)
    {
        var salida = new StringWriter();

        using (var lector = documento.CreateReader())
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment
        }))
        {
            esquemas.CadenaOriginal.Transform(lector, escritor);
        }

        return salida.ToString();
    }

    private static string Sellar(string cadena, CsdDescifradoDto material)
    {
        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(material.ContrasenaLlave, material.LlavePrivadaKey, out _);

        // SHA-256 con relleno PKCS#1: es lo que pide el SAT para CFDI 3.3 en adelante.
        var firma = rsa.SignData(
            Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(firma);
    }

    private ErrorNegocio? Validar(XDocument documento, bool esCartaPorte, bool esNotaria, bool esComercio,
        bool esImpuestosLocales)
    {
        var problemas = new List<string>();

        var ajustes = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = esCartaPorte ? esquemas.EsquemaCartaPorte : esNotaria ? esquemas.EsquemaNotaria :
                esComercio ? esquemas.EsquemaCfdiComercioExterior :
                esImpuestosLocales ? esquemas.EsquemaCfdiImpuestosLocales : esquemas.Esquema,
            DtdProcessing = DtdProcessing.Prohibit
        };

        ajustes.ValidationEventHandler += (_, e) => problemas.Add(e.Message);

        using (var lector = XmlReader.Create(new StringReader(Serializar(documento)), ajustes))
        {
            while (lector.Read()) { }
        }

        if (problemas.Count == 0) return null;

        // Al log el detalle completo; al usuario, que el comprobante no cumple el estándar.
        // Un mensaje de XSD no le dice nada a un contador y sí revela la estructura interna.
        registro.LogError(
            "El CFDI generado no cumple el XSD del SAT: {Problemas}", string.Join(" | ", problemas));

        return ErrorNegocio.Regla(
            "cfdi-no-cumple-el-estandar",
            "El comprobante generado no cumple el estándar del SAT. El detalle quedó en la bitácora técnica.");
    }

    private static string Serializar(XDocument documento)
    {
        // UTF-8 sin BOM: el SAT lo exige y un BOM invalida el sello.
        using var salida = new MemoryStream();

        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            Indent = false,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        }))
        {
            documento.Save(escritor);
        }

        return Encoding.UTF8.GetString(salida.ToArray());
    }
}
