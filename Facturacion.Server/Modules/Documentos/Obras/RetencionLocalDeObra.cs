using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// <summary>
/// Retención que va al complemento de impuestos locales: la primera deducción de una obra
/// pública, con el nombre y la tasa capturados (0.50 en obra federal; 0.20 o 0.30 en algunas
/// estatales y municipales). Las demás deducciones solo restan del importe líquido.
/// </summary>
/// <param name="Tasa">Porcentaje, como lo pide <c>TasadeRetencion</c>: 0.50 es el 5 al millar.</param>
public sealed record RetencionLocalDeObra(string Nombre, decimal Tasa, decimal Importe)
{
    public static RetencionLocalDeObra? Calcular(Comprobante comprobante, DatosObra? obra)
        => obra is { TipoObra: TiposDeObra.Publica } && obra.PorcentajeDeduccion1 != 0
            ? new(obra.NombreDeduccion1, obra.PorcentajeDeduccion1,
                Math.Round(comprobante.SubTotal * obra.PorcentajeDeduccion1 / 100m, 2, MidpointRounding.ToEven))
            : null;
}
