using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// Retención del complemento de impuestos locales: la primera deducción de la estimación
/// (por ejemplo 5 al millar: 0.50 federal; 0.20 o 0.30 en algunos estados y municipios).
/// Las demás deducciones solo restan del importe líquido.
public static class RetencionLocalDeObra
{
    // subTotal: ya en los decimales de la moneda.
    public static ImpuestoLocalCfdi? Calcular(DatosObra? obra, decimal subTotal)
        => obra is { PorcentajeDeduccion1: > 0 }
            ? new(obra.NombreDeduccion1, obra.PorcentajeDeduccion1,
                Math.Round(subTotal * obra.PorcentajeDeduccion1 / 100m,
                    ProyeccionMonetariaCfdi.DecimalesLocales, MidpointRounding.ToEven),
                EsRetencion: true)
            : null;
}
