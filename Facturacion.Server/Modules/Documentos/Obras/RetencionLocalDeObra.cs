using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// <summary>
/// Retención que va al complemento de impuestos locales: la primera deducción de una obra
/// pública, con el nombre y la tasa capturados (0.50 en obra federal; 0.20 o 0.30 en algunas
/// estatales y municipales). Las demás deducciones solo restan del importe líquido.
/// </summary>
public static class RetencionLocalDeObra
{
    /// <param name="subTotal">Importe de los trabajos, ya en los decimales de la moneda.</param>
    public static ImpuestoLocalCfdi? Calcular(DatosObra? obra, decimal subTotal)
        => obra is { TipoObra: TiposDeObra.Publica } && obra.PorcentajeDeduccion1 != 0
            ? new(obra.NombreDeduccion1, obra.PorcentajeDeduccion1,
                Math.Round(subTotal * obra.PorcentajeDeduccion1 / 100m,
                    ProyeccionMonetariaCfdi.DecimalesLocales, MidpointRounding.ToEven),
                EsRetencion: true)
            : null;
}
