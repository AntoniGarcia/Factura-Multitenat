using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// <summary>
/// El anticipo que amortiza una estimación y lo que le queda por amortizar. Se revisa al guardar
/// el cálculo de obra y otra vez al timbrar, porque entre las dos otra estimación pudo tomar saldo.
/// </summary>
public static class AnticipoDeObra
{
    /// <summary>Clave de <c>c_ClaveProdServ</c> que el Apéndice 6 fija para el CFDI de anticipo.</summary>
    private const string ClaveDeAnticipo = "84111506";

    public static async Task<ErrorNegocio?> ValidarAsync(
        AppDbContext baseDeDatos, Comprobante comprobante, decimal porcentajeAmortizacion, CancellationToken ct)
    {
        if (porcentajeAmortizacion == 0) return null;

        var relacionados = comprobante.Relacionados
            .Where(x => x.TipoRelacion == AmortizacionDeObra.TipoRelacionAnticipo)
            .Select(x => x.UuidRelacionado)
            .ToArray();
        if (relacionados.Length != 1)
            return ErrorNegocio.Regla("obra-amortizacion-sin-anticipo",
                "Para amortizar, relaciona en «CFDI relacionados» un solo anticipo con tipo 07 y guarda la factura.");

        var uuid = relacionados[0];
        var anticipo = await baseDeDatos.Comprobantes.AsNoTracking()
            .Where(x => x.Uuid == uuid)
            .Select(x => new
            {
                x.Estatus,
                x.TipoDeComprobante,
                x.ReceptorRfc,
                x.SubTotal,
                EsAnticipo = x.Conceptos.Any(c => c.ClaveProdServ == ClaveDeAnticipo)
            })
            .FirstOrDefaultAsync(ct);
        if (anticipo is null || anticipo.TipoDeComprobante != "I" || !anticipo.EsAnticipo)
            return ErrorNegocio.Regla("obra-anticipo-invalido",
                $"El CFDI relacionado con tipo 07 no es un anticipo: debe ser una factura de esta empresa con la clave {ClaveDeAnticipo}.");
        if (anticipo.Estatus != "timbrado")
            return ErrorNegocio.Regla("obra-anticipo-no-vigente",
                "El anticipo relacionado está cancelado o en cancelación; ya no se puede amortizar.");
        if (anticipo.ReceptorRfc != comprobante.ReceptorRfc)
            return ErrorNegocio.Regla("obra-anticipo-de-otro-cliente",
                "El anticipo relacionado se emitió a otro cliente.");

        // Solo cuenta lo que ya salió o está saliendo al SAT; un borrador no aparta saldo.
        var amortizado = await baseDeDatos.Comprobantes.AsNoTracking()
            .Where(x => x.Id != comprobante.Id &&
                (x.Estatus == "timbrando" || x.Estatus == "timbrado" || x.Estatus == "en_cancelacion") &&
                x.Relacionados.Any(r => r.TipoRelacion == AmortizacionDeObra.TipoRelacionAnticipo && r.UuidRelacionado == uuid))
            .SumAsync(x => x.Descuento, ct);
        var saldo = anticipo.SubTotal - amortizado;
        if (comprobante.Descuento > saldo)
            return ErrorNegocio.Regla("obra-amortizacion-excede-anticipo",
                $"La amortización ({comprobante.Descuento:N2}) supera el saldo del anticipo ({saldo:N2}). Baja el porcentaje.");

        return null;
    }
}
