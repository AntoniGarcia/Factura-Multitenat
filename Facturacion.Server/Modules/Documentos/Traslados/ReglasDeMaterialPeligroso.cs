using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;

namespace Facturacion.Server.Modules.Documentos.Traslados;

public static class ReglasDeMaterialPeligroso
{
    public static ErrorNegocio? ValidarFicha(MercanciaCartaPorteDto mercancia)
    {
        if (mercancia.MaterialPeligroso == "Sí")
        {
            if (string.IsNullOrWhiteSpace(mercancia.CveMaterialPeligroso) || string.IsNullOrWhiteSpace(mercancia.Embalaje))
                return ErrorNegocio.Validacion("material-peligroso-incompleto", "Selecciona la clave de material peligroso y su embalaje.");
            if (mercancia.DescripEmbalaje is { } texto && (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > 100 || texto.Contains('|')))
                return ErrorNegocio.Validacion("descripcion-embalaje-invalida", "La descripción del embalaje admite hasta 100 caracteres sin '|'.");
        }
        else if (mercancia.CveMaterialPeligroso is not null || mercancia.Embalaje is not null || mercancia.DescripEmbalaje is not null)
            return ErrorNegocio.Validacion("material-peligroso-no-corresponde", "La clave y el embalaje solo corresponden a material peligroso.");
        return null;
    }

    public static ErrorNegocio? ValidarSeguro(bool peligroso, string? aseguradora, string? poliza)
    {
        if (!peligroso)
            return aseguradora is not null || poliza is not null
                ? ErrorNegocio.Validacion("seguro-ambiental-no-corresponde", "El seguro ambiental solo corresponde a un traslado con material peligroso.") : null;
        if (string.IsNullOrWhiteSpace(aseguradora) || string.IsNullOrWhiteSpace(poliza) ||
            aseguradora.Trim().Length is < 3 or > 50 || poliza.Trim().Length is < 3 or > 30 || aseguradora.Contains('|') || poliza.Contains('|'))
            return ErrorNegocio.Validacion("seguro-ambiental-incompleto", "Registra aseguradora ambiental (3 a 50 caracteres) y póliza (3 a 30), sin '|'.");
        return null;
    }
}
