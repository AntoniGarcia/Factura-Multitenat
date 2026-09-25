using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Incorpora Carta Porte 3.1 a un CFDI de traslado nacional por autotransporte propio.
/// No consulta los catálogos maestros: recibe exclusivamente las copias del traslado.
/// </summary>
public sealed class GeneradorDeXmlCartaPorte(GeneradorDeXmlCfdi generadorCfdi)
{
    private static readonly XNamespace Cfdi = EsquemasSat.EspacioDeNombresCfdi;
    private static readonly XNamespace CartaPorte = EsquemasSat.EspacioDeNombresCartaPorte31;
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private const string PaisMexico = "MEX";
    private const string UnidadPesoKilogramo = "KGM";

    public Resultado<XDocument> Generar(
        Comprobante comprobante,
        DatosDeEmision datosDeEmision,
        TrasladoCartaPorte traslado,
        TimeZoneInfo zonaHoraria)
    {
        var error = ValidarDatos(traslado);
        if (error is not null) return error;

        var documento = generadorCfdi.Generar(comprobante, datosDeEmision);
        var raiz = documento.Root!;
        raiz.SetAttributeValue(Xsi + "schemaLocation",
            $"{Cfdi.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd " +
            $"{CartaPorte.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/CartaPorte/CartaPorte31.xsd");
        raiz.SetAttributeValue(XNamespace.Xmlns + "cartaporte31", CartaPorte.NamespaceName);

        var mercancias = traslado.Mercancias.OrderBy(x => x.Orden).ToArray();
        var pesoTotal = mercancias.Sum(x => RedondearPeso(x.PesoEnKg));

        raiz.Add(new XElement(Cfdi + "Complemento",
            new XElement(CartaPorte + "CartaPorte",
                new XAttribute("Version", "3.1"),
                new XAttribute("IdCCP", traslado.IdCcp!),
                new XAttribute("TranspInternac", "No"),
                new XAttribute("TotalDistRec", Decimal(traslado.DistanciaRecorridaKm, 2)),
                Ubicaciones(traslado, zonaHoraria),
                Mercancias(traslado, mercancias, pesoTotal),
                FiguraTransporte(traslado))));

        return documento;
    }

    private static XElement Ubicaciones(TrasladoCartaPorte traslado, TimeZoneInfo zonaHoraria)
        => new(CartaPorte + "Ubicaciones", traslado.Ubicaciones
            .OrderBy(x => x.Orden)
            .Select(ubicacion => new XElement(CartaPorte + "Ubicacion",
                new XAttribute("TipoUbicacion", ubicacion.Tipo),
                new XAttribute("RFCRemitenteDestinatario", ubicacion.RfcRemitenteDestinatario!),
                Opcional("NombreRemitenteDestinatario", ubicacion.NombreRemitenteDestinatario),
                new XAttribute("FechaHoraSalidaLlegada", FechaLocal(ubicacion.Tipo == "Origen"
                    ? traslado.FechaSalidaUtc : traslado.FechaLlegadaUtc, zonaHoraria)),
                ubicacion.Tipo == "Destino"
                    ? new XAttribute("DistanciaRecorrida", Decimal(traslado.DistanciaRecorridaKm, 2))
                    : null,
                new XElement(CartaPorte + "Domicilio",
                    new XAttribute("Calle", ubicacion.Calle),
                    Opcional("NumeroExterior", ubicacion.NumeroExterior),
                    Opcional("NumeroInterior", ubicacion.NumeroInterior),
                    new XAttribute("Municipio", ubicacion.Municipio),
                    new XAttribute("Estado", ubicacion.Estado),
                    new XAttribute("Pais", PaisMexico),
                    new XAttribute("CodigoPostal", ubicacion.CodigoPostal)))));

    private static XElement Mercancias(
        TrasladoCartaPorte traslado, IReadOnlyList<MercanciaCartaPorte> mercancias, decimal pesoTotal)
        => new(CartaPorte + "Mercancias",
            new XAttribute("PesoBrutoTotal", Decimal(pesoTotal, 3)),
            new XAttribute("UnidadPeso", UnidadPesoKilogramo),
            new XAttribute("NumTotalMercancias", mercancias.Count.ToString(CultureInfo.InvariantCulture)),
            mercancias.Select(mercancia => new XElement(CartaPorte + "Mercancia",
                new XAttribute("BienesTransp", mercancia.ClaveProdServ),
                new XAttribute("Descripcion", mercancia.Descripcion),
                new XAttribute("Cantidad", Decimal(mercancia.Cantidad, 6)),
                new XAttribute("ClaveUnidad", mercancia.ClaveUnidad),
                Opcional("Unidad", mercancia.Unidad),
                Opcional("Dimensiones", mercancia.Dimensiones),
                new XAttribute("PesoEnKg", Decimal(RedondearPeso(mercancia.PesoEnKg), 3)))),
            new XElement(CartaPorte + "Autotransporte",
                new XAttribute("PermSCT", traslado.VehiculoTipoPermiso!),
                new XAttribute("NumPermisoSCT", traslado.VehiculoNumeroPermiso!),
                new XElement(CartaPorte + "IdentificacionVehicular",
                    new XAttribute("ConfigVehicular", traslado.VehiculoConfiguracionAutotransporte!),
                    new XAttribute("PesoBrutoVehicular", Decimal(traslado.VehiculoPesoBruto!.Value, 2)),
                    new XAttribute("PlacaVM", LimpiarPlaca(traslado.VehiculoPlaca!)),
                    new XAttribute("AnioModeloVM", traslado.VehiculoAnioModelo!.Value.ToString(CultureInfo.InvariantCulture))),
                new XElement(CartaPorte + "Seguros",
                    new XAttribute("AseguraRespCivil", traslado.VehiculoAseguradora!),
                    new XAttribute("PolizaRespCivil", traslado.VehiculoPoliza!))));

    private static XElement FiguraTransporte(TrasladoCartaPorte traslado)
        => new(CartaPorte + "FiguraTransporte",
            new XElement(CartaPorte + "TiposFigura",
                new XAttribute("TipoFigura", traslado.FiguraTipo!),
                new XAttribute("RFCFigura", traslado.FiguraRfc!),
                Opcional("NumLicencia", traslado.FiguraNumeroLicencia),
                new XAttribute("NombreFigura", traslado.FiguraNombre!)));

    private static ErrorNegocio? ValidarDatos(TrasladoCartaPorte traslado)
    {
        if (traslado.IdCcp is null || traslado.VehiculoAnioModelo is null || traslado.VehiculoPesoBruto is null ||
            traslado.Ubicaciones.Count != 2 || traslado.Mercancias.Count == 0 ||
            traslado.Ubicaciones.Any(x => string.IsNullOrWhiteSpace(x.RfcRemitenteDestinatario)) ||
            new[]
            {
                traslado.VehiculoConfiguracionAutotransporte, traslado.VehiculoPlaca, traslado.VehiculoAseguradora,
                traslado.VehiculoPoliza, traslado.VehiculoTipoPermiso, traslado.VehiculoNumeroPermiso,
                traslado.FiguraTipo, traslado.FiguraRfc, traslado.FiguraNombre
            }.Any(string.IsNullOrWhiteSpace))
            return ErrorNegocio.Regla(
                "carta-porte-datos-incompletos",
                "Guarda de nuevo el traslado después de elegir un vehículo y operador activos antes de validar su XML.");

        var placa = LimpiarPlaca(traslado.VehiculoPlaca!);
        if (traslado.DistanciaRecorridaKm < 0.01m || traslado.DistanciaRecorridaKm > 99999m ||
            Math.Round(traslado.DistanciaRecorridaKm, 2) != traslado.DistanciaRecorridaKm ||
            traslado.VehiculoPesoBruto.Value < 0.01m ||
            Math.Round(traslado.VehiculoPesoBruto.Value, 2) != traslado.VehiculoPesoBruto.Value ||
            placa.Length is < 5 or > 7 || placa.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9')) ||
            (traslado.FiguraTipo == "01" && string.IsNullOrWhiteSpace(traslado.FiguraNumeroLicencia)) ||
            traslado.Mercancias.Any(x => x.Cantidad < 0.000001m || Math.Round(x.Cantidad, 6) != x.Cantidad ||
                x.PesoEnKg < 0.001m || Math.Round(x.PesoEnKg, 3) != x.PesoEnKg))
            return ErrorNegocio.Regla(
                "carta-porte-valores-invalidos",
                "La distancia, vehículo, licencia del operador o las mercancías no cumplen el formato de Carta Porte 3.1. Corrige el borrador o el catálogo de transporte y vuelve a validar.");

        return null;
    }

    private static string FechaLocal(DateTime fechaUtc, TimeZoneInfo zonaHoraria)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), zonaHoraria)
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static decimal RedondearPeso(decimal valor) => Math.Round(valor, 3, MidpointRounding.ToEven);
    private static string Decimal(decimal valor, int decimales) => valor.ToString($"F{decimales}", CultureInfo.InvariantCulture);
    private static XAttribute? Opcional(string nombre, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XAttribute(nombre, valor);

    private static string LimpiarPlaca(string placa)
        => string.Concat(placa.Where(caracter => caracter != '-' && !char.IsWhiteSpace(caracter))).ToUpperInvariant();
}
