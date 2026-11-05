using System.Text.RegularExpressions;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.ComercioExterior;

public sealed class ValidadorFiscalDeComercioExterior(AppDbContext db)
{
    public async Task<ErrorNegocio?> ValidarAsync(Comprobante comprobante, DatosComercioExteriorDto datos,
        CancellationToken ct, bool completo = true)
    {
        if (ReglasDeComercioExterior.ValidarCabecera(comprobante, datos, completo) is { } cabecera) return cabecera;
        if (datos.Incoterm is { Length: > 0 } incoterm &&
            !await db.SatIncoterms.AsNoTracking().AnyAsync(x => x.Clave == incoterm && x.Vigente, ct))
            return ErrorNegocio.Validacion("comercio-incoterm-invalido", "Selecciona un INCOTERM vigente.");
        if (datos.DomicilioEmisor is null || datos.DomicilioReceptor is null)
            return ErrorNegocio.Validacion("comercio-domicilio-incompleto", "Completa los domicilios del emisor y del receptor.");

        var emisor = datos.DomicilioEmisor;
        if (emisor.Pais is { Length: > 0 } && emisor.Pais != "MEX")
            return ErrorNegocio.Validacion("comercio-emisor-pais-invalido", "El domicilio del emisor debe estar en México.");
        if (emisor.CodigoPostal is { Length: > 0 } cp)
        {
            var postal = await db.SatCodigosPostales.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == cp && x.Vigente, ct);
            if (postal is null || postal.ClaveEstado != emisor.Estado || postal.ClaveMunicipio != emisor.Municipio ||
                !string.IsNullOrEmpty(emisor.Localidad) && postal.ClaveLocalidad != emisor.Localidad)
                return ErrorNegocio.Validacion("comercio-emisor-cp-inconsistente", "El código postal debe corresponder al estado, municipio y localidad del emisor.");
            if (emisor.Colonia is { Length: 4 } colonia && colonia.All(char.IsAsciiDigit) &&
                !await db.SatColonias.AsNoTracking().AnyAsync(x => x.Clave == colonia && x.ClaveCodigoPostal == cp && x.Vigente, ct))
                return ErrorNegocio.Validacion("comercio-emisor-colonia-invalida", "La clave de colonia no corresponde al código postal del emisor.");
        }

        var receptor = datos.DomicilioReceptor;
        if (receptor.Pais is { Length: > 0 } paisReceptor)
        {
            var pais = await db.SatPaises.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == paisReceptor && x.Vigente, ct);
            if (pais is null)
                return ErrorNegocio.Validacion("comercio-pais-invalido", "Selecciona un país vigente del receptor.");
            if (!string.IsNullOrWhiteSpace(datos.NumeroExportadorConfiable) &&
                pais.Agrupaciones?.Contains("Unión Europea", StringComparison.OrdinalIgnoreCase) != true)
                return ErrorNegocio.Validacion("comercio-exportador-no-aplica", "El número de exportador confiable requiere un receptor de la Unión Europea.");
            if (receptor.Estado is { Length: > 0 } estado &&
                await db.SatEstados.AsNoTracking().AnyAsync(x => x.ClavePais == paisReceptor && x.Vigente, ct) &&
                !await db.SatEstados.AsNoTracking().AnyAsync(x => x.ClavePais == paisReceptor && x.Clave == estado && x.Vigente, ct))
                return ErrorNegocio.Validacion("comercio-receptor-estado-invalido", "El estado o provincia no pertenece al país del receptor.");
            if (receptor.CodigoPostal is { Length: > 0 } codigo && !Coincide(codigo, pais.FormatoCodigoPostal))
                return ErrorNegocio.Validacion("comercio-receptor-cp-invalido", "El código postal no cumple el formato del país del receptor.");
            if (string.IsNullOrWhiteSpace(pais.ValidacionRegistroTributario) &&
                datos.NumeroRegistroTributario is { Length: > 0 } registro && !Coincide(registro, pais.FormatoRegistroTributario))
                return ErrorNegocio.Validacion("comercio-registro-tributario-invalido", "El registro tributario no cumple el formato del país del receptor.");
        }

        var fecha = DateOnly.FromDateTime(comprobante.FechaEmisionUtc == default ? DateTime.UtcNow : comprobante.FechaEmisionUtc);
        foreach (var mercancia in datos.Mercancias ?? [])
        {
            var concepto = comprobante.Conceptos.FirstOrDefault(c => c.Orden == mercancia.OrdenConcepto);
            if (concepto is null) return ErrorNegocio.Validacion("comercio-concepto-modificado", "Revisa las mercancías y los conceptos guardados.");
            if (completo && ReglasDeComercioExterior.ValidarMercancia(concepto, mercancia) is { } errorMercancia) return errorMercancia;
            if (mercancia.FraccionArancelaria is { Length: > 0 } clave)
            {
                var fraccion = await db.SatFraccionesArancelarias.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
                if (fraccion is null || !fraccion.Vigente && fraccion.FechaFinVigencia is null ||
                    fraccion.FechaInicioVigencia > fecha || fraccion.FechaFinVigencia is { } fin && fin < fecha)
                    return ErrorNegocio.Validacion("comercio-fraccion-no-vigente", "La fracción arancelaria no está vigente para la fecha del comprobante.");
                if (string.IsNullOrWhiteSpace(fraccion.UnidadMedida))
                    return ErrorNegocio.Regla("comercio-catalogo-sin-umt", "Actualiza el catálogo de fracciones arancelarias para cargar su UMT.");
                var unidad = mercancia.UnidadAduana ?? (completo ? concepto.UnidadTexto : null);
                if (unidad is not null && unidad != fraccion.UnidadMedida)
                    return ErrorNegocio.Validacion("comercio-unidad-fraccion-inconsistente", "La unidad aduanera debe coincidir con la UMT de la fracción arancelaria.");
            }
            if (mercancia.UnidadAduana is { Length: > 0 } aduana &&
                !await db.SatUnidadesAduana.AsNoTracking().AnyAsync(x => x.Clave == aduana && x.Vigente, ct))
                return ErrorNegocio.Validacion("comercio-unidad-invalida", "Selecciona una unidad aduanera vigente.");
        }
        return null;
    }

    private static bool Coincide(string valor, string? patron)
    {
        if (string.IsNullOrWhiteSpace(patron)) return true;
        try
        {
            return Regex.IsMatch(valor, "\\A(?:" + patron + ")\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException) { return false; }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
