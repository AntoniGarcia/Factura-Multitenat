using System.Globalization;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// La expresión impresa del CFDI: el texto que va dentro del código QR de la representación
/// impresa, y que lleva a la página del SAT donde se verifica el comprobante.
///
/// <para><b>Formato</b></para>
/// <c>https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id={UUID}&amp;re={RFC
/// emisor}&amp;rr={RFC receptor}&amp;tt={Total}&amp;fe={últimos 8 del sello}</c>
///
/// <para><b>Dos detalles que se equivocan seguido</b></para>
/// <list type="bullet">
///   <item><description>
///     <c>tt</c> va con el total <b>tal como aparece en el XML</b>, sin rellenar de ceros.
///     En CFDI 3.3 se rellenaba a 18 enteros y 6 decimales; en 4.0 no. Arrastrar el formato
///     viejo hace que el SAT no encuentre el comprobante.
///   </description></item>
///   <item><description>
///     <c>fe</c> <b>no se codifica para URL</b>. Son los últimos ocho caracteres del sello en
///     base 64, y suelen traer <c>/</c> o <c>=</c>; escaparlos rompe la verificación.
///   </description></item>
/// </list>
///
/// <para><b>Qué tan verificado está</b></para>
/// Contrastado contra las implementaciones de referencia de la comunidad mexicana
/// —<c>phpcfdi/cfdi-expresiones</c>, <c>nodecfdi</c> y <c>CfdiUtils</c>—, no contra el PDF del
/// Anexo 20 directamente. Antes de producción conviene comprobar un comprobante real en la
/// página del SAT: es una verificación de treinta segundos que cierra la duda del todo.
/// </summary>
public static class ExpresionImpresa
{
    private const string Portal = "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx";

    /// <summary>Caracteres del sello que van en el parámetro <c>fe</c>.</summary>
    private const int CaracteresDeSello = 8;

    /// <summary>
    /// Arma la expresión. Devuelve <c>null</c> si al comprobante le falta algo de lo que
    /// necesita: sin UUID o sin sello no hay nada que verificar, y un QR a medias que lleva
    /// a una página de error es peor que no ponerlo.
    /// </summary>
    /// <param name="total">Ya formateado con los decimales de la moneda, como va en el XML.</param>
    public static string? Construir(Guid? uuid, string? rfcEmisor, string? rfcReceptor, string total, string? selloCfd)
    {
        if (uuid is not { } folioFiscal || string.IsNullOrWhiteSpace(selloCfd)) return null;
        if (string.IsNullOrWhiteSpace(rfcEmisor) || string.IsNullOrWhiteSpace(rfcReceptor)) return null;

        var fe = selloCfd.Length <= CaracteresDeSello
            ? selloCfd
            : selloCfd[^CaracteresDeSello..];

        // Sin Uri.EscapeDataString en 'fe' a propósito; ver el resumen de la clase.
        return $"{Portal}?id={folioFiscal.ToString().ToUpperInvariant()}" +
               $"&re={rfcEmisor}" +
               $"&rr={rfcReceptor}" +
               $"&tt={total}" +
               $"&fe={fe}";
    }

    /// <summary>Formatea el total como debe ir en la expresión: igual que en el XML.</summary>
    public static string Total(decimal total, int decimales)
        => total.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
