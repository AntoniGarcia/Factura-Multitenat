using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Datos fiscales, domicilio y configuración de la empresa activa.
/// <para>
/// La empresa sobre la que trabaja sale siempre del claim del token, nunca de un
/// identificador que llegue del navegador (CLAUDE.md §4).
/// </para>
/// </summary>
public sealed class ServicioDeEmpresa(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora)
{
    public async Task<EmpresaDto?> ObtenerAsync(CancellationToken ct)
    {
        var empresa = await CargarAsync(ct);
        return empresa is null ? null : AEmpresaDto(empresa);
    }

    public async Task<Resultado<RespuestaGuardarEmpresa>> GuardarAsync(
        PeticionGuardarEmpresa peticion, CancellationToken ct)
    {
        var empresa = await CargarAsync(ct);

        if (empresa is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "No se encontró la empresa activa.");

        var nombre = NombreFiscal.Normalizar(peticion.NombreFiscal);

        if (string.IsNullOrWhiteSpace(nombre.Normalizado))
            return ErrorNegocio.Validacion("nombre-vacio", "Escribe el nombre o razón social de la empresa.");

        var validacion = await ValidarFiscalesAsync(empresa.Rfc, peticion, ct);
        if (validacion is not null) return validacion;

        var antes = AEmpresaDto(empresa);

        empresa.NombreFiscal = nombre.Normalizado;
        empresa.RegimenFiscal = peticion.RegimenFiscal;
        empresa.CodigoPostalExpedicion = peticion.CodigoPostalExpedicion;
        empresa.ZonaHoraria = peticion.ZonaHoraria;

        empresa.Calle = Recortar(peticion.Calle);
        empresa.NumeroExterior = Recortar(peticion.NumeroExterior);
        empresa.NumeroInterior = Recortar(peticion.NumeroInterior);
        empresa.Referencia = Recortar(peticion.Referencia);
        empresa.Colonia = Recortar(peticion.Colonia);
        empresa.Localidad = Recortar(peticion.Localidad);
        empresa.Municipio = Recortar(peticion.Municipio);
        empresa.Estado = Recortar(peticion.Estado);
        empresa.Pais = Recortar(peticion.Pais);
        empresa.CodigoPostal = Recortar(peticion.CodigoPostal);
        empresa.Telefono = Recortar(peticion.Telefono);
        empresa.CorreoContacto = Recortar(peticion.CorreoContacto);

        empresa.LicNotarios = peticion.Licencias.Notarios;
        empresa.LicObras = peticion.Licencias.Obras;
        empresa.LicComercio = peticion.Licencias.Comercio;
        empresa.LicINE = peticion.Licencias.Ine;

        var despues = AEmpresaDto(empresa);

        bitacora.Registrar(
            EntidadesDeBitacora.Empresa, empresa.Id.ToString(), AccionesDeBitacora.EmpresaActualizada,
            antes, despues, empresaId: empresa.Id);

        await baseDeDatos.SaveChangesAsync(ct);

        return new RespuestaGuardarEmpresa(despues, nombre);
    }

    public async Task<ConfiguracionEmpresaDto> ObtenerConfiguracionAsync(CancellationToken ct)
    {
        var configuracion = await baseDeDatos.ConfiguracionesEmpresa
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        // Sin renglón todavía significa «nunca se ha configurado», no un error: se
        // devuelven los valores por omisión del modelo.
        return configuracion is null
            ? new ConfiguracionEmpresaDto(0.160000m, 0m, 0m, 30)
            : AConfiguracionDto(configuracion);
    }

    public async Task<Resultado<ConfiguracionEmpresaDto>> GuardarConfiguracionAsync(
        ConfiguracionEmpresaDto peticion, CancellationToken ct)
    {
        var error = ValidarConfiguracion(peticion);
        if (error is not null) return error;

        var configuracion = await baseDeDatos.ConfiguracionesEmpresa.FirstOrDefaultAsync(ct);
        var antes = configuracion is null ? null : AConfiguracionDto(configuracion);

        if (configuracion is null)
        {
            // EmpresaId lo pone el interceptor de sellado desde el claim; no se asigna aquí
            // para que no haya dos lugares que decidan a qué empresa pertenece un renglón.
            configuracion = new ConfiguracionEmpresa();
            baseDeDatos.ConfiguracionesEmpresa.Add(configuracion);
        }

        configuracion.TasaIvaPorDefecto = peticion.TasaIvaPorDefecto;
        configuracion.TasaRetencionIvaPorDefecto = peticion.TasaRetencionIvaPorDefecto;
        configuracion.TasaRetencionIsrPorDefecto = peticion.TasaRetencionIsrPorDefecto;
        configuracion.DiasAvisoCaducidadCertificado = peticion.DiasAvisoCaducidadCertificado;

        bitacora.Registrar(
            EntidadesDeBitacora.ConfiguracionEmpresa, contexto.EmpresaId.ToString(),
            AccionesDeBitacora.ConfiguracionActualizada, antes, peticion);

        await baseDeDatos.SaveChangesAsync(ct);

        return peticion;
    }

    private Task<Empresa?> CargarAsync(CancellationToken ct)
        => baseDeDatos.Empresas.FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct);

    /// <summary>
    /// Las tres validaciones que rompen timbrados: régimen del catálogo, régimen compatible
    /// con el tipo de persona que implica el RFC, y código postal de expedición existente
    /// (CLAUDE.md §7).
    /// </summary>
    private async Task<ErrorNegocio?> ValidarFiscalesAsync(
        string rfc, PeticionGuardarEmpresa peticion, CancellationToken ct)
    {
        var regimen = await baseDeDatos.SatRegimenesFiscales
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Clave == peticion.RegimenFiscal, ct);

        if (regimen is null)
            return ErrorNegocio.Validacion("regimen-desconocido",
                $"El régimen fiscal {peticion.RegimenFiscal} no está en el catálogo del SAT.");

        // La longitud del RFC es lo que distingue persona moral (12) de física (13).
        var esMoral = rfc.Length == 12;

        if (esMoral && !regimen.AplicaMoral)
            return ErrorNegocio.Validacion("regimen-incompatible",
                $"El régimen «{regimen.Descripcion}» es solo para personas físicas, y este RFC es de persona moral.");

        if (!esMoral && !regimen.AplicaFisica)
            return ErrorNegocio.Validacion("regimen-incompatible",
                $"El régimen «{regimen.Descripcion}» es solo para personas morales, y este RFC es de persona física.");

        var existeCp = await baseDeDatos.SatCodigosPostales
            .AsNoTracking()
            .AnyAsync(c => c.Clave == peticion.CodigoPostalExpedicion, ct);

        if (!existeCp)
            return ErrorNegocio.Validacion("cp-desconocido",
                $"El código postal {peticion.CodigoPostalExpedicion} no está en el catálogo del SAT.");

        if (!EsZonaHorariaConocida(peticion.ZonaHoraria))
            return ErrorNegocio.Validacion("zona-horaria-desconocida",
                "El huso horario del lugar de expedición no es válido.");

        return null;
    }

    private static ErrorNegocio? ValidarConfiguracion(ConfiguracionEmpresaDto peticion)
    {
        // Las tasas viajan como fracción: 0.16 es 16 %. Una tasa mayor que 1 casi siempre
        // significa que alguien escribió «16» esperando por ciento, y dejarlo pasar produce
        // un comprobante con 1600 % de IVA.
        if (peticion.TasaIvaPorDefecto is < 0 or > 1)
            return ErrorNegocio.Validacion("iva-invalido", "La tasa de IVA va entre 0 y 1: 0.16 es 16 %.");

        if (peticion.TasaRetencionIvaPorDefecto is < 0 or > 1)
            return ErrorNegocio.Validacion("retencion-iva-invalida", "La retención de IVA va entre 0 y 1.");

        if (peticion.TasaRetencionIsrPorDefecto is < 0 or > 1)
            return ErrorNegocio.Validacion("retencion-isr-invalida", "La retención de ISR va entre 0 y 1.");

        if (peticion.DiasAvisoCaducidadCertificado is < 1 or > 365)
            return ErrorNegocio.Validacion("dias-aviso-invalidos",
                "Los días de aviso de caducidad van entre 1 y 365.");

        return null;
    }

    private static bool EsZonaHorariaConocida(string zona)
    {
        if (string.IsNullOrWhiteSpace(zona)) return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zona);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string? Recortar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static EmpresaDto AEmpresaDto(Empresa e) => new(
        e.Id, e.Rfc, e.NombreFiscal, e.RegimenFiscal, e.CodigoPostalExpedicion, e.ZonaHoraria,
        e.Calle, e.NumeroExterior, e.NumeroInterior, e.Referencia, e.Colonia, e.Localidad,
        e.Municipio, e.Estado, e.Pais, e.CodigoPostal, e.Telefono, e.CorreoContacto,
        e.LogoRuta is not null, e.LogoNombreOriginal,
        new LicenciasDto(e.LicNotarios, e.LicObras, e.LicComercio, e.LicINE));

    private static ConfiguracionEmpresaDto AConfiguracionDto(ConfiguracionEmpresa c) => new(
        c.TasaIvaPorDefecto, c.TasaRetencionIvaPorDefecto,
        c.TasaRetencionIsrPorDefecto, c.DiasAvisoCaducidadCertificado);
}
