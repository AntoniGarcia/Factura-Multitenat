using System.Text.RegularExpressions;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Notaria;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Notaria;

/// <summary>Administra el perfil del notario que se reutilizará en los CFDI notariales.</summary>
public sealed class ServicioDeNotaria(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora,
    ServicioDeXmlCfdi xml)
{
    private static readonly Regex PatronCurp = new(
        "^[A-Z]{4}\\d{6}[HM][A-Z]{5}[A-Z0-9]\\d$", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> TiposDeInmueble = ["01", "02", "03", "04", "05"];

    private static readonly Regex PatronCodigoPostal = new("^\\d{5}$", RegexOptions.CultureInvariant);

    private static readonly Regex PatronRfc = new(
        "^[A-ZÑ&]{3,4}\\d{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant);

    public async Task<Resultado<ConfiguracionNotarioDto?>> ObtenerAsync(CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var configuracion = await baseDeDatos.ConfiguracionesNotario
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return configuracion is null ? null : ADto(configuracion);
    }

    public async Task<Resultado<ConfiguracionNotarioDto>> GuardarAsync(
        PeticionGuardarConfiguracionNotario peticion, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var curp = (peticion.Curp ?? string.Empty).Trim().ToUpperInvariant();
        var estado = (peticion.Estado ?? string.Empty).Trim().ToUpperInvariant();
        var adscripcion = Recortar(peticion.Adscripcion);

        if (!PatronCurp.IsMatch(curp) || peticion.NumeroNotaria is < 1 or > 999)
            return ErrorNegocio.Validacion(
                "notario-invalido", "La CURP debe tener 18 caracteres válidos y el número de notaría va de 1 a 999.");

        var estadoValido = await baseDeDatos.SatEstados
            .AsNoTracking()
            .AnyAsync(x => x.Clave == estado && x.ClavePais == "MEX" && x.Vigente, ct);

        if (!estadoValido)
            return ErrorNegocio.Validacion(
                "estado-notario-invalido", "Selecciona una entidad federativa vigente del catálogo SAT.");

        var configuracion = await baseDeDatos.ConfiguracionesNotario.FirstOrDefaultAsync(ct);
        var antes = configuracion is null ? null : ADto(configuracion);

        if (configuracion is null)
        {
            configuracion = new ConfiguracionNotario
            {
                Curp = curp,
                NumeroNotaria = peticion.NumeroNotaria,
                Estado = estado,
                Adscripcion = adscripcion,
                ModificadoUtc = DateTime.UtcNow
            };
            baseDeDatos.ConfiguracionesNotario.Add(configuracion);
        }
        else
        {
            configuracion.Curp = curp;
            configuracion.NumeroNotaria = peticion.NumeroNotaria;
            configuracion.Estado = estado;
            configuracion.Adscripcion = adscripcion;
            configuracion.ModificadoUtc = DateTime.UtcNow;
        }

        var despues = ADto(configuracion);
        bitacora.Registrar(
            EntidadesDeBitacora.ConfiguracionNotario,
            contexto.EmpresaId.ToString(),
            AccionesDeBitacora.ConfiguracionNotarioActualizada,
            antes,
            despues);

        await baseDeDatos.SaveChangesAsync(ct);
        return despues;
    }

    public async Task<Resultado<DatosNotariaDto?>> ObtenerParaComprobanteAsync(
        Guid comprobanteId, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var existe = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .AnyAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);

        if (!existe)
            return ErrorNegocio.NoEncontrado("comprobante-notarial-no-encontrado", "No se encontró una factura para esos datos notariales.");

        var datos = await baseDeDatos.DatosNotaria
            .AsNoTracking()
            .Include(x => x.Inmuebles)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

        return datos is null ? null : ADto(datos);
    }

    public async Task<Resultado<DatosNotariaDto>> GuardarParaComprobanteAsync(
        Guid comprobanteId, PeticionGuardarDatosNotaria peticion, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var comprobante = await baseDeDatos.Comprobantes
            .FirstOrDefaultAsync(x => x.Id == comprobanteId, ct);

        if (comprobante is null || comprobante.TipoDeComprobante != "I")
            return ErrorNegocio.NoEncontrado("comprobante-notarial-no-encontrado", "No se encontró una factura para esos datos notariales.");

        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-notarial-no-editable",
                "Solo se pueden editar los datos notariales de una factura en borrador o con error.");

        var validacion = await ValidarDatosAsync(peticion, ct);
        if (validacion.EsFallo) return validacion.Error!;

        var datos = await baseDeDatos.DatosNotaria
            .Include(x => x.Inmuebles)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);
        var antes = datos is null ? null : ADto(datos);

        if (datos is null)
        {
            datos = new DatosNotaria
            {
                Id = Guid.NewGuid(),
                ComprobanteId = comprobante.Id
            };
            baseDeDatos.DatosNotaria.Add(datos);
        }

        datos.NumeroInstrumentoNotarial = peticion.NumeroInstrumentoNotarial;
        datos.FechaInstrumentoNotarial = peticion.FechaInstrumentoNotarial;
        datos.MontoOperacion = peticion.MontoOperacion;
        datos.SubtotalOperacion = peticion.SubtotalOperacion;
        datos.IvaOperacion = peticion.IvaOperacion;
        datos.Inmuebles.Clear();
        datos.Inmuebles.AddRange(peticion.Inmuebles
            .OrderBy(x => x.Orden)
            .Select(x => new InmuebleNotarial
            {
                Id = Guid.NewGuid(),
                Orden = x.Orden,
                TipoInmueble = x.TipoInmueble.Trim(),
                Calle = x.Calle.Trim(),
                NumeroExterior = Recortar(x.NumeroExterior),
                NumeroInterior = Recortar(x.NumeroInterior),
                Colonia = Recortar(x.Colonia),
                Localidad = Recortar(x.Localidad),
                Referencia = Recortar(x.Referencia),
                Municipio = x.Municipio.Trim(),
                Estado = x.Estado.Trim(),
                Pais = x.Pais.Trim(),
                CodigoPostal = x.CodigoPostal.Trim()
            }));

        comprobante.ModificadoUtc = DateTime.UtcNow;
        var despues = ADto(datos);
        bitacora.Registrar(
            EntidadesDeBitacora.DatosNotaria,
            datos.Id.ToString(),
            AccionesDeBitacora.DatosNotariaActualizados,
            antes,
            despues);

        await baseDeDatos.SaveChangesAsync(ct);
        return despues;
    }

    public async Task<Resultado<PartesNotarialesDto?>> ObtenerPartesAsync(Guid comprobanteId, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var datos = await baseDeDatos.DatosNotaria
            .AsNoTracking()
            .Include(x => x.Partes)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

        return datos is null ? null : APartesDto(datos);
    }

    public async Task<Resultado<PartesNotarialesDto>> GuardarPartesAsync(
        Guid comprobanteId, PeticionGuardarPartesNotariales peticion, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var datos = await baseDeDatos.DatosNotaria
            .Include(x => x.Comprobante)
            .Include(x => x.Partes)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

        if (datos is null)
            return ErrorNegocio.Regla(
                "operacion-notarial-pendiente",
                "Guarda primero los datos de la operación y del inmueble.");

        if (datos.Comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-notarial-no-editable",
                "Solo se pueden editar vendedores y compradores de una factura en borrador o con error.");

        var validacion = ValidarPartes(peticion);
        if (validacion is not null) return validacion;

        var antes = APartesDto(datos);
        datos.EnajenantesEnCopropiedad = peticion.EnajenantesEnCopropiedad;
        datos.AdquirentesEnCopropiedad = peticion.AdquirentesEnCopropiedad;
        datos.Partes.Clear();
        AgregarPartes(datos, "enajenante", peticion.Enajenantes);
        AgregarPartes(datos, "adquirente", peticion.Adquirentes);
        datos.Comprobante.ModificadoUtc = DateTime.UtcNow;

        var despues = APartesDto(datos);
        bitacora.Registrar(
            EntidadesDeBitacora.PartesNotariales,
            datos.Id.ToString(),
            AccionesDeBitacora.PartesNotarialesActualizadas,
            antes,
            despues);
        await baseDeDatos.SaveChangesAsync(ct);
        return despues;
    }

    public async Task<Resultado<string>> ValidarXmlAsync(Guid comprobanteId, CancellationToken ct)
    {
        var licencia = await RequiereLicenciaAsync(ct);
        if (licencia is not null) return licencia;

        var comprobante = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Include(x => x.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(x => x.Relacionados)
            .FirstOrDefaultAsync(x => x.Id == comprobanteId && x.TipoDeComprobante == "I", ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-notarial-no-encontrado", "No se encontró esa factura.");

        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("comprobante-notarial-no-validable",
                "Solo se puede validar el XML de una factura en borrador o con error.");

        if (!await baseDeDatos.DatosNotaria.AnyAsync(x => x.ComprobanteId == comprobanteId, ct))
            return ErrorNegocio.Regla("operacion-notarial-pendiente",
                "Guarda la operación notarial y sus partes antes de validar el XML.");

        var resultado = await xml.GenerarAsync(comprobante, ct);
        return resultado.EsFallo
            ? resultado.Error!
            : "El XML notarial cumple el esquema del SAT. No se envió al PAC ni se consumió ningún timbre.";
    }

    private async Task<Resultado<bool>> ValidarDatosAsync(PeticionGuardarDatosNotaria peticion, CancellationToken ct)
    {
        if (peticion.NumeroInstrumentoNotarial is < 1 or > 999999 ||
            peticion.FechaInstrumentoNotarial == default ||
            peticion.MontoOperacion < 0 || peticion.SubtotalOperacion < 0 || peticion.IvaOperacion < 0)
            return ErrorNegocio.Validacion(
                "operacion-notarial-invalida",
                "Completa el instrumento, su fecha y los importes no negativos de la operación.");

        if (decimal.Round(peticion.SubtotalOperacion + peticion.IvaOperacion, 6, MidpointRounding.ToEven) !=
            decimal.Round(peticion.MontoOperacion, 6, MidpointRounding.ToEven))
            return ErrorNegocio.Validacion(
                "importe-operacion-inconsistente",
                "El monto de la operación debe ser igual al subtotal más el IVA.");

        if (peticion.Inmuebles.Count == 0 ||
            peticion.Inmuebles.Select(x => x.Orden).Distinct().Count() != peticion.Inmuebles.Count ||
            peticion.Inmuebles.Any(x => !EsInmuebleValido(x)))
            return ErrorNegocio.Validacion(
                "inmueble-notarial-invalido",
                "Agrega al menos un inmueble con tipo SAT, calle, municipio, estado, país y código postal válidos.");

        var paises = peticion.Inmuebles.Select(x => x.Pais.Trim()).Distinct().ToArray();
        if (await baseDeDatos.SatPaises.AsNoTracking().CountAsync(x => paises.Contains(x.Clave) && x.Vigente, ct) != paises.Length)
            return ErrorNegocio.Validacion(
                "pais-inmueble-invalido", "Selecciona países vigentes del catálogo SAT.");

        foreach (var inmueble in peticion.Inmuebles)
        {
            var estado = inmueble.Estado.Trim();
            var codigoPostal = inmueble.CodigoPostal.Trim();
            var estadoValido = await baseDeDatos.SatEstados.AsNoTracking()
                .AnyAsync(x => x.Clave == estado && x.ClavePais == "MEX" && x.Vigente, ct);

            if (!estadoValido)
                return ErrorNegocio.Validacion(
                    "estado-inmueble-invalido", "Selecciona una entidad federativa vigente del catálogo SAT.");

            if (inmueble.Pais.Trim() == "MEX")
            {
                var cpValido = await baseDeDatos.SatCodigosPostales.AsNoTracking()
                    .AnyAsync(x => x.Clave == codigoPostal && x.ClaveEstado == estado && x.Vigente, ct);

                if (!cpValido)
                    return ErrorNegocio.Validacion(
                        "codigo-postal-inmueble-invalido",
                        "El código postal no corresponde a la entidad federativa seleccionada en el catálogo SAT.");
            }
        }

        return true;
    }

    private static bool EsInmuebleValido(InmuebleNotarialDto inmueble)
        => inmueble.Orden > 0 &&
           TiposDeInmueble.Contains(inmueble.TipoInmueble.Trim()) &&
           EnRango(inmueble.Calle, 150) &&
           EnRangoOpcional(inmueble.NumeroExterior, 55) &&
           EnRangoOpcional(inmueble.NumeroInterior, 30) &&
           EnRangoOpcional(inmueble.Colonia, 100) &&
           EnRangoOpcional(inmueble.Localidad, 100) &&
           EnRangoOpcional(inmueble.Referencia, 100) &&
           EnRango(inmueble.Municipio, 100) &&
           inmueble.Estado.Trim().Length == 2 &&
           inmueble.Pais.Trim().Length == 3 &&
           PatronCodigoPostal.IsMatch(inmueble.CodigoPostal.Trim());

    private static bool EnRango(string? valor, int maximo)
        => !string.IsNullOrWhiteSpace(valor) && valor.Trim().Length <= maximo;

    private static bool EnRangoOpcional(string? valor, int maximo)
        => string.IsNullOrWhiteSpace(valor) || valor.Trim().Length <= maximo;

    private static ErrorNegocio? ValidarPartes(PeticionGuardarPartesNotariales peticion)
    {
        var errorEnajenantes = ValidarGrupo(
            peticion.Enajenantes, peticion.EnajenantesEnCopropiedad, "vendedor", curpObligatoriaSiIndividual: true,
            apellidoPaternoObligatorioSiIndividual: true);
        if (errorEnajenantes is not null) return errorEnajenantes;

        return ValidarGrupo(
            peticion.Adquirentes, peticion.AdquirentesEnCopropiedad, "comprador", curpObligatoriaSiIndividual: false,
            apellidoPaternoObligatorioSiIndividual: false);
    }

    private static ErrorNegocio? ValidarGrupo(
        IReadOnlyList<ParteNotarialDto> partes,
        bool enCopropiedad,
        string etiqueta,
        bool curpObligatoriaSiIndividual,
        bool apellidoPaternoObligatorioSiIndividual)
    {
        if (partes.Count == 0 || (!enCopropiedad && partes.Count != 1) ||
            partes.Select(x => x.Orden).Distinct().Count() != partes.Count ||
            partes.Select(x => Normalizar(x.Rfc)).Distinct().Count() != partes.Count)
            return ErrorNegocio.Validacion(
                $"{etiqueta}-notarial-invalido",
                $"Captura {(enCopropiedad ? "uno o más" : "una sola")} persona(s) {etiqueta}(as) sin RFC repetido.");

        foreach (var parte in partes)
        {
            var esIndividual = !enCopropiedad;
            if (parte.Orden < 1 || !EnRango(parte.Nombre, 254) ||
                !PatronRfc.IsMatch(Normalizar(parte.Rfc)) ||
                (etiqueta == "vendedor" && Normalizar(parte.Rfc).Length != 13) ||
                !EnRangoOpcional(parte.ApellidoPaterno, 200) || !EnRangoOpcional(parte.ApellidoMaterno, 200) ||
                !EnRangoOpcional(parte.Curp, 18) ||
                (esIndividual && apellidoPaternoObligatorioSiIndividual && !EnRango(parte.ApellidoPaterno, 200)) ||
                (esIndividual && curpObligatoriaSiIndividual && !PatronCurp.IsMatch(Normalizar(parte.Curp))) ||
                (!string.IsNullOrWhiteSpace(parte.Curp) && !PatronCurp.IsMatch(Normalizar(parte.Curp))) ||
                (enCopropiedad && (parte.Porcentaje is < 0 or > 100 ||
                                    decimal.Round(parte.Porcentaje ?? -1m, 2) != parte.Porcentaje)) ||
                (!enCopropiedad && parte.Porcentaje is not null))
                return ErrorNegocio.Validacion(
                    $"{etiqueta}-notarial-invalido",
                    $"Revisa nombre, RFC, CURP y porcentaje de cada {etiqueta}." +
                    (etiqueta == "vendedor" ? " Los vendedores deben ser personas físicas." : string.Empty));
        }

        if (enCopropiedad && decimal.Round(partes.Sum(x => x.Porcentaje ?? 0m), 2) != 100m)
            return ErrorNegocio.Validacion(
                $"porcentaje-{etiqueta}-invalido",
                $"Los porcentajes de {etiqueta}(es) deben sumar exactamente 100 %.");

        return null;
    }

    private static void AgregarPartes(DatosNotaria datos, string rol, IReadOnlyList<ParteNotarialDto> partes)
        => datos.Partes.AddRange(partes.OrderBy(x => x.Orden).Select(x => new ParteNotarial
        {
            Id = Guid.NewGuid(),
            Rol = rol,
            Orden = x.Orden,
            Nombre = x.Nombre.Trim(),
            ApellidoPaterno = Recortar(x.ApellidoPaterno),
            ApellidoMaterno = Recortar(x.ApellidoMaterno),
            Rfc = Normalizar(x.Rfc),
            Curp = Recortar(Normalizar(x.Curp)),
            Porcentaje = x.Porcentaje
        }));

    private static string Normalizar(string? valor) => (valor ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<ErrorNegocio?> RequiereLicenciaAsync(CancellationToken ct)
    {
        var tieneLicencia = await baseDeDatos.Empresas
            .AsNoTracking()
            .AnyAsync(x => x.Id == contexto.EmpresaId && x.LicNotarios, ct);

        return tieneLicencia
            ? null
            : ErrorNegocio.Regla(
                "modulo-notaria-no-contratado",
                "Esta empresa no tiene contratado el módulo de Notaría. Solicítalo al operador del sistema.");
    }

    private static ConfiguracionNotarioDto ADto(ConfiguracionNotario x)
        => new(x.Curp, x.NumeroNotaria, x.Estado, x.Adscripcion);

    private static DatosNotariaDto ADto(DatosNotaria x) => new(
        x.ComprobanteId,
        x.NumeroInstrumentoNotarial,
        x.FechaInstrumentoNotarial,
        x.MontoOperacion,
        x.SubtotalOperacion,
        x.IvaOperacion,
        [.. x.Inmuebles.OrderBy(y => y.Orden).Select(y => new InmuebleNotarialDto(
            y.Id, y.Orden, y.TipoInmueble, y.Calle, y.NumeroExterior, y.NumeroInterior,
            y.Colonia, y.Localidad, y.Referencia, y.Municipio, y.Estado, y.Pais, y.CodigoPostal))]);

    private static PartesNotarialesDto APartesDto(DatosNotaria x) => new(
        x.EnajenantesEnCopropiedad,
        APartesDto(x.Partes, "enajenante"),
        x.AdquirentesEnCopropiedad,
        APartesDto(x.Partes, "adquirente"));

    private static IReadOnlyList<ParteNotarialDto> APartesDto(IEnumerable<ParteNotarial> partes, string rol)
        => [.. partes.Where(x => x.Rol == rol).OrderBy(x => x.Orden).Select(x => new ParteNotarialDto(
            x.Id, x.Orden, x.Nombre, x.ApellidoPaterno, x.ApellidoMaterno, x.Rfc, x.Curp, x.Porcentaje))];

    private static string? Recortar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
