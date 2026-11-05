using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Emision;

public static class DatosFiscalesDeConcepto
{
    public static DatosFiscalesDeConceptoDto? Leer(string? json)
        => json is null ? null : JsonSerializer.Deserialize<DatosFiscalesDeConceptoDto>(json);

    public static async Task<Resultado<string?>> PrepararAsync(DatosFiscalesDeConceptoDto? datos,
        IServicioCatalogosSat catalogos, CancellationToken ct)
    {
        if (datos is null) return Resultado<string?>.Exito(null);
        if (datos.Pedimentos is null || datos.CuentasPrediales is null || datos.Pedimentos.Count > 50 || datos.CuentasPrediales.Count > 50)
            return Error("Admite hasta 50 pedimentos y 50 cuentas prediales por concepto.");
        var pedimentos = datos.Pedimentos.Select(x => x?.Trim() ?? "").ToArray();
        var cuentas = datos.CuentasPrediales.Select(x => x?.Trim() ?? "").ToArray();
        if (pedimentos.Distinct().Count() != pedimentos.Length || cuentas.Distinct().Count() != cuentas.Length ||
            pedimentos.Any(x => !Regex.IsMatch(x, @"\A[0-9]{2}  [0-9]{2}  [0-9]{4}  [0-9]{7}\z")) ||
            cuentas.Any(x => !Regex.IsMatch(x, @"\A[0-9a-zA-Z]{1,150}\z")))
            return Error("Revisa pedimentos con formato AA  AD  PPPP  NNNNNNN y cuentas prediales alfanuméricas, sin repetirlos.");
        TerceroDeConceptoDto? tercero = null;
        if (datos.Tercero is { } t)
        {
            var rfc = (t.Rfc ?? "").Trim().ToUpperInvariant();
            var nombre = NombreFiscal.Normalizar(t.Nombre ?? "").Normalizado;
            var cp = t.CodigoPostal?.Trim() ?? "";
            if (!Rfc.Validar(rfc).EsValido || Rfc.EsGenerico(rfc) || nombre.Length is 0 or > 300 || nombre.Contains('|') ||
                !Regex.IsMatch(cp, @"\A[0-9]{5}\z")) return Error("Completa RFC, nombre fiscal, régimen y código postal del tercero.");
            try { XmlConvert.VerifyXmlChars(nombre); }
            catch (XmlException) { return Error("El nombre del tercero contiene caracteres no admitidos."); }
            if (await catalogos.ResolverAsync("c_RegimenFiscal", t.RegimenFiscal, ct) is not { Vigente: true } ||
                await catalogos.ResolverAsync("c_CodigoPostal", cp, ct) is not { Vigente: true } ||
                !await catalogos.EsUsoCfdiCompatibleAsync("S01", t.RegimenFiscal, Rfc.EsPersonaMoral(rfc), ct))
                return Error("El régimen o código postal del tercero no es vigente o compatible con su RFC.");
            tercero = new(rfc, nombre, t.RegimenFiscal, cp);
        }
        if (pedimentos.Length == 0 && cuentas.Length == 0 && tercero is null) return Resultado<string?>.Exito(null);
        var json = JsonSerializer.Serialize(new DatosFiscalesDeConceptoDto(pedimentos, cuentas, tercero));
        return json.Length > 16000 ? Error("Los datos fiscales del concepto exceden el tamaño permitido.") : Resultado<string?>.Exito(json);
    }
    private static ErrorNegocio Error(string mensaje) => ErrorNegocio.Validacion("datos-fiscales-concepto-invalidos", mensaje);
}
