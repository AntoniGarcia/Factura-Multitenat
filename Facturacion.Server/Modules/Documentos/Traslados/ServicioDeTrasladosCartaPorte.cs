using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Transporte;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Traslados;

/// <summary>
/// Guarda borradores de traslado de mercancía propia. La generación del XML Carta Porte y
/// el timbrado viven en una fase posterior; por ello este servicio solo admite borradores.
/// </summary>
public sealed class ServicioDeTrasladosCartaPorte(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioEmpresaEmisora empresaEmisora,
    HusoDeEmpresa huso,
    IServicioDeBitacora bitacora,
    ServicioDeXmlCfdi xml)
{
    private const string TipoTraslado = "T";
    private const string MonedaSinEfectos = "XXX";
    private const string UsoSinEfectosFiscales = "S01";
    private const string SinExportacion = "01";

    // Claves prescritas para el concepto único de un CFDI de traslado con Carta Porte.
    private const string ClaveServicioTraslado = "78101800";
    private const string ClaveUnidadServicio = "E48";

    public async Task<Resultado<TrasladoCartaPorteDto>> CrearAsync(
        PeticionGuardarTrasladoCartaPorte peticion, CancellationToken ct)
    {
        var validacion = await ValidarAsync(peticion, ct);
        if (validacion.EsFallo) return validacion.Error!;

        var emisor = await empresaEmisora.ObtenerParaTimbradoAsync(ct);
        var (salidaUtc, llegadaUtc) = await AFechasUtcAsync(peticion, ct);
        var comprobante = NuevoComprobante(emisor, contexto.UsuarioActual ?? Guid.Empty);
        var traslado = NuevoTraslado(peticion, comprobante, salidaUtc, llegadaUtc, validacion.Valor);

        comprobante.Conceptos.Add(new Concepto
        {
            Id = Guid.NewGuid(),
            Orden = 1,
            ClaveProdServ = ClaveServicioTraslado,
            ClaveUnidad = ClaveUnidadServicio,
            UnidadTexto = "Servicio",
            Descripcion = "Traslado de bienes y/o mercancías",
            Cantidad = 1m,
            ValorUnitario = 0m,
            Importe = 0m,
            Descuento = 0m,
            ObjetoImp = "01"
        });

        baseDeDatos.Comprobantes.Add(comprobante);
        baseDeDatos.TrasladosCartaPorte.Add(traslado);

        bitacora.Registrar(
            EntidadesDeBitacora.TrasladoCartaPorte, traslado.Id.ToString(),
            AccionesDeBitacora.TrasladoCartaPorteCreado,
            despues: ParaBitacora(traslado));

        await baseDeDatos.SaveChangesAsync(ct);
        return ADto(comprobante.Estatus, traslado);
    }

    public async Task<TrasladoCartaPorteDto?> ObtenerAsync(Guid comprobanteId, CancellationToken ct)
    {
        var traslado = await CargarAsync(comprobanteId, ct);
        return traslado is null ? null : ADto(traslado.Comprobante.Estatus, traslado);
    }

    /// <summary>
    /// Arma, sella y valida el XML con el XSD oficial, sin reservar folio, timbre ni llamar al PAC.
    /// El XML no se persiste: el que se envíe algún día al PAC se volverá a generar al apartar el folio.
    /// </summary>
    public async Task<Resultado<ValidacionXmlCartaPorteDto>> ValidarXmlAsync(Guid comprobanteId, CancellationToken ct)
    {
        var traslado = await CargarAsync(comprobanteId, ct);
        if (traslado is null)
            return ErrorNegocio.NoEncontrado("traslado-no-encontrado", "No se encontró ese traslado Carta Porte.");

        if (traslado.Comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "traslado-no-validable", "Solo se puede validar el XML de un traslado en borrador o con error.");

        var resultado = await xml.GenerarAsync(traslado.Comprobante, ct);
        if (resultado.EsFallo)
        {
            if (resultado.Error!.Codigo == "cfdi-no-cumple-el-estandar")
                return ErrorNegocio.Validacion(
                    "carta-porte-xml-invalido",
                    "El XML Carta Porte no cumple el estándar del SAT. Revisa la placa, permiso, seguro, licencia y claves de las mercancías.");

            return resultado.Error;
        }

        return new ValidacionXmlCartaPorteDto(
            "El XML Carta Porte 3.1 es válido contra el esquema del SAT. No se envió al PAC ni se consumió ningún timbre.");
    }

    public async Task<Resultado<TrasladoCartaPorteDto>> ActualizarAsync(
        Guid comprobanteId, PeticionGuardarTrasladoCartaPorte peticion, CancellationToken ct)
    {
        var traslado = await CargarAsync(comprobanteId, ct);
        if (traslado is null)
            return ErrorNegocio.NoEncontrado("traslado-no-encontrado", "No se encontró ese traslado Carta Porte.");

        if (traslado.Comprobante.Estatus != EstatusComprobante.Borrador.ACadena())
            return ErrorNegocio.Conflicto("traslado-no-editable", "Solo se puede editar un traslado en borrador.");

        var validacion = await ValidarAsync(peticion, ct);
        if (validacion.EsFallo) return validacion.Error!;

        var antes = ParaBitacora(traslado);
        var (salidaUtc, llegadaUtc) = await AFechasUtcAsync(peticion, ct);
        traslado.VehiculoId = peticion.VehiculoId;
        traslado.FiguraTransporteId = peticion.FiguraTransporteId;
        traslado.FechaSalidaUtc = salidaUtc;
        traslado.FechaLlegadaUtc = llegadaUtc;
        traslado.DistanciaRecorridaKm = peticion.DistanciaRecorridaKm;
        traslado.PesoBrutoTotalKg = peticion.Mercancias.Sum(x => x.PesoEnKg);
        traslado.TotalMercancias = peticion.Mercancias.Count;
        traslado.IdCcp ??= NuevoIdCcp();
        CopiarDatosDeTransporte(traslado, validacion.Valor);
        ReemplazarDetalles(traslado, peticion, traslado.Comprobante.EmisorRfc);
        traslado.Comprobante.ModificadoUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.TrasladoCartaPorte, traslado.Id.ToString(),
            AccionesDeBitacora.TrasladoCartaPorteActualizado,
            antes, ParaBitacora(traslado));

        await baseDeDatos.SaveChangesAsync(ct);
        return ADto(traslado.Comprobante.Estatus, traslado);
    }

    private async Task<Resultado<RecursosDeTransporte>> ValidarAsync(PeticionGuardarTrasladoCartaPorte p, CancellationToken ct)
    {
        if (p.VehiculoId == Guid.Empty || p.FiguraTransporteId == Guid.Empty ||
            p.DistanciaRecorridaKm <= 0 || p.FechaSalidaLocal == default || p.FechaLlegadaLocal == default ||
            p.FechaLlegadaLocal < p.FechaSalidaLocal)
            return ErrorNegocio.Validacion("traslado-incompleto", "Completa los datos del trayecto, vehículo y operador.");

        if (p.Ubicaciones.Count != 2 || p.Ubicaciones.Count(x => x.Tipo == "Origen") != 1 ||
            p.Ubicaciones.Count(x => x.Tipo == "Destino") != 1)
            return ErrorNegocio.Validacion("ubicaciones-invalidas", "Registra exactamente un origen y un destino.");

        if (p.Mercancias.Count == 0 || p.Mercancias.Any(x => x.Orden < 1 || string.IsNullOrWhiteSpace(x.ClaveProdServ) ||
            string.IsNullOrWhiteSpace(x.ClaveUnidad) || string.IsNullOrWhiteSpace(x.Descripcion) ||
            x.Cantidad <= 0 || x.PesoEnKg <= 0))
            return ErrorNegocio.Validacion("mercancias-invalidas", "Agrega al menos una mercancía con cantidad y peso mayores a cero.");

        if (p.Ubicaciones.Any(x => x.Orden < 1 || string.IsNullOrWhiteSpace(x.Calle) ||
            string.IsNullOrWhiteSpace(x.NumeroExterior) || x.CodigoPostal.Length != 5 ||
            string.IsNullOrWhiteSpace(x.Estado) || string.IsNullOrWhiteSpace(x.Municipio)))
            return ErrorNegocio.Validacion("domicilio-incompleto", "Completa el domicilio de origen y destino.");

        if (p.Ubicaciones.Select(x => x.Orden).Distinct().Count() != p.Ubicaciones.Count ||
            p.Mercancias.Select(x => x.Orden).Distinct().Count() != p.Mercancias.Count)
            return ErrorNegocio.Validacion("orden-repetido", "Cada ubicación y mercancía debe tener un orden distinto.");

        var vehiculo = await baseDeDatos.Vehiculos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == p.VehiculoId && x.Activo, ct);
        var figura = await baseDeDatos.FigurasTransporte.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == p.FiguraTransporteId && x.Activo, ct);
        if (vehiculo is null || figura is null)
            return ErrorNegocio.Validacion("recurso-transporte-inactivo", "El vehículo y el operador deben existir y estar activos.");

        var clavesProducto = p.Mercancias.Select(x => x.ClaveProdServ.Trim()).Distinct().ToArray();
        var clavesUnidad = p.Mercancias.Select(x => x.ClaveUnidad.Trim()).Distinct().ToArray();
        if (await baseDeDatos.SatClavesProdServCartaPorte.CountAsync(x => clavesProducto.Contains(x.Clave) && x.Vigente, ct) != clavesProducto.Length ||
            await baseDeDatos.SatClavesUnidad.CountAsync(x => clavesUnidad.Contains(x.Clave) && x.Vigente, ct) != clavesUnidad.Length)
            return ErrorNegocio.Validacion("catalogo-mercancia-invalido", "La clave Carta Porte o la unidad no está vigente en el SAT.");

        foreach (var ubicacion in p.Ubicaciones)
        {
            var domicilioValido = await baseDeDatos.SatCodigosPostales.AnyAsync(x =>
                x.Clave == ubicacion.CodigoPostal && x.ClaveEstado == ubicacion.Estado &&
                x.ClaveMunicipio == ubicacion.Municipio && x.Vigente, ct);
            if (!domicilioValido)
                return ErrorNegocio.Validacion("domicilio-catalogo-invalido", "El estado, municipio y código postal no coinciden con el catálogo SAT.");
        }

        return new RecursosDeTransporte(vehiculo, figura);
    }

    private async Task<(DateTime SalidaUtc, DateTime LlegadaUtc)> AFechasUtcAsync(
        PeticionGuardarTrasladoCartaPorte peticion, CancellationToken ct)
    {
        var zona = await huso.ObtenerAsync(ct);
        return (
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(peticion.FechaSalidaLocal, DateTimeKind.Unspecified), zona),
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(peticion.FechaLlegadaLocal, DateTimeKind.Unspecified), zona));
    }

    private static Comprobante NuevoComprobante(EmisorFiscalDto emisor, Guid usuarioId) => new()
    {
        Id = Guid.NewGuid(),
        Estatus = EstatusComprobante.Borrador.ACadena(),
        TipoDeComprobante = TipoTraslado,
        FechaEmisionUtc = DateTime.UtcNow,
        LugarExpedicion = emisor.CodigoPostalExpedicion,
        Moneda = MonedaSinEfectos,
        Exportacion = SinExportacion,
        EmisorRfc = emisor.Rfc,
        EmisorNombre = emisor.Nombre,
        EmisorRegimenFiscal = emisor.RegimenFiscal,
        ReceptorRfc = emisor.Rfc,
        ReceptorNombre = emisor.Nombre,
        ReceptorRegimenFiscal = emisor.RegimenFiscal,
        ReceptorDomicilioFiscal = emisor.CodigoPostalExpedicion,
        ReceptorUsoCfdi = UsoSinEfectosFiscales,
        CreadoUtc = DateTime.UtcNow,
        CreadoPorUsuarioId = usuarioId
    };

    private static TrasladoCartaPorte NuevoTraslado(
        PeticionGuardarTrasladoCartaPorte p, Comprobante comprobante, DateTime salidaUtc, DateTime llegadaUtc,
        RecursosDeTransporte recursos)
    {
        var traslado = new TrasladoCartaPorte
        {
            Id = Guid.NewGuid(), ComprobanteId = comprobante.Id, VehiculoId = p.VehiculoId,
            FiguraTransporteId = p.FiguraTransporteId, FechaSalidaUtc = salidaUtc, FechaLlegadaUtc = llegadaUtc,
            DistanciaRecorridaKm = p.DistanciaRecorridaKm, PesoBrutoTotalKg = p.Mercancias.Sum(x => x.PesoEnKg),
            TotalMercancias = p.Mercancias.Count, IdCcp = NuevoIdCcp()
        };
        CopiarDatosDeTransporte(traslado, recursos);
        ReemplazarDetalles(traslado, p, comprobante.EmisorRfc);
        return traslado;
    }

    private static void CopiarDatosDeTransporte(TrasladoCartaPorte traslado, RecursosDeTransporte recursos)
    {
        var vehiculo = recursos.Vehiculo;
        var figura = recursos.Figura;
        traslado.VehiculoConfiguracionAutotransporte = vehiculo.ConfiguracionAutotransporte;
        traslado.VehiculoPlaca = vehiculo.Placa;
        traslado.VehiculoAnioModelo = vehiculo.AnioModelo;
        traslado.VehiculoPesoBruto = vehiculo.PesoBrutoVehicular;
        traslado.VehiculoAseguradora = vehiculo.Aseguradora;
        traslado.VehiculoPoliza = vehiculo.Poliza;
        traslado.VehiculoTipoPermiso = vehiculo.TipoPermiso;
        traslado.VehiculoNumeroPermiso = vehiculo.NumeroPermiso;
        traslado.FiguraTipo = figura.TipoFigura;
        traslado.FiguraRfc = figura.Rfc;
        traslado.FiguraNombre = figura.Nombre;
        traslado.FiguraNumeroLicencia = figura.NumeroLicencia;
    }

    private static void ReemplazarDetalles(
        TrasladoCartaPorte traslado, PeticionGuardarTrasladoCartaPorte p, string rfcDeLaEmpresa)
    {
        traslado.Ubicaciones.Clear();
        traslado.Mercancias.Clear();
        traslado.Ubicaciones.AddRange(p.Ubicaciones.Select(x => new UbicacionCartaPorte
        {
            Id = Guid.NewGuid(), Tipo = x.Tipo, Orden = x.Orden,
            RfcRemitenteDestinatario = rfcDeLaEmpresa, Calle = x.Calle.Trim(),
            NumeroExterior = x.NumeroExterior.Trim(), NumeroInterior = Recortar(x.NumeroInterior),
            Estado = x.Estado.Trim(), Municipio = x.Municipio.Trim(), CodigoPostal = x.CodigoPostal.Trim()
        }));
        traslado.Mercancias.AddRange(p.Mercancias.Select(x => new MercanciaCartaPorte
        {
            Id = Guid.NewGuid(), Orden = x.Orden, ClaveProdServ = x.ClaveProdServ.Trim(),
            Descripcion = x.Descripcion.Trim(), Cantidad = x.Cantidad, ClaveUnidad = x.ClaveUnidad.Trim(), PesoEnKg = x.PesoEnKg
        }));
    }

    private Task<TrasladoCartaPorte?> CargarAsync(Guid comprobanteId, CancellationToken ct)
        => baseDeDatos.TrasladosCartaPorte
            .Include(x => x.Comprobante).ThenInclude(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(x => x.Ubicaciones).Include(x => x.Mercancias)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

    private static TrasladoCartaPorteDto ADto(string estatus, TrasladoCartaPorte x) => new(
        x.ComprobanteId, estatus, x.VehiculoId, x.FiguraTransporteId, x.FechaSalidaUtc, x.FechaLlegadaUtc,
        x.DistanciaRecorridaKm, x.PesoBrutoTotalKg, x.TotalMercancias,
        [.. x.Ubicaciones.OrderBy(y => y.Orden).Select(y => new UbicacionCartaPorteDto(y.Tipo, y.Orden,
            y.RfcRemitenteDestinatario, y.Calle, y.NumeroExterior, y.NumeroInterior, y.Estado, y.Municipio, y.CodigoPostal))],
        [.. x.Mercancias.OrderBy(y => y.Orden).Select(y => new MercanciaCartaPorteDto(y.Orden, y.ClaveProdServ,
            y.Descripcion, y.Cantidad, y.ClaveUnidad, y.PesoEnKg))]);

    private static object ParaBitacora(TrasladoCartaPorte x) => new
    {
        x.ComprobanteId, x.VehiculoId, x.FiguraTransporteId, x.FechaSalidaUtc, x.FechaLlegadaUtc,
        x.IdCcp, x.DistanciaRecorridaKm, x.PesoBrutoTotalKg, x.TotalMercancias
    };

    private static string NuevoIdCcp()
    {
        var id = Guid.NewGuid().ToString("D");
        return "CCC" + id[3..];
    }

    private static string? Recortar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed record RecursosDeTransporte(Vehiculo Vehiculo, FiguraTransporte Figura);
}
