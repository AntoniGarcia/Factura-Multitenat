using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Productos;

/// <summary>
/// Valida un producto contra los catálogos del SAT. Todo en el servidor: una clave que no
/// existe o una tasa que el SAT no reconoce producen un comprobante rechazado, y para
/// entonces ya se apartó un folio (CLAUDE.md §5).
/// </summary>
public sealed class ValidadorDeProducto(AppDbContext baseDeDatos)
{
    /// <summary>
    /// Claves de <c>c_ObjetoImp</c> que <b>no</b> llevan desglose de impuestos en el
    /// comprobante: «no objeto» y «sí objeto pero no obligado al desglose».
    /// </summary>
    private static readonly string[] SinDesglose = ["01", "03"];

    public async Task<ErrorNegocio?> ValidarAsync(PeticionGuardarProducto peticion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion.Descripcion))
            return ErrorNegocio.Validacion("descripcion-vacia", "Escribe la descripción del producto.");

        if (string.IsNullOrWhiteSpace(peticion.UnidadTexto))
            return ErrorNegocio.Validacion("unidad-vacia",
                "Escribe la unidad de medida que verá el cliente en el PDF.");

        if (peticion.ValorUnitario < 0)
            return ErrorNegocio.Validacion("precio-negativo", "El precio de venta no puede ser negativo.");

        if (peticion.PesoKg is < 0)
            return ErrorNegocio.Validacion("peso-negativo", "El peso no puede ser negativo.");

        var existeClave = await baseDeDatos.SatClavesProdServ
            .AsNoTracking().AnyAsync(c => c.Clave == peticion.ClaveProdServ, ct);

        if (!existeClave)
            return ErrorNegocio.Validacion("clave-prodserv-desconocida",
                $"La clave de producto o servicio {peticion.ClaveProdServ} no está en el catálogo del SAT. " +
                "Elígela con el buscador, no la escribas a mano.");

        var existeUnidad = await baseDeDatos.SatClavesUnidad
            .AsNoTracking().AnyAsync(u => u.Clave == peticion.ClaveUnidad, ct);

        if (!existeUnidad)
            return ErrorNegocio.Validacion("clave-unidad-desconocida",
                $"La clave de unidad {peticion.ClaveUnidad} no está en el catálogo del SAT.");

        var objeto = await baseDeDatos.SatObjetosImp
            .AsNoTracking().FirstOrDefaultAsync(o => o.Clave == peticion.ObjetoImp, ct);

        if (objeto is null)
            return ErrorNegocio.Validacion("objeto-imp-desconocido",
                $"El objeto de impuesto {peticion.ObjetoImp} no está en el catálogo del SAT.");

        return await ValidarImpuestosAsync(peticion, objeto.Descripcion, ct);
    }

    private async Task<ErrorNegocio?> ValidarImpuestosAsync(
        PeticionGuardarProducto peticion, string descripcionObjeto, CancellationToken ct)
    {
        var impuestos = peticion.Impuestos;
        var llevaDesglose = !SinDesglose.Contains(peticion.ObjetoImp);

        // Coherencia entre el objeto de impuesto y los impuestos configurados. Un concepto
        // marcado «no objeto» que además trae desglose es una contradicción que el SAT
        // rechaza, y es fácil de producir cambiando el objeto y olvidando los impuestos.
        if (!llevaDesglose && impuestos.Count > 0)
            return ErrorNegocio.Validacion("impuestos-sobran",
                $"Marcaste «{descripcionObjeto}», así que el concepto no lleva desglose de impuestos. " +
                "Quita los impuestos configurados o cambia el objeto de impuesto.");

        if (llevaDesglose && impuestos.Count == 0)
            return ErrorNegocio.Validacion("impuestos-faltan",
                $"Marcaste «{descripcionObjeto}», así que hay que configurar al menos un impuesto.");

        var repetido = impuestos
            .GroupBy(i => (i.Impuesto, i.EsRetencion))
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
            return ErrorNegocio.Validacion("impuesto-repetido",
                $"El impuesto {repetido.Key.Impuesto} está configurado dos veces como " +
                $"{(repetido.Key.EsRetencion ? "retención" : "traslado")}.");

        foreach (var impuesto in impuestos)
        {
            var error = await ValidarUnImpuestoAsync(impuesto, ct);
            if (error is not null) return error;
        }

        return null;
    }

    private async Task<ErrorNegocio?> ValidarUnImpuestoAsync(
        ImpuestoDeProductoDto impuesto, CancellationToken ct)
    {
        var delCatalogo = await baseDeDatos.SatImpuestos
            .AsNoTracking().FirstOrDefaultAsync(i => i.Clave == impuesto.Impuesto, ct);

        if (delCatalogo is null)
            return ErrorNegocio.Validacion("impuesto-desconocido",
                $"El impuesto {impuesto.Impuesto} no está en el catálogo del SAT.");

        if (impuesto.EsRetencion && !delCatalogo.Retencion)
            return ErrorNegocio.Validacion("impuesto-no-se-retiene",
                $"El {delCatalogo.Descripcion} no se puede retener.");

        if (!impuesto.EsRetencion && !delCatalogo.Traslado)
            return ErrorNegocio.Validacion("impuesto-no-se-traslada",
                $"El {delCatalogo.Descripcion} no se puede trasladar.");

        var existeFactor = await baseDeDatos.SatTiposFactor
            .AsNoTracking().AnyAsync(f => f.Clave == impuesto.TipoFactor, ct);

        if (!existeFactor)
            return ErrorNegocio.Validacion("factor-desconocido",
                $"El tipo de factor {impuesto.TipoFactor} no está en el catálogo del SAT.");

        // Exento no lleva valor, y Tasa o Cuota lo exigen. Confundirlo produce un XML con un
        // atributo de más o de menos, que el PAC rechaza por esquema.
        if (impuesto.TipoFactor == "Exento")
            return impuesto.TasaOCuota is null
                ? null
                : ErrorNegocio.Validacion("exento-con-tasa",
                    "Un impuesto exento no lleva tasa ni cuota.");

        if (impuesto.TasaOCuota is null)
            return ErrorNegocio.Validacion("falta-tasa",
                $"Falta la tasa o cuota del {delCatalogo.Descripcion}.");

        return await ValidarTasaContraCatalogoAsync(impuesto, delCatalogo.Descripcion, ct);
    }

    /// <summary>
    /// Comprueba que la tasa exista en <c>c_TasaOCuota</c>.
    ///
    /// <para><b>Por qué no basta con «entre 0 y 1»</b></para>
    /// El SAT no acepta cualquier tasa: el IVA trasladado solo puede ser 0 % o 16 %, y un
    /// producto configurado al 15 % produce comprobantes rechazados uno tras otro. El
    /// catálogo trae renglones fijos y renglones de rango —la retención de IVA es cualquier
    /// valor entre 0 y 16 %—, y aquí se respetan los dos.
    /// </para>
    /// <para>
    /// La columna <c>Impuesto</c> de ese catálogo trae el nombre («IVA», «IEPS») y no la
    /// clave numérica, que es como lo publica el SAT; por eso la comparación es contra la
    /// descripción de <c>c_Impuesto</c> y no contra la clave.
    /// </para>
    /// <para>
    /// <b>Y por eso también se aceptan las variantes con prefijo.</b> El SAT publica el
    /// estímulo de la franja fronteriza como un renglón aparte llamado «IVA Crédito aplicado
    /// del 50%» con tasa 0.08, pero en el XML ese concepto viaja igual como IVA (002) al
    /// 8 %. Comparando solo por igualdad, un contribuyente de la frontera no podría
    /// configurar sus productos. Se exige el nombre completo seguido de espacio para que
    /// «IVA» no llegue a emparejarse con un impuesto que solo empiece parecido.
    /// </para>
    /// </summary>
    private async Task<ErrorNegocio?> ValidarTasaContraCatalogoAsync(
        ImpuestoDeProductoDto impuesto, string nombreImpuesto, CancellationToken ct)
    {
        var valor = impuesto.TasaOCuota!.Value;
        var variante = nombreImpuesto + " ";

        var candidatas = await baseDeDatos.SatTasasOCuota
            .AsNoTracking()
            .Where(t => t.Vigente
                        && (t.Impuesto == nombreImpuesto || t.Impuesto.StartsWith(variante))
                        && t.Factor == impuesto.TipoFactor
                        && (impuesto.EsRetencion ? t.Retencion : t.Traslado))
            .ToListAsync(ct);

        var admitida = candidatas.Any(t => t.ValorMinimo is null
            ? t.ValorMaximo == valor
            : valor >= t.ValorMinimo.Value && valor <= t.ValorMaximo);

        if (admitida) return null;

        var permitidas = candidatas
            .Select(t => t.ValorMinimo is null
                ? $"{t.ValorMaximo:0.######}"
                : $"de {t.ValorMinimo.Value:0.######} a {t.ValorMaximo:0.######}")
            .ToList();

        var sentido = impuesto.EsRetencion ? "retenido" : "trasladado";

        return ErrorNegocio.Validacion("tasa-no-admitida",
            permitidas.Count == 0
                ? $"El SAT no admite {nombreImpuesto} {sentido} con factor {impuesto.TipoFactor}."
                : $"El SAT no admite {valor:0.######} como tasa de {nombreImpuesto} {sentido}. " +
                  $"Los valores permitidos son: {string.Join(", ", permitidas)}.");
    }
}
