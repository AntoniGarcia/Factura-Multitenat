using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Catalogos;

public sealed class ServicioDeCatalogosDeOperador(AppDbContext db)
{
    public async Task<IReadOnlyList<CatalogoSatDeOperadorDto>> ListarAsync(CancellationToken ct)
    {
        var cargas = await db.CatalogoVersiones.AsNoTracking().Select(x => new CatalogoVersionDto(
            x.Catalogo, x.VersionCatalogo, x.RevisionCatalogo, x.FechaPublicacion, x.FechaCargaUtc, x.RenglonesVigentes)).ToListAsync(ct);
        var porClave = cargas.ToDictionary(x => x.Catalogo, StringComparer.OrdinalIgnoreCase);
        return [.. CatalogosSatDeConsulta.Disponibles.Select(x => new CatalogoSatDeOperadorDto(x, porClave.GetValueOrDefault(x.Clave)))];
    }

    public async Task<Resultado<PaginaCatalogoSatDto>> ConsultarAsync(
        string catalogo, string? texto, bool? vigentes, int pagina, int tamano, CancellationToken ct)
    {
        var definicion = CatalogosSatDeConsulta.Disponibles.FirstOrDefault(x => x.Clave == catalogo);
        if (definicion is null)
            return ErrorNegocio.Validacion("catalogo-invalido", "Ese catálogo no está disponible para consulta.");
        var filtro = (texto ?? string.Empty).Trim();
        if (filtro.Length > 150)
            return ErrorNegocio.Validacion("texto-largo", "La búsqueda admite hasta 150 caracteres.");
        if (filtro.Length > 0 && !filtro.Any(char.IsLetterOrDigit))
            return ErrorNegocio.Validacion("texto-invalido", "Escribe una clave o descripción para buscar.");
        if (definicion.RequiereBusqueda && filtro.Length < 3)
            return ErrorNegocio.Validacion("texto-corto", "Escribe al menos tres caracteres para consultar este catálogo.");

        var consulta = Consulta(catalogo, filtro);
        if (vigentes is { } vigente) consulta = consulta.Where(x => x.Vigente == vigente);
        if (filtro.Length > 0 && catalogo is not ("c_ClaveProdServ" or "c_CodigoPostal" or "c_Colonia"))
            consulta = consulta.Where(x => x.Clave.StartsWith(filtro) || x.Descripcion.Contains(filtro) ||
                (x.Contexto != null && x.Contexto.Contains(filtro)));
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderBy(x => x.Clave).ThenBy(x => x.Contexto)
            .Skip(Math.Clamp(pagina, 0, 100000) * Math.Clamp(tamano, 1, 50)).Take(Math.Clamp(tamano, 1, 50))
            .Select(x => new FilaCatalogoSatDto(x.Clave, x.Descripcion, x.Contexto, x.Vigente, x.Inicio, x.Fin)).ToListAsync(ct);
        return new PaginaCatalogoSatDto(filas, total);
    }

    private IQueryable<Fila> Consulta(string catalogo, string texto) => catalogo switch
    {
        "c_FormaPago" => Simples(db.SatFormasPago), "c_MetodoPago" => Simples(db.SatMetodosPago),
        "c_UsoCFDI" => Simples(db.SatUsosCfdi), "c_RegimenFiscal" => Simples(db.SatRegimenesFiscales),
        "c_Moneda" => db.SatMonedas.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Descripcion,
            Contexto = "Decimales: " + x.Decimales, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        "c_ObjetoImp" => Simples(db.SatObjetosImp), "c_TipoRelacion" => Simples(db.SatTiposRelacion),
        "c_Periodicidad" => Simples(db.SatPeriodicidades), "c_Meses" => Simples(db.SatMeses),
        "c_Exportacion" => Simples(db.SatExportaciones), "c_TipoDeComprobante" => Simples(db.SatTiposDeComprobante),
        "c_Impuesto" => Simples(db.SatImpuestos), "c_Pais" => Simples(db.SatPaises),
        "c_ConfigAutotransporte" => Simples(db.SatConfiguracionesAutotransporte), "c_TipoPermiso" => Simples(db.SatTiposPermiso),
        "c_FiguraTransporte" => Simples(db.SatFigurasTransporte), "c_ClaveProdServCP" => Simples(db.SatClavesProdServCartaPorte),
        "c_INCOTERM" => Simples(db.SatIncoterms), "c_UnidadAduana" => Simples(db.SatUnidadesAduana),
        "c_FraccionArancelaria" => Simples(db.SatFraccionesArancelarias),
        "c_ClaveProdServ" => Productos(texto), "c_CodigoPostal" => CodigosPostales(texto), "c_Colonia" => Colonias(texto),
        "c_ClaveUnidad" => db.SatClavesUnidad.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Nombre,
            Contexto = x.Simbolo, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        "c_TipoFactor" => db.SatTiposFactor.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Clave,
            Contexto = null, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        "c_TasaOCuota" => db.SatTasasOCuota.AsNoTracking().Select(x => new Fila { Clave = x.Clave,
            Descripcion = x.Impuesto + " · " + x.Factor + " · " + x.RangoOFijo,
            Contexto = "Mínimo: " + x.ValorMinimo + " · Máximo: " + x.ValorMaximo +
                (x.Traslado ? " · Traslado" : "") + (x.Retencion ? " · Retención" : ""),
            Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        "c_Estado" => db.SatEstados.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Nombre,
            Contexto = x.ClavePais, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        "c_Municipio" => db.SatMunicipios.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Descripcion,
            Contexto = x.ClaveEstado, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia }),
        _ => throw new InvalidOperationException("Falta la consulta del catálogo autorizado.")
    };

    private static IQueryable<Fila> Simples<T>(DbSet<T> tabla) where T : class, ISatCatalogoSimple
        => tabla.AsNoTracking().Select(x => new Fila { Clave = x.Clave, Descripcion = x.Descripcion,
            Contexto = null, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia });

    private IQueryable<Fila> Productos(string texto)
    {
        var patron = Patron(texto);
        var consulta = db.SatClavesProdServ.AsNoTracking();
        consulta = texto.All(char.IsDigit) ? consulta.Where(x => x.Clave.StartsWith(texto))
            : consulta.Where(x => EF.Functions.Contains(x.Descripcion, patron) || EF.Functions.Contains(x.PalabrasSimilares!, patron));
        return Simples(consulta);
    }

    private static IQueryable<Fila> Simples<T>(IQueryable<T> tabla) where T : class, ISatCatalogoSimple
        => tabla.Select(x => new Fila { Clave = x.Clave, Descripcion = x.Descripcion,
            Contexto = null, Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia });

    private IQueryable<Fila> Colonias(string texto)
    {
        var patron = Patron(texto);
        var consulta = db.SatColonias.AsNoTracking();
        consulta = texto.All(char.IsDigit)
            ? consulta.Where(x => x.ClaveCodigoPostal.StartsWith(texto) || x.Clave.StartsWith(texto))
            : consulta.Where(x => EF.Functions.Contains(x.Nombre, patron));
        return consulta.Select(x => new Fila { Clave = x.Clave, Descripcion = x.Nombre, Contexto = x.ClaveCodigoPostal,
            Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia });
    }

    private IQueryable<Fila> CodigosPostales(string texto)
    {
        var patron = Patron(texto);
        var consulta = db.SatCodigosPostales.AsNoTracking();
        consulta = texto.All(char.IsDigit) ? consulta.Where(x => x.Clave.StartsWith(texto))
            : consulta.Where(x => db.SatEstados.Any(e => e.Clave == x.ClaveEstado && (e.Nombre.Contains(texto) || e.Clave == texto)) ||
                db.SatColonias.Any(c => c.ClaveCodigoPostal == x.Clave && EF.Functions.Contains(c.Nombre, patron)) ||
                db.SatMunicipios.Any(m => m.Clave == x.ClaveMunicipio && m.ClaveEstado == x.ClaveEstado && EF.Functions.Contains(m.Descripcion, patron)));
        return consulta.Select(x => new Fila { Clave = x.Clave,
            Descripcion = db.SatEstados.Where(e => e.Clave == x.ClaveEstado).Select(e => e.Nombre).FirstOrDefault() ?? x.ClaveEstado,
            Contexto = "Municipio: " + (x.ClaveMunicipio ?? "—") + " · Localidad: " + (x.ClaveLocalidad ?? "—"),
            Vigente = x.Vigente, Inicio = x.FechaInicioVigencia, Fin = x.FechaFinVigencia });
    }

    private static string Patron(string texto) => $"\"{texto.Replace("\"", "")}*\"";

    private sealed class Fila
    {
        public string Clave { get; init; } = "";
        public string Descripcion { get; init; } = "";
        public string? Contexto { get; init; }
        public bool Vigente { get; init; }
        public DateOnly Inicio { get; init; }
        public DateOnly? Fin { get; init; }
    }
}
