using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioCatalogosSat"/> para pruebas en Development.
/// Devuelve datos reales del SAT hardcodeados para los catálogos más comunes.
/// </summary>
public sealed class DobleServicioCatalogosSat : IServicioCatalogosSat
{
    public Task<ClaveSatDto?> ResolverAsync(
        string catalogo, string clave, CancellationToken ct)
    {
        var resultado = catalogo switch
        {
            "c_ClaveProdServ" => ResolverClaveProdServ(clave),
            "c_ClaveUnidad" => ResolverClaveUnidad(clave),
            "c_Impuesto" => ResolverImpuesto(clave),
            "c_TipoFactor" => ResolverTipoFactor(clave),
            "c_Moneda" => ResolverMoneda(clave),
            "c_FormaPago" => ResolverFormaPago(clave),
            "c_MetodoPago" => ResolverMetodoPago(clave),
            "c_RegimenFiscal" => ResolverRegimenFiscal(clave),
            "c_UsoCFDI" => ResolverUsoCfdi(clave),
            "c_TipoDeComprobante" => ResolverTipoDeComprobante(clave),
            _ => null
        };

        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(
        string catalogo, string texto, int tope, CancellationToken ct)
    {
        var resultados = catalogo switch
        {
            "c_ClaveProdServ" => BuscarClaveProdServ(texto, tope),
            "c_ClaveUnidad" => BuscarClaveUnidad(texto, tope),
            "c_Moneda" => BuscarMoneda(texto, tope),
            "c_RegimenFiscal" => BuscarRegimenFiscal(texto, tope),
            "c_UsoCFDI" => BuscarUsoCfdi(texto, tope),
            _ => []
        };

        return Task.FromResult(resultados);
    }

    public Task<bool> EsUsoCfdiCompatibleAsync(
        string usoCfdi, string regimenReceptor, bool esPersonaMoral, CancellationToken ct)
    {
        // Simplificado: la mayoría de usos son compatibles con la mayoría de regímenes.
        // G01 (adquisición de mercancias) no es compatible con personas físicas en algunos casos,
        // pero para pruebas rápidas esto es suficiente.
        if (usoCfdi == "S01" && regimenReceptor == "616")
            return Task.FromResult(true);

        if (usoCfdi == "G01" && regimenReceptor == "601")
            return Task.FromResult(true);

        if (usoCfdi.StartsWith("P"))
            return Task.FromResult(true);

        return Task.FromResult(esPersonaMoral || !new[] { "S01", "S02", "S03" }.Contains(usoCfdi));
    }

    private static ClaveSatDto? ResolverClaveProdServ(string clave) =>
        clave switch
        {
            "84101600" => new ClaveSatDto(
                Catalogo: "c_ClaveProdServ",
                Clave: "84101600",
                Descripcion: "Servicios de consultoría",
                Vigente: true),
            "01010101" => new ClaveSatDto(
                Catalogo: "c_ClaveProdServ",
                Clave: "01010101",
                Descripcion: "Provision de agua",
                Vigente: true),
            "80131700" => new ClaveSatDto(
                Catalogo: "c_ClaveProdServ",
                Clave: "80131700",
                Descripcion: "Servicios de educación",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverClaveUnidad(string clave) =>
        clave switch
        {
            "H87" => new ClaveSatDto(
                Catalogo: "c_ClaveUnidad",
                Clave: "H87",
                Descripcion: "Prestación de Servicios",
                Vigente: true),
            "KGM" => new ClaveSatDto(
                Catalogo: "c_ClaveUnidad",
                Clave: "KGM",
                Descripcion: "Kilogramo",
                Vigente: true),
            "LTR" => new ClaveSatDto(
                Catalogo: "c_ClaveUnidad",
                Clave: "LTR",
                Descripcion: "Litro",
                Vigente: true),
            "PZA" => new ClaveSatDto(
                Catalogo: "c_ClaveUnidad",
                Clave: "PZA",
                Descripcion: "Pieza",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverImpuesto(string clave) =>
        clave switch
        {
            "002" => new ClaveSatDto(
                Catalogo: "c_Impuesto",
                Clave: "002",
                Descripcion: "IVA",
                Vigente: true),
            "001" => new ClaveSatDto(
                Catalogo: "c_Impuesto",
                Clave: "001",
                Descripcion: "ISR",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverTipoFactor(string clave) =>
        clave switch
        {
            "Tasa" => new ClaveSatDto(
                Catalogo: "c_TipoFactor",
                Clave: "Tasa",
                Descripcion: "Tasa",
                Vigente: true),
            "Cuota" => new ClaveSatDto(
                Catalogo: "c_TipoFactor",
                Clave: "Cuota",
                Descripcion: "Cuota Fija",
                Vigente: true),
            "Exento" => new ClaveSatDto(
                Catalogo: "c_TipoFactor",
                Clave: "Exento",
                Descripcion: "Exento",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverMoneda(string clave) =>
        clave switch
        {
            "MXN" => new ClaveSatDto(
                Catalogo: "c_Moneda",
                Clave: "MXN",
                Descripcion: "Peso Mexicano",
                Vigente: true),
            "USD" => new ClaveSatDto(
                Catalogo: "c_Moneda",
                Clave: "USD",
                Descripcion: "Dólar estadounidense",
                Vigente: true),
            "EUR" => new ClaveSatDto(
                Catalogo: "c_Moneda",
                Clave: "EUR",
                Descripcion: "Euro",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverFormaPago(string clave) =>
        clave switch
        {
            "01" => new ClaveSatDto(
                Catalogo: "c_FormaPago",
                Clave: "01",
                Descripcion: "Efectivo",
                Vigente: true),
            "02" => new ClaveSatDto(
                Catalogo: "c_FormaPago",
                Clave: "02",
                Descripcion: "Cheque nominativo",
                Vigente: true),
            "03" => new ClaveSatDto(
                Catalogo: "c_FormaPago",
                Clave: "03",
                Descripcion: "Transferencia electrónica de fondos",
                Vigente: true),
            "04" => new ClaveSatDto(
                Catalogo: "c_FormaPago",
                Clave: "04",
                Descripcion: "Tarjeta de crédito",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverMetodoPago(string clave) =>
        clave switch
        {
            "PUE" => new ClaveSatDto(
                Catalogo: "c_MetodoPago",
                Clave: "PUE",
                Descripcion: "Pago en una exhibición",
                Vigente: true),
            "PPD" => new ClaveSatDto(
                Catalogo: "c_MetodoPago",
                Clave: "PPD",
                Descripcion: "Pago en parcialidades o diferido",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverRegimenFiscal(string clave) =>
        clave switch
        {
            "601" => new ClaveSatDto(
                Catalogo: "c_RegimenFiscal",
                Clave: "601",
                Descripcion: "General de Ley Personas Morales",
                Vigente: true),
            "616" => new ClaveSatDto(
                Catalogo: "c_RegimenFiscal",
                Clave: "616",
                Descripcion: "Simplificado",
                Vigente: true),
            "605" => new ClaveSatDto(
                Catalogo: "c_RegimenFiscal",
                Clave: "605",
                Descripcion: "Personas Físicas con Actividades Empresariales y Profesionales",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverUsoCfdi(string clave) =>
        clave switch
        {
            "S01" => new ClaveSatDto(
                Catalogo: "c_UsoCFDI",
                Clave: "S01",
                Descripcion: "Sin efecto fiscal",
                Vigente: true),
            "G01" => new ClaveSatDto(
                Catalogo: "c_UsoCFDI",
                Clave: "G01",
                Descripcion: "Adquisición de mercancias",
                Vigente: true),
            "P01" => new ClaveSatDto(
                Catalogo: "c_UsoCFDI",
                Clave: "P01",
                Descripcion: "Por cuenta de terceros",
                Vigente: true),
            "CP01" => new ClaveSatDto(
                Catalogo: "c_UsoCFDI",
                Clave: "CP01",
                Descripcion: "Pagos",
                Vigente: true),
            _ => null
        };

    private static ClaveSatDto? ResolverTipoDeComprobante(string clave) =>
        clave switch
        {
            "I" => new ClaveSatDto(
                Catalogo: "c_TipoDeComprobante",
                Clave: "I",
                Descripcion: "Ingreso",
                Vigente: true),
            "E" => new ClaveSatDto(
                Catalogo: "c_TipoDeComprobante",
                Clave: "E",
                Descripcion: "Egreso",
                Vigente: true),
            "T" => new ClaveSatDto(
                Catalogo: "c_TipoDeComprobante",
                Clave: "T",
                Descripcion: "Traslado",
                Vigente: true),
            "P" => new ClaveSatDto(
                Catalogo: "c_TipoDeComprobante",
                Clave: "P",
                Descripcion: "Pago",
                Vigente: true),
            _ => null
        };

    private static IReadOnlyList<ClaveSatDto> BuscarClaveProdServ(string texto, int tope)
    {
        var candidatos = new[]
        {
            new ClaveSatDto("c_ClaveProdServ", "84101600", "Servicios de consultoría", true),
            new ClaveSatDto("c_ClaveProdServ", "01010101", "Provision de agua", true),
            new ClaveSatDto("c_ClaveProdServ", "80131700", "Servicios de educación", true),
            new ClaveSatDto("c_ClaveProdServ", "78101500", "Servicios de consultoría en informática", true),
            new ClaveSatDto("c_ClaveProdServ", "78111000", "Servicios de procesamiento de datos", true),
        };

        var filtrados = candidatos
            .Where(c => c.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                       c.Clave.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(tope)
            .ToList();

        return filtrados;
    }

    private static IReadOnlyList<ClaveSatDto> BuscarClaveUnidad(string texto, int tope)
    {
        var candidatos = new[]
        {
            new ClaveSatDto("c_ClaveUnidad", "H87", "Prestación de Servicios", true),
            new ClaveSatDto("c_ClaveUnidad", "KGM", "Kilogramo", true),
            new ClaveSatDto("c_ClaveUnidad", "LTR", "Litro", true),
            new ClaveSatDto("c_ClaveUnidad", "PZA", "Pieza", true),
            new ClaveSatDto("c_ClaveUnidad", "MTR", "Metro", true),
        };

        var filtrados = candidatos
            .Where(c => c.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                       c.Clave.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(tope)
            .ToList();

        return filtrados;
    }

    private static IReadOnlyList<ClaveSatDto> BuscarMoneda(string texto, int tope)
    {
        var candidatos = new[]
        {
            new ClaveSatDto("c_Moneda", "MXN", "Peso Mexicano", true),
            new ClaveSatDto("c_Moneda", "USD", "Dólar estadounidense", true),
            new ClaveSatDto("c_Moneda", "EUR", "Euro", true),
        };

        var filtrados = candidatos
            .Where(c => c.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                       c.Clave.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(tope)
            .ToList();

        return filtrados;
    }

    private static IReadOnlyList<ClaveSatDto> BuscarRegimenFiscal(string texto, int tope)
    {
        var candidatos = new[]
        {
            new ClaveSatDto("c_RegimenFiscal", "601", "General de Ley Personas Morales", true),
            new ClaveSatDto("c_RegimenFiscal", "616", "Simplificado", true),
            new ClaveSatDto("c_RegimenFiscal", "605", "Personas Físicas con Actividades Empresariales y Profesionales", true),
        };

        var filtrados = candidatos
            .Where(c => c.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                       c.Clave.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(tope)
            .ToList();

        return filtrados;
    }

    private static IReadOnlyList<ClaveSatDto> BuscarUsoCfdi(string texto, int tope)
    {
        var candidatos = new[]
        {
            new ClaveSatDto("c_UsoCFDI", "S01", "Sin efecto fiscal", true),
            new ClaveSatDto("c_UsoCFDI", "G01", "Adquisición de mercancias", true),
            new ClaveSatDto("c_UsoCFDI", "P01", "Por cuenta de terceros", true),
            new ClaveSatDto("c_UsoCFDI", "CP01", "Pagos", true),
        };

        var filtrados = candidatos
            .Where(c => c.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                       c.Clave.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(tope)
            .ToList();

        return filtrados;
    }
}
