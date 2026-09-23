using System.Text.Json;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.ComercioExterior;

public sealed record ArchivoXmlDeComercio(string Nombre, string TipoContenido, byte[] Contenido);

public sealed class ServicioDeComercioExterior(
    AppDbContext db, IContextoEmpresaInterno contexto, IServicioDeBitacora bitacora,
    GeneradorDeXmlComercioExterior generadorXml, ServicioDeXmlCfdi xmlCfdi)
{
    public async Task<Resultado<string>> ValidarXmlCfdiAsync(Guid comprobanteId, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await db.Comprobantes.AsNoTracking()
            .Include(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(x => x.Relacionados)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("comercio-no-validable", "Solo se valida el XML de una factura en borrador o con error.");
        var resultado = await xmlCfdi.GenerarAsync(comprobante, ct);
        return resultado.EsFallo ? resultado.Error! :
            "El CFDI con Comercio Exterior 2.0 cumple el esquema SAT. No se envió al PAC ni se consumió un timbre.";
    }

    public async Task<Resultado<ArchivoXmlDeComercio>> ObtenerXmlBorradorAsync(Guid comprobanteId, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await db.Comprobantes.AsNoTracking().Include(x => x.Conceptos)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("comercio-no-editable", "El XML de borrador solo está disponible antes del timbrado.");
        var contenido = await db.DatosComercioExterior.AsNoTracking()
            .Where(x => x.ComprobanteId == comprobanteId).Select(x => x.Contenido)
            .FirstOrDefaultAsync(ct);
        if (contenido is null)
            return ErrorNegocio.Validacion("comercio-sin-datos", "Guarda los datos de comercio exterior antes de generar el XML.");
        var datos = JsonSerializer.Deserialize<DatosComercioExteriorDto>(contenido);
        if (datos is null)
            return ErrorNegocio.Regla("comercio-datos-corruptos", "No se pudieron leer los datos de comercio exterior guardados.");
        var generado = generadorXml.Generar(comprobante, datos);
        return generado.EsFallo ? generado.Error! :
            new ArchivoXmlDeComercio($"borrador-complemento-comercio-exterior-{comprobanteId:N}.xml",
                "application/xml", generado.Valor!);
    }

    public async Task<Resultado<DatosComercioExteriorDto?>> ObtenerAsync(Guid comprobanteId, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } error) return error;
        if (!await db.Comprobantes.AsNoTracking().AnyAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct))
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");

        var datos = await db.DatosComercioExterior.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        return datos is null ? null : JsonSerializer.Deserialize<DatosComercioExteriorDto>(datos.Contenido);
    }

    public async Task<Resultado<DatosComercioExteriorDto>> GuardarAsync(
        Guid comprobanteId, DatosComercioExteriorDto peticion, CancellationToken ct)
    {
        if (await ValidarLicenciaAsync(ct) is { } licencia) return licencia;
        var comprobante = await db.Comprobantes
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);
        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura.");
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("comercio-no-editable", "Los datos de comercio exterior solo se editan en borrador.");
        if (comprobante.Exportacion != "02")
            return ErrorNegocio.Validacion("comercio-exportacion-invalida", "Guarda la factura como exportación definitiva antes de capturar Comercio Exterior.");

        if (peticion.DomicilioEmisor is null || peticion.DomicilioReceptor is null ||
            !ValidarLongitudes(peticion) ||
            peticion.ClavePedimento is not (null or "A1") ||
            peticion.CertificadoOrigen && string.IsNullOrWhiteSpace(peticion.NumeroCertificadoOrigen) ||
            !ValorValido(peticion.TipoCambioUsd) || !ValorValido(peticion.TotalUsd))
            return ErrorNegocio.Validacion("comercio-datos-invalidos", "Revisa los campos, longitudes, importes y claves de comercio exterior.");

        if (peticion.Incoterm is { } incoterm &&
            !await db.SatIncoterms.AsNoTracking().AnyAsync(x => x.Clave == incoterm && x.Vigente, ct))
            return ErrorNegocio.Validacion("comercio-incoterm-invalido", "Selecciona un INCOTERM vigente del catálogo de Comercio Exterior 2.0.");

        var mercancias = peticion.Mercancias ?? [];
        if (mercancias.Count > 100 || mercancias.Any(x => x is null) ||
            mercancias.Select(x => x.OrdenConcepto).Distinct().Count() != mercancias.Count)
            return ErrorNegocio.Validacion("comercio-mercancias-invalidas", "Revisa las mercancías: no puede haber conceptos repetidos ni más de 100 renglones.");
        var conceptos = await db.Conceptos.AsNoTracking()
            .Where(x => x.ComprobanteId == comprobanteId)
            .Select(x => new { x.Orden, x.ClaveProdServ, x.Descripcion })
            .ToListAsync(ct);
        foreach (var mercancia in mercancias)
        {
            if (!conceptos.Any(x => x.Orden == mercancia.OrdenConcepto &&
                x.ClaveProdServ == mercancia.ClaveProdServConcepto &&
                x.Descripcion == mercancia.DescripcionConcepto))
                return ErrorNegocio.Validacion("comercio-concepto-modificado", "Un concepto cambió desde que capturaste sus datos aduaneros. Revísalo y vuelve a guardar.");
            if (!MercanciaValida(mercancia))
                return ErrorNegocio.Validacion("comercio-mercancia-invalida", $"Revisa las cantidades y longitudes de la mercancía del concepto {mercancia.OrdenConcepto}.");
        }
        var fracciones = mercancias.Select(x => x.FraccionArancelaria).Where(x => x is not null).Distinct().ToArray();
        var unidades = mercancias.Select(x => x.UnidadAduana).Where(x => x is not null).Distinct().ToArray();
        if (fracciones.Length != await db.SatFraccionesArancelarias.AsNoTracking().CountAsync(x => fracciones.Contains(x.Clave) && x.Vigente, ct) ||
            unidades.Length != await db.SatUnidadesAduana.AsNoTracking().CountAsync(x => unidades.Contains(x.Clave) && x.Vigente, ct))
            return ErrorNegocio.Validacion("comercio-catalogo-aduanero-invalido", "Selecciona fracciones arancelarias y unidades aduaneras vigentes del catálogo SAT.");

        var paises = new[] { peticion.ResidenciaFiscal, peticion.DomicilioEmisor.Pais, peticion.DomicilioReceptor.Pais }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (paises.Length > 0)
        {
            var encontrados = await db.SatPaises.AsNoTracking()
                .CountAsync(x => paises.Contains(x.Clave) && x.Vigente, ct);
            if (encontrados != paises.Length)
                return ErrorNegocio.Validacion("comercio-pais-invalido", "Selecciona países vigentes del catálogo SAT.");
        }
        if (peticion.ResidenciaFiscal is not null && peticion.DomicilioReceptor.Pais is not null &&
            peticion.ResidenciaFiscal != peticion.DomicilioReceptor.Pais)
            return ErrorNegocio.Validacion("comercio-residencia-inconsistente", "El país del receptor debe coincidir con su residencia fiscal.");
        if (peticion.DomicilioEmisor.Pais is not (null or "MEX"))
            return ErrorNegocio.Validacion("comercio-emisor-pais-invalido", "El domicilio del emisor debe estar en México.");
        if (peticion.DomicilioEmisor.Estado is { } estado &&
            !await db.SatEstados.AsNoTracking().AnyAsync(x => x.Clave == estado && x.ClavePais == "MEX" && x.Vigente, ct))
            return ErrorNegocio.Validacion("comercio-emisor-estado-invalido", "Selecciona un estado vigente del catálogo SAT.");
        if (peticion.DomicilioEmisor.Municipio is { } municipio &&
            (peticion.DomicilioEmisor.Estado is null ||
             !await db.SatMunicipios.AsNoTracking().AnyAsync(x => x.Clave == municipio &&
                 x.ClaveEstado == peticion.DomicilioEmisor.Estado && x.Vigente, ct)))
            return ErrorNegocio.Validacion("comercio-emisor-municipio-invalido", "Selecciona un municipio del estado indicado.");

        var datos = await db.DatosComercioExterior
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        var antes = datos?.Contenido;
        var contenido = JsonSerializer.Serialize(peticion);
        if (datos is null)
        {
            datos = new DatosComercioExterior
            {
                Id = Guid.NewGuid(), ComprobanteId = comprobanteId, Contenido = contenido
            };
            db.DatosComercioExterior.Add(datos);
        }
        else datos.Contenido = contenido;

        comprobante.ModificadoUtc = DateTime.UtcNow;
        bitacora.Registrar(EntidadesDeBitacora.DatosComercioExterior, datos.Id.ToString(),
            AccionesDeBitacora.DatosComercioExteriorActualizados, antes, contenido);
        await db.SaveChangesAsync(ct);
        return peticion;
    }

    private async Task<ErrorNegocio?> ValidarLicenciaAsync(CancellationToken ct)
        => await db.Empresas.AsNoTracking().AnyAsync(x => x.Id == contexto.EmpresaId && x.LicComercio, ct)
            ? null : ErrorNegocio.Regla("modulo-comercio-no-contratado", "Esta empresa no tiene activo el módulo de Comercio Exterior.");

    private static bool ValorValido(decimal? valor)
        => valor is null || valor is >= 0 and <= 999999999999m && decimal.Round(valor.Value, 6) == valor.Value;

    private static bool Largo(string? texto, int maximo) => texto is null || texto.Length <= maximo;

    private static bool ValidarLongitudes(DatosComercioExteriorDto datos)
        => Largo(datos.ResidenciaFiscal, 3) && Largo(datos.NumeroRegistroTributario, 40) &&
           Largo(datos.ClavePedimento, 2) && Largo(datos.NumeroCertificadoOrigen, 40) &&
           Largo(datos.NumeroExportadorConfiable, 50) &&
           Largo(datos.Observaciones, 300) && Largo(datos.CurpEmisor, 18) &&
           Largo(datos.Incoterm, 10) &&
           DomicilioValido(datos.DomicilioEmisor) && DomicilioValido(datos.DomicilioReceptor);

    private static bool MercanciaValida(MercanciaComercioExteriorDto m)
        => m.OrdenConcepto > 0 && Largo(m.ClaveProdServConcepto, 8) &&
           Largo(m.DescripcionConcepto, 1000) && Largo(m.FraccionArancelaria, 12) &&
           Largo(m.UnidadAduana, 10) && Largo(m.Marca, 35) && Largo(m.Modelo, 80) &&
           Largo(m.Submodelo, 50) && Largo(m.NumeroSerie, 40) &&
           ValorValido(m.CantidadAduana) && ValorValido(m.ValorUnitarioAduana) &&
           ValorValido(m.ValorDolares);

    private static bool DomicilioValido(DomicilioComercioExteriorDto d)
        => Largo(d.Calle, 150) && Largo(d.NumeroExterior, 55) && Largo(d.NumeroInterior, 55) &&
           Largo(d.Colonia, 120) && Largo(d.Localidad, 120) && Largo(d.Referencia, 120) &&
           Largo(d.Municipio, 120) && Largo(d.Estado, 120) && Largo(d.Pais, 3) && Largo(d.CodigoPostal, 30);
}
