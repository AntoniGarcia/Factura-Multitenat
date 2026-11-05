namespace Facturacion.Shared.Comun;

public static class ReglasDeObjetoImpuesto
{
    public static string? ValidarCombinacion(IEnumerable<string> objetos)
    {
        var claves = objetos.Distinct().ToArray();
        return claves.Contains("05") && claves.Length > 1
            ? "Los conceptos con objeto 05 deben facturarse por separado de los demás objetos de impuesto."
            : null;
    }

    public static bool PermiteDesglose(string? objeto) => objeto is "02" or "06" or "07" or "08";

    public static bool PermiteImpuesto(string? objeto, string impuesto, bool esRetencion)
        => objeto switch
        {
            "02" => true,
            "06" or "08" => impuesto == "001" && esRetencion,
            "07" => impuesto == "003" || impuesto == "001" && esRetencion,
            _ => false
        };

    // Anexo 29 de la RMF 2026, VI.1.8 y VI.2.1.1: aplica a conceptos y a ImpuestosDR.
    public static string? Validar(string? objeto, IEnumerable<(string Impuesto, bool EsRetencion)> impuestos)
    {
        if (objeto is not ("01" or "02" or "03" or "04" or "05" or "06" or "07" or "08"))
            return "El objeto de impuesto no está admitido.";

        var desglose = impuestos.ToArray();
        if (!PermiteDesglose(objeto) && desglose.Length > 0)
            return $"El objeto de impuesto {objeto} no permite desglose de impuestos.";
        if (objeto == "02" && desglose.Length == 0)
            return "El objeto de impuesto 02 necesita al menos un impuesto.";
        if (desglose.Any(i => !PermiteImpuesto(objeto, i.Impuesto, i.EsRetencion)))
            return objeto == "07"
                ? "El objeto 07 admite IEPS y retención de ISR; no admite IVA."
                : $"El objeto {objeto} solo admite retención de ISR.";
        if (objeto == "07" && !desglose.Any(i => i.Impuesto == "003" && !i.EsRetencion))
            return "El objeto 07 necesita IEPS trasladado.";
        return null;
    }
}
