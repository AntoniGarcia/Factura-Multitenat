using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Traslados;

public sealed class ValidadorFiscalCartaPorte(AppDbContext db)
{
    public static Resultado<string?> ValidarPeligrosidad(string? indicador, string? declaracion, bool guardada = false)
    {
        if (declaracion is not (null or "Sí" or "No"))
            return ErrorNegocio.Validacion("mercancia-peligrosidad-invalida", "Selecciona Sí o No en material peligroso.");
        if (indicador is not ("0" or "1" or "0,1"))
            return ErrorNegocio.Regla("catalogo-porte-sin-indicador", "Actualiza las claves de productos Carta Porte para cargar su clasificación.");
        if ((indicador is "0,1" or "1") && declaracion is null)
            return ErrorNegocio.Validacion("mercancia-peligrosidad-pendiente", "Confirma si la mercancía es material peligroso según su composición.");
        if ((indicador == "1" && declaracion != "Sí") || (indicador == "0" && declaracion == "Sí"))
            return ErrorNegocio.Validacion("mercancia-peligrosidad-incompatible", "La declaración de peligrosidad no coincide con la clasificación SAT.");
        if (indicador == "0" && guardada && declaracion is not null)
            return ErrorNegocio.Validacion("mercancia-clasificacion-cambiada", "La clasificación SAT cambió. Revisa y guarda nuevamente el borrador.");
        return indicador == "0" ? (string?)null : declaracion;
    }

    public async Task<Resultado<IReadOnlyDictionary<string, string>>> ValidarAsync(string configuracion,
        IReadOnlyList<MercanciaCartaPorteDto> mercancias, CancellationToken ct, bool guardadas = false,
        IReadOnlyList<RemolqueCartaPorteDto>? remolques = null, string? aseguradora = null, string? poliza = null)
    {
        remolques ??= [];
        if (ReglasDeRemolques.ValidarDatos(remolques) is { } errorDatos) return errorDatos;
        var vehiculo = await db.SatConfiguracionesAutotransporte.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Clave == configuracion && x.Vigente, ct);
        if (ReglasDeRemolques.ValidarConfiguracion(vehiculo?.Remolque, remolques.Count) is { } errorConfiguracion)
            return errorConfiguracion;
        var subtipos = remolques.Select(x => x.SubTipoRem.Trim()).Distinct().ToArray();
        if (subtipos.Length > 0 && await db.SatSubTiposRemolque.CountAsync(x => subtipos.Contains(x.Clave) && x.Vigente, ct) != subtipos.Length)
            return ErrorNegocio.Validacion("subtipo-remolque-invalido", "Selecciona un subtipo de remolque vigente del SAT.");

        var renglones = mercancias.ToArray();
        var claves = renglones.Select(x => x.ClaveProdServ.Trim()).Distinct().ToArray();
        var catalogos = await db.SatClavesProdServCartaPorte.AsNoTracking()
            .Where(x => claves.Contains(x.Clave) && x.Vigente).ToDictionaryAsync(x => x.Clave, ct);
        foreach (var renglon in renglones)
        {
            if (!catalogos.TryGetValue(renglon.ClaveProdServ.Trim(), out var catalogo))
                return ErrorNegocio.Validacion("catalogo-mercancia-invalido", "Selecciona una clave Carta Porte vigente.");
            var resultado = ValidarPeligrosidad(catalogo.MaterialPeligroso, renglon.MaterialPeligroso, guardadas);
            if (resultado.EsFallo) return resultado.Error!;
            if (ReglasDeMaterialPeligroso.ValidarFicha(renglon) is { } ficha) return ficha;
        }
        if (ReglasDeMaterialPeligroso.ValidarSeguro(renglones.Any(x => x.MaterialPeligroso == "Sí"), aseguradora, poliza) is { } seguro)
            return seguro;
        var materiales = renglones.Where(x => x.MaterialPeligroso == "Sí").Select(x => x.CveMaterialPeligroso!.Trim()).Distinct().ToArray();
        var embalajes = renglones.Where(x => x.MaterialPeligroso == "Sí").Select(x => x.Embalaje!.Trim()).Distinct().ToArray();
        if (materiales.Length > 0 && await db.SatMaterialesPeligrosos.CountAsync(x => materiales.Contains(x.Clave) && x.Vigente, ct) != materiales.Length)
            return ErrorNegocio.Validacion("clave-material-peligroso-invalida", "Selecciona una clave vigente de material peligroso.");
        if (embalajes.Length > 0 && await db.SatTiposEmbalaje.CountAsync(x => embalajes.Contains(x.Clave) && x.Vigente, ct) != embalajes.Length)
            return ErrorNegocio.Validacion("embalaje-invalido", "Selecciona un tipo de embalaje vigente.");
        return catalogos.ToDictionary(x => x.Key, x => x.Value.MaterialPeligroso!);
    }
}
