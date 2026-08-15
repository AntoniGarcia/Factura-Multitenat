using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>
/// Implementación real de <see cref="IServicioCatalogosSat"/> del contrato congelado
/// (REPARTO-EQUIPO.md §5). En cuanto esta fase cierra, la mitad B sustituye su doble por
/// esto cambiando un registro (REPARTO-EQUIPO.md §7).
/// </summary>
public sealed class ServicioCatalogosSat(AppDbContext db) : IServicioCatalogosSat
{
    public Task<ClaveSatDto?> ResolverAsync(string catalogo, string clave, CancellationToken ct) => catalogo switch
    {
        "c_FormaPago" => ResolverSimpleAsync(db.SatFormasPago, catalogo, clave, ct),
        "c_Exportacion" => ResolverSimpleAsync(db.SatExportaciones, catalogo, clave, ct),
        "c_MetodoPago" => ResolverSimpleAsync(db.SatMetodosPago, catalogo, clave, ct),
        "c_Periodicidad" => ResolverSimpleAsync(db.SatPeriodicidades, catalogo, clave, ct),
        "c_Meses" => ResolverSimpleAsync(db.SatMeses, catalogo, clave, ct),
        "c_TipoRelacion" => ResolverSimpleAsync(db.SatTiposRelacion, catalogo, clave, ct),
        "c_Pais" => ResolverSimpleAsync(db.SatPaises, catalogo, clave, ct),
        "c_ObjetoImp" => ResolverSimpleAsync(db.SatObjetosImp, catalogo, clave, ct),
        "c_Moneda" => ResolverSimpleAsync(db.SatMonedas, catalogo, clave, ct),
        "c_TipoDeComprobante" => ResolverSimpleAsync(db.SatTiposDeComprobante, catalogo, clave, ct),
        "c_RegimenFiscal" => ResolverSimpleAsync(db.SatRegimenesFiscales, catalogo, clave, ct),
        "c_UsoCFDI" => ResolverSimpleAsync(db.SatUsosCfdi, catalogo, clave, ct),
        "c_ClaveProdServ" => ResolverSimpleAsync(db.SatClavesProdServ, catalogo, clave, ct),
        "c_Impuesto" => ResolverSimpleAsync(db.SatImpuestos, catalogo, clave, ct),

        "c_ClaveUnidad" => ResolverClaveUnidadAsync(clave, ct),
        "c_TipoFactor" => ResolverTipoFactorAsync(clave, ct),
        "c_TasaOCuota" => ResolverTasaOCuotaAsync(clave, ct),
        "c_CodigoPostal" => ResolverCodigoPostalAsync(clave, ct),

        _ => throw new ArgumentOutOfRangeException(nameof(catalogo), catalogo, "Catálogo del SAT desconocido.")
    };

    public Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, int tope, CancellationToken ct)
    {
        if (texto.Length < 3)
            throw new ArgumentException("La búsqueda exige al menos tres caracteres.", nameof(texto));

        return catalogo switch
        {
            "c_FormaPago" => BuscarSimpleAsync(db.SatFormasPago, catalogo, texto, tope, ct),
            "c_Exportacion" => BuscarSimpleAsync(db.SatExportaciones, catalogo, texto, tope, ct),
            "c_MetodoPago" => BuscarSimpleAsync(db.SatMetodosPago, catalogo, texto, tope, ct),
            "c_Periodicidad" => BuscarSimpleAsync(db.SatPeriodicidades, catalogo, texto, tope, ct),
            "c_Meses" => BuscarSimpleAsync(db.SatMeses, catalogo, texto, tope, ct),
            "c_TipoRelacion" => BuscarSimpleAsync(db.SatTiposRelacion, catalogo, texto, tope, ct),
            "c_Pais" => BuscarSimpleAsync(db.SatPaises, catalogo, texto, tope, ct),
            "c_ObjetoImp" => BuscarSimpleAsync(db.SatObjetosImp, catalogo, texto, tope, ct),
            "c_Moneda" => BuscarSimpleAsync(db.SatMonedas, catalogo, texto, tope, ct),
            "c_TipoDeComprobante" => BuscarSimpleAsync(db.SatTiposDeComprobante, catalogo, texto, tope, ct),
            "c_RegimenFiscal" => BuscarSimpleAsync(db.SatRegimenesFiscales, catalogo, texto, tope, ct),
            "c_UsoCFDI" => BuscarSimpleAsync(db.SatUsosCfdi, catalogo, texto, tope, ct),
            "c_Impuesto" => BuscarSimpleAsync(db.SatImpuestos, catalogo, texto, tope, ct),

            // Con índice de texto completo (CLAUDE.md §7): ~52,000 renglones no se buscan con LIKE.
            "c_ClaveProdServ" => BuscarClaveProdServAsync(texto, tope, ct),
            "c_ClaveUnidad" => BuscarClaveUnidadAsync(texto, tope, ct),
            "c_CodigoPostal" => BuscarCodigoPostalAsync(texto, tope, ct),

            "c_TipoFactor" or "c_TasaOCuota" => throw new NotSupportedException(
                $"'{catalogo}' no se busca por texto: no tiene una descripción libre que buscar. Resuélvelo por clave."),

            _ => throw new ArgumentOutOfRangeException(nameof(catalogo), catalogo, "Catálogo del SAT desconocido.")
        };
    }

    public async Task<bool> EsUsoCfdiCompatibleAsync(
        string usoCfdi, string regimenReceptor, bool esPersonaMoral, CancellationToken ct)
    {
        var uso = await db.SatUsosCfdi.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Clave == usoCfdi && u.Vigente, ct);

        if (uso is null) return false;
        if (esPersonaMoral && !uso.AplicaMoral) return false;
        if (!esPersonaMoral && !uso.AplicaFisica) return false;

        return uso.EsCompatibleConRegimen(regimenReceptor);
    }

    // ── Los catálogos con forma "clave + descripción + vigencia" ────────────────────────

    private static async Task<ClaveSatDto?> ResolverSimpleAsync<TEntidad>(
        DbSet<TEntidad> tabla, string catalogo, string clave, CancellationToken ct)
        where TEntidad : class, ISatCatalogoSimple
    {
        var e = await tabla.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        return e is null ? null : new ClaveSatDto(catalogo, e.Clave, e.Descripcion, e.Vigente);
    }

    private static async Task<IReadOnlyList<ClaveSatDto>> BuscarSimpleAsync<TEntidad>(
        DbSet<TEntidad> tabla, string catalogo, string texto, int tope, CancellationToken ct)
        where TEntidad : class, ISatCatalogoSimple
    {
        return await tabla.AsNoTracking()
            .Where(x => x.Vigente && (x.Clave.Contains(texto) || x.Descripcion.Contains(texto)))
            .OrderBy(x => x.Clave)
            .Take(tope)
            .Select(x => new ClaveSatDto(catalogo, x.Clave, x.Descripcion, x.Vigente))
            .ToListAsync(ct);
    }

    // ── ClaveProdServ — texto completo sobre Descripcion y PalabrasSimilares ───────────

    private async Task<IReadOnlyList<ClaveSatDto>> BuscarClaveProdServAsync(string texto, int tope, CancellationToken ct)
    {
        var patron = PatronDeBusquedaDeTexto(texto);

        return await db.SatClavesProdServ.AsNoTracking()
            .Where(x => x.Vigente && (EF.Functions.Contains(x.Descripcion, patron) || EF.Functions.Contains(x.PalabrasSimilares!, patron)))
            .OrderBy(x => x.Clave)
            .Take(tope)
            .Select(x => new ClaveSatDto("c_ClaveProdServ", x.Clave, x.Descripcion, x.Vigente))
            .ToListAsync(ct);
    }

    // ── ClaveUnidad — el nombre para mostrar vive en Nombre, no en Descripcion ────────

    private async Task<ClaveSatDto?> ResolverClaveUnidadAsync(string clave, CancellationToken ct)
    {
        var e = await db.SatClavesUnidad.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        return e is null ? null : new ClaveSatDto("c_ClaveUnidad", e.Clave, e.Nombre, e.Vigente);
    }

    private async Task<IReadOnlyList<ClaveSatDto>> BuscarClaveUnidadAsync(string texto, int tope, CancellationToken ct)
    {
        return await db.SatClavesUnidad.AsNoTracking()
            .Where(x => x.Vigente && (x.Clave.Contains(texto) || x.Nombre.Contains(texto)))
            .OrderBy(x => x.Clave)
            .Take(tope)
            .Select(x => new ClaveSatDto("c_ClaveUnidad", x.Clave, x.Nombre, x.Vigente))
            .ToListAsync(ct);
    }

    // ── TipoFactor — la clave ya es la descripción ────────────────────────────────────

    private async Task<ClaveSatDto?> ResolverTipoFactorAsync(string clave, CancellationToken ct)
    {
        var e = await db.SatTiposFactor.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        return e is null ? null : new ClaveSatDto("c_TipoFactor", e.Clave, e.Clave, e.Vigente);
    }

    // ── TasaOCuota — se resuelve por la clave sintética que arma el importador ───────

    private async Task<ClaveSatDto?> ResolverTasaOCuotaAsync(string clave, CancellationToken ct)
    {
        var e = await db.SatTasasOCuota.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        return e is null
            ? null
            : new ClaveSatDto("c_TasaOCuota", e.Clave, $"{e.Impuesto} {e.Factor} {e.ValorMaximo:0.######}", e.Vigente);
    }

    // ── CódigoPostal — la búsqueda por texto es sobre colonia y municipio, no sobre el CP ──

    private async Task<ClaveSatDto?> ResolverCodigoPostalAsync(string clave, CancellationToken ct)
    {
        var e = await db.SatCodigosPostales.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        if (e is null) return null;

        var estado = await db.SatEstados.AsNoTracking()
            .Where(x => x.Clave == e.ClaveEstado)
            .Select(x => x.Nombre)
            .FirstOrDefaultAsync(ct);

        return new ClaveSatDto("c_CodigoPostal", e.Clave, estado ?? e.ClaveEstado, e.Vigente);
    }

    /// <summary>
    /// Busca por dígitos del código postal, o por nombre de colonia o de municipio con
    /// texto completo (CLAUDE.md §7). Cada código postal puede tener muchas colonias; el
    /// resultado se agrupa por código postal para no repetir el mismo CP una vez por colonia.
    /// </summary>
    private async Task<IReadOnlyList<ClaveSatDto>> BuscarCodigoPostalAsync(string texto, int tope, CancellationToken ct)
    {
        var estados = await db.SatEstados.AsNoTracking().ToDictionaryAsync(x => x.Clave, x => x.Nombre, ct);
        var resultado = new Dictionary<string, ClaveSatDto>(tope);

        if (texto.All(char.IsDigit))
        {
            var porCp = await db.SatCodigosPostales.AsNoTracking()
                .Where(x => x.Vigente && x.Clave.StartsWith(texto))
                .OrderBy(x => x.Clave)
                .Take(tope)
                .Select(x => new { x.Clave, x.ClaveEstado })
                .ToListAsync(ct);

            foreach (var cp in porCp)
                resultado[cp.Clave] = new ClaveSatDto(
                    "c_CodigoPostal", cp.Clave, estados.GetValueOrDefault(cp.ClaveEstado, cp.ClaveEstado), true);

            return resultado.Values.ToList();
        }

        var patron = PatronDeBusquedaDeTexto(texto);

        // La descripción muestra la colonia o el municipio que de verdad hizo match, no una
        // colonia cualquiera del mismo código postal: mostrar la equivocada parece un error
        // del buscador aunque el código postal sea el correcto.
        var porColonia = await db.SatColonias.AsNoTracking()
            .Where(x => x.Vigente && EF.Functions.Contains(x.Nombre, patron))
            .OrderBy(x => x.Nombre)
            .Select(x => new { x.ClaveCodigoPostal, x.Nombre })
            .Take(tope * 3) // holgura: varias colonias pueden caer en el mismo código postal
            .ToListAsync(ct);

        if (porColonia.Count > 0)
        {
            var cps = porColonia.Select(x => x.ClaveCodigoPostal).Distinct().ToList();
            var estadoPorCp = await db.SatCodigosPostales.AsNoTracking()
                .Where(x => cps.Contains(x.Clave))
                .ToDictionaryAsync(x => x.Clave, x => x.ClaveEstado, ct);

            foreach (var c in porColonia)
            {
                if (resultado.Count >= tope) break;
                if (resultado.ContainsKey(c.ClaveCodigoPostal)) continue;

                var estado = estadoPorCp.TryGetValue(c.ClaveCodigoPostal, out var claveEstado)
                    ? estados.GetValueOrDefault(claveEstado, claveEstado)
                    : "";
                resultado[c.ClaveCodigoPostal] = new ClaveSatDto(
                    "c_CodigoPostal", c.ClaveCodigoPostal, $"{c.Nombre}, {estado}", true);
            }
        }

        if (resultado.Count < tope)
        {
            var porMunicipio = await db.SatMunicipios.AsNoTracking()
                .Where(m => m.Vigente && EF.Functions.Contains(m.Descripcion, patron))
                .Select(m => new { m.Clave, m.ClaveEstado, m.Descripcion })
                .ToListAsync(ct);

            foreach (var m in porMunicipio)
            {
                if (resultado.Count >= tope) break;

                var cps = await db.SatCodigosPostales.AsNoTracking()
                    .Where(cp => cp.Vigente && cp.ClaveMunicipio == m.Clave && cp.ClaveEstado == m.ClaveEstado)
                    .Select(cp => cp.Clave)
                    .Take(tope - resultado.Count)
                    .ToListAsync(ct);

                foreach (var cp in cps)
                {
                    if (resultado.ContainsKey(cp) || resultado.Count >= tope) continue;
                    var estado = estados.GetValueOrDefault(m.ClaveEstado, m.ClaveEstado);
                    resultado[cp] = new ClaveSatDto("c_CodigoPostal", cp, $"{m.Descripcion}, {estado}", true);
                }
            }
        }

        return resultado.Values.OrderBy(x => x.Clave).ToList();
    }

    /// <summary>
    /// <c>CONTAINS</c> de SQL Server no acepta texto libre sin envolver: se arma como
    /// prefijo (<c>"texto*"</c>) para que "rom" encuentre "Roma" mientras se sigue
    /// escribiendo, igual que espera <c>BuscadorCatalogo</c> (CLAUDE.md §7).
    /// </summary>
    private static string PatronDeBusquedaDeTexto(string texto)
        => $"\"{texto.Replace("\"", "")}*\"";
}
