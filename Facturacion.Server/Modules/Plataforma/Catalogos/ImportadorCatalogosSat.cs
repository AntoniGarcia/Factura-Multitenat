using System.Globalization;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>
/// Carga idempotente de los catálogos del SAT desde el archivo oficial <c>catCFDI</c>
/// (Anexo 20). Se dispara con <c>dotnet run -- --cargar-catalogos ruta.xls</c>, ver
/// <c>Program.cs</c>; no corre nunca dentro de una petición HTTP.
/// <para><b>Por qué no hay una fila fija de encabezado</b></para>
/// Cada hoja del archivo del SAT trae el encabezado en una fila distinta —algunas empiezan
/// con una fila en blanco, otras no— así que <see cref="FilaDeEncabezado"/> lo busca por
/// texto en vez de asumir una posición. Se comprobó inspeccionando el archivo real, hoja por
/// hoja, antes de escribir esto (ARQUITECTURA.md §9: no dar por hecho el formato).
/// <para><b>Por qué las claves numéricas se reconstruyen con ceros</b></para>
/// El archivo del SAT no escribe todas las claves como texto: la mayoría de
/// <c>c_ClaveProdServ</c> son celdas numéricas (Excel les quita los ceros a la izquierda que
/// no tienen, y no hace falta —esas claves son de ocho dígitos sin ceros iniciales salvo la
/// clave especial <c>01010101</c>, que el propio SAT ya escribe como texto—), mientras que
/// <c>c_ClaveUnidad</c> mezcla números (unidades heredadas) y texto (<c>H87</c>, <c>KGM</c>).
/// <see cref="Texto"/> normaliza los dos casos a la misma representación.
/// <para><b>Por qué nada se borra</b></para>
/// Una clave que ya no aparece en el archivo nuevo no se elimina: se marca
/// <c>Vigente = false</c> con la fecha de hoy, porque un comprobante viejo tiene que poder
/// seguir mostrando la descripción de una clave que el SAT retiró (ARQUITECTURA.md §7).
/// </summary>
public sealed class ImportadorCatalogosSat(AppDbContext db)
{
    /// <param name="soloEsteCatalogo">
    /// Nombre de un catálogo para recargar solo ese, o <c>null</c> para los veintiuno.
    /// </param>
    public async Task<IReadOnlyList<string>> ImportarAsync(
        string rutaArchivo, CancellationToken ct, string? soloEsteCatalogo = null)
    {
        using var flujo = File.OpenRead(rutaArchivo);
        using var libro = WorkbookFactory.Create(flujo);

        // Cada catálogo con su nombre, para poder recargar uno solo.
        //
        // Recargar los veintiún catálogos —unos 300.000 renglones— para corregir uno de
        // diecinueve no solo es lento: agota el buffer pool de SQL Server Express y la carga
        // falla entera. Y el SAT publica correcciones catálogo por catálogo, así que poder
        // recargar solo el que cambió es lo que se necesita en operación, no una comodidad.
        var catalogos = new (string Nombre, Func<Task<string>> Importar)[]
        {
            ("c_FormaPago", () => ImportarSimpleAsync<SatFormaPago>(libro, "c_FormaPago", db.SatFormasPago, ct, columnaInicio: 12, columnaFin: 13, digitosClave: 2)),
            ("c_Exportacion", () => ImportarSimpleAsync<SatExportacion>(libro, "c_Exportacion", db.SatExportaciones, ct)),
            ("c_MetodoPago", () => ImportarSimpleAsync<SatMetodoPago>(libro, "c_MetodoPago", db.SatMetodosPago, ct)),
            ("c_Periodicidad", () => ImportarSimpleAsync<SatPeriodicidad>(libro, "c_Periodicidad", db.SatPeriodicidades, ct)),
            ("c_Meses", () => ImportarSimpleAsync<SatMes>(libro, "c_Meses", db.SatMeses, ct)),
            ("c_TipoRelacion", () => ImportarSimpleAsync<SatTipoRelacion>(libro, "c_TipoRelacion", db.SatTiposRelacion, ct, digitosClave: 2)),
            ("c_Pais", () => ImportarSimpleAsync<SatPais>(libro, "c_Pais", db.SatPaises, ct)),
            ("c_ObjetoImp", () => ImportarSimpleAsync<SatObjetoImp>(libro, "c_ObjetoImp", db.SatObjetosImp, ct)),

            ("c_Moneda", () => ImportarMonedaAsync(libro, ct)),
            ("c_TipoDeComprobante", () => ImportarTipoDeComprobanteAsync(libro, ct)),
            ("c_RegimenFiscal", () => ImportarRegimenFiscalAsync(libro, ct)),
            ("c_UsoCFDI", () => ImportarUsoCfdiAsync(libro, ct)),
            ("c_ClaveProdServ", () => ImportarClaveProdServAsync(libro, ct)),
            ("c_ClaveUnidad", () => ImportarClaveUnidadAsync(libro, ct)),
            ("c_Impuesto", () => ImportarImpuestoAsync(libro, ct)),
            ("c_TipoFactor", () => ImportarTipoFactorAsync(libro, ct)),
            ("c_TasaOCuota", () => ImportarTasaOCuotaAsync(libro, ct)),

            ("c_Estado", () => ImportarEstadoAsync(libro, ct)),
            ("C_Municipio", () => ImportarMunicipioAsync(libro, ct)),
            ("C_Colonia", () => ImportarColoniaAsync(libro, ct)),
            ("c_CodigoPostal", () => ImportarCodigoPostalAsync(libro, ct))
        };

        var seleccionados = soloEsteCatalogo is null
            ? catalogos
            : [.. catalogos.Where(c => string.Equals(c.Nombre, soloEsteCatalogo, StringComparison.OrdinalIgnoreCase))];

        if (seleccionados.Length == 0)
            throw new InvalidOperationException(
                $"'{soloEsteCatalogo}' no es un catálogo conocido. Los válidos son: " +
                string.Join(", ", catalogos.Select(c => c.Nombre)));

        var resumen = new List<string>();

        foreach (var catalogo in seleccionados)
            resumen.Add(await catalogo.Importar());

        return resumen;
    }

    // ── Los ocho catálogos "clave + descripción + vigencia" ─────────────────────────────

    /// <summary>
    /// <paramref name="columnaInicio"/>/<paramref name="columnaFin"/> son configurables
    /// porque no todos están en la columna 2/3: <c>c_FormaPago</c> intercala once columnas
    /// de validación bancaria (ajenas a esta fase; ver §27–§29 del complemento de pagos)
    /// antes de llegar a la vigencia, y <c>c_Pais</c> sencillamente no trae vigencia por
    /// renglón —se comprobó inspeccionando las dos hojas completas, no solo las primeras
    /// columnas—, así que sus llamadas pasan índices fuera de rango a propósito:
    /// <see cref="Fecha"/> devuelve <c>null</c> ante una celda que no es fecha, así que el
    /// catálogo simplemente queda siempre vigente en vez de fallar.
    ///
    /// <para><b><paramref name="digitosClave"/>: los ceros a la izquierda no son cosméticos</b></para>
    /// Excel guarda «01» como el número 1, así que leer la celda tal cual produce «1». Para
    /// <c>c_FormaPago</c> y <c>c_TipoRelacion</c> eso es un error fiscal, no de presentación:
    /// el SAT exige <c>FormaPago="01"</c> y rechaza <c>FormaPago="1"</c>. Se descubrió en la
    /// fase 5, al ser lo primero que de verdad usó esas claves.
    /// </summary>
    private async Task<string> ImportarSimpleAsync<TEntidad>(
        IWorkbook libro, string hoja, DbSet<TEntidad> tabla, CancellationToken ct,
        int columnaInicio = 2, int columnaFin = 3, int digitosClave = 0)
        where TEntidad : class, ISatCatalogoSimple, new()
    {
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, TEntidad>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = digitosClave > 0 ? ClaveConCeros(fila, 0, digitosClave) : Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, columnaFin);
            nuevos[clave] = new TEntidad
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                FechaInicioVigencia = Fecha(fila, columnaInicio) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            tabla, nuevos, e => e.Clave,
            (actual, nuevo) =>
            {
                actual.Descripcion = nuevo.Descripcion;
                actual.FechaInicioVigencia = nuevo.FechaInicioVigencia;
                actual.FechaFinVigencia = nuevo.FechaFinVigencia;
                actual.Vigente = nuevo.Vigente;
            },
            e => { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── Moneda ────────────────────────────────────────────────────────────────────────

    private async Task<string> ImportarMonedaAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_Moneda";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatMoneda>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 5);
            nuevos[clave] = new SatMoneda
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                Decimales = (int)(Numero(fila, 2) ?? 2m),
                FechaInicioVigencia = Fecha(fila, 4) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatMonedas, nuevos, e => e.Clave,
            (a, n) => { a.Descripcion = n.Descripcion; a.Decimales = n.Decimales; a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── TipoDeComprobante ─────────────────────────────────────────────────────────────

    private async Task<string> ImportarTipoDeComprobanteAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_TipoDeComprobante";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatTipoDeComprobante>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 5);
            nuevos[clave] = new SatTipoDeComprobante
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                ValorMaximo = Numero(fila, 2),
                FechaInicioVigencia = Fecha(fila, 4) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatTiposDeComprobante, nuevos, e => e.Clave,
            (a, n) => { a.Descripcion = n.Descripcion; a.ValorMaximo = n.ValorMaximo; a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── RegimenFiscal ─────────────────────────────────────────────────────────────────

    private async Task<string> ImportarRegimenFiscalAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_RegimenFiscal";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        // A diferencia de c_UsoCFDI, aquí "Aplica para tipo persona" está en la fila DE
        // ARRIBA del encabezado real: el encabezado que trae "c_RegimenFiscal" ya incluye
        // Física/Moral en la misma fila, no hace falta saltar una extra.
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatRegimenFiscal>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 5);
            nuevos[clave] = new SatRegimenFiscal
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                AplicaFisica = SiNo(fila, 2),
                AplicaMoral = SiNo(fila, 3),
                FechaInicioVigencia = Fecha(fila, 4) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatRegimenesFiscales, nuevos, e => e.Clave,
            (a, n) => { a.Descripcion = n.Descripcion; a.AplicaFisica = n.AplicaFisica; a.AplicaMoral = n.AplicaMoral; a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── UsoCFDI — trae la matriz de compatibilidad lista ─────────────────────────────

    private async Task<string> ImportarUsoCfdiAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_UsoCFDI";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja) + 1;

        var nuevos = new Dictionary<string, SatUsoCfdi>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 5);
            nuevos[clave] = new SatUsoCfdi
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                AplicaFisica = SiNo(fila, 2),
                AplicaMoral = SiNo(fila, 3),
                RegimenesFiscalesAplicables = Texto(fila, 6) ?? "",
                FechaInicioVigencia = Fecha(fila, 4) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatUsosCfdi, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.Descripcion = n.Descripcion;
                a.AplicaFisica = n.AplicaFisica;
                a.AplicaMoral = n.AplicaMoral;
                a.RegimenesFiscalesAplicables = n.RegimenesFiscalesAplicables;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── ClaveProdServ (~52,000 claves) ───────────────────────────────────────────────

    private async Task<string> ImportarClaveProdServAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_ClaveProdServ";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatClaveProdServ>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = ClaveConCeros(fila, 0, 8);
            if (clave is null) continue;

            var fin = Fecha(fila, 6);
            nuevos[clave] = new SatClaveProdServ
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                IncluirIvaTrasladado = Texto(fila, 2),
                IncluirIepsTrasladado = Texto(fila, 3),
                ComplementoQueDebeIncluir = Texto(fila, 4),
                EstimuloFranjaFronteriza = Numero(fila, 7) == 1m,
                PalabrasSimilares = Texto(fila, 8),
                FechaInicioVigencia = Fecha(fila, 5) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatClavesProdServ, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.Descripcion = n.Descripcion;
                a.IncluirIvaTrasladado = n.IncluirIvaTrasladado;
                a.IncluirIepsTrasladado = n.IncluirIepsTrasladado;
                a.ComplementoQueDebeIncluir = n.ComplementoQueDebeIncluir;
                a.EstimuloFranjaFronteriza = n.EstimuloFranjaFronteriza;
                a.PalabrasSimilares = n.PalabrasSimilares;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── ClaveUnidad ───────────────────────────────────────────────────────────────────

    private async Task<string> ImportarClaveUnidadAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_ClaveUnidad";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatClaveUnidad>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 5);
            nuevos[clave] = new SatClaveUnidad
            {
                Clave = clave,
                Nombre = Texto(fila, 1) ?? clave,
                Descripcion = Texto(fila, 2),
                Nota = Texto(fila, 3),
                Simbolo = Texto(fila, 6),
                FechaInicioVigencia = Fecha(fila, 4) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatClavesUnidad, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.Nombre = n.Nombre;
                a.Descripcion = n.Descripcion;
                a.Nota = n.Nota;
                a.Simbolo = n.Simbolo;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── Impuesto ──────────────────────────────────────────────────────────────────────

    private async Task<string> ImportarImpuestoAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_Impuesto";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatImpuesto>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            // Tres dígitos: el SAT usa 001 (ISR), 002 (IVA) y 003 (IEPS), y así viajan en el
            // XML. Leerlos como "1", "2", "3" produce comprobantes que el PAC rechaza.
            var clave = ClaveConCeros(fila, 0, 3);
            if (clave is null) continue;

            var fin = Fecha(fila, 6);
            nuevos[clave] = new SatImpuesto
            {
                Clave = clave,
                Descripcion = Texto(fila, 1) ?? clave,
                Retencion = SiNo(fila, 2),
                Traslado = SiNo(fila, 3),
                LocalOFederal = Texto(fila, 4) ?? "",
                FechaInicioVigencia = Fecha(fila, 5) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatImpuestos, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.Descripcion = n.Descripcion;
                a.Retencion = n.Retencion;
                a.Traslado = n.Traslado;
                a.LocalOFederal = n.LocalOFederal;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── TipoFactor — sin columna de descripción propia ───────────────────────────────

    private async Task<string> ImportarTipoFactorAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_TipoFactor";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatTipoFactor>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 2);
            nuevos[clave] = new SatTipoFactor
            {
                Clave = clave,
                FechaInicioVigencia = Fecha(fila, 1) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatTiposFactor, nuevos, e => e.Clave,
            (a, n) => { a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── TasaOCuota — sin clave propia en el archivo, se construye una ───────────────

    private async Task<string> ImportarTasaOCuotaAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_TasaOCuota";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        // Dos filas de encabezado: "Rango o Fijo | c_TasaOCuota | ... " y, debajo,
        // "Valor mínimo | Valor máximo" para las dos columnas que comparten título.
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja) + 1;

        var nuevos = new Dictionary<string, SatTasaOCuota>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var rangoOFijo = Texto(fila, 0);
            var impuesto = Texto(fila, 3);
            var factor = Texto(fila, 4);
            var valorMaximo = Numero(fila, 2);
            if (rangoOFijo is null || impuesto is null || factor is null || valorMaximo is null) continue;

            var valorMinimo = Numero(fila, 1);
            var traslado = SiNo(fila, 5);
            var retencion = SiNo(fila, 6);

            // La clave lleva TODAS las columnas que distinguen un renglón de otro.
            //
            // Con solo impuesto+factor+valor máximo, el «IVA Tasa 0.16 de traslado» y el
            // «IVA Tasa rango 0–0.16 de retención» producían la misma clave y el segundo
            // sobrescribía al primero: se perdía el IVA 16 % trasladado, que es el impuesto
            // más común del país. Se descubrió en la fase 6, al ser lo primero que consultó
            // este catálogo.
            var clave = string.Join('|',
                impuesto, factor, rangoOFijo,
                valorMinimo?.ToString(CultureInfo.InvariantCulture) ?? "-",
                valorMaximo.Value.ToString(CultureInfo.InvariantCulture),
                traslado ? "T" : "-",
                retencion ? "R" : "-");

            var fin = Fecha(fila, 8);
            nuevos[clave] = new SatTasaOCuota
            {
                Clave = clave,
                RangoOFijo = rangoOFijo,
                ValorMinimo = valorMinimo,
                ValorMaximo = valorMaximo.Value,
                Impuesto = impuesto,
                Factor = factor,
                Traslado = traslado,
                Retencion = retencion,
                FechaInicioVigencia = Fecha(fila, 7) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatTasasOCuota, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.RangoOFijo = n.RangoOFijo;
                a.ValorMinimo = n.ValorMinimo;
                a.ValorMaximo = n.ValorMaximo;
                a.Traslado = n.Traslado;
                a.Retencion = n.Retencion;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── Estado ────────────────────────────────────────────────────────────────────────

    private async Task<string> ImportarEstadoAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "c_Estado";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<string, SatEstado>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            if (clave is null) continue;

            var fin = Fecha(fila, 4);
            nuevos[clave] = new SatEstado
            {
                Clave = clave,
                ClavePais = Texto(fila, 1) ?? "MEX",
                Nombre = Texto(fila, 2) ?? clave,
                FechaInicioVigencia = Fecha(fila, 3) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatEstados, nuevos, e => e.Clave,
            (a, n) => { a.ClavePais = n.ClavePais; a.Nombre = n.Nombre; a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── Municipio ─────────────────────────────────────────────────────────────────────

    private async Task<string> ImportarMunicipioAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hoja = "C_Municipio";
        var hojaExcel = HojaRequerida(libro, hoja);
        var version = MetadatosDeVersion(hojaExcel);
        var filaEncabezado = FilaDeEncabezado(hojaExcel, hoja);

        var nuevos = new Dictionary<(string, string), SatMunicipio>();
        for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
        {
            var fila = hojaExcel.GetRow(r);
            var clave = Texto(fila, 0);
            var claveEstado = Texto(fila, 1);
            if (clave is null || claveEstado is null) continue;

            var fin = Fecha(fila, 4);
            nuevos[(clave, claveEstado)] = new SatMunicipio
            {
                Clave = clave,
                ClaveEstado = claveEstado,
                Descripcion = Texto(fila, 2) ?? clave,
                FechaInicioVigencia = Fecha(fila, 3) ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        var vigentes = await SincronizarAsync(
            db.SatMunicipios, nuevos, e => (e.Clave, e.ClaveEstado),
            (a, n) => { a.Descripcion = n.Descripcion; a.FechaFinVigencia = n.FechaFinVigencia; a.Vigente = n.Vigente; },
            Retirar, ct);

        await ActualizarVersionAsync(hoja, version, vigentes, ct);
        return $"{hoja}: {vigentes} vigentes";
    }

    // ── Colonia — repartida en tres hojas ────────────────────────────────────────────

    private async Task<string> ImportarColoniaAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hojaBase = "C_Colonia";
        var partes = new[] { "C_Colonia_1", "C_Colonia_2", "C_Colonia_3" };
        var version = MetadatosDeVersion(HojaRequerida(libro, partes[0]));

        var nuevos = new Dictionary<(string, string), SatColonia>();
        foreach (var nombreParte in partes)
        {
            var hojaExcel = libro.GetSheet(nombreParte);
            if (hojaExcel is null) continue; // alguna versión del archivo puede traer menos partes

            var filaEncabezado = FilaDeEncabezado(hojaExcel, "c_Colonia");
            for (var r = filaEncabezado + 1; r <= hojaExcel.LastRowNum; r++)
            {
                var fila = hojaExcel.GetRow(r);
                var clave = ClaveConCeros(fila, 0, 4);
                var claveCp = Texto(fila, 1);
                if (clave is null || claveCp is null) continue;

                nuevos[(clave, claveCp)] = new SatColonia
                {
                    Clave = clave,
                    ClaveCodigoPostal = claveCp,
                    Nombre = Texto(fila, 2) ?? clave,
                    FechaInicioVigencia = DateOnly.MinValue,
                    FechaFinVigencia = null,
                    Vigente = true
                };
            }
        }

        var vigentes = await SincronizarAsync(
            db.SatColonias, nuevos, e => (e.Clave, e.ClaveCodigoPostal),
            (a, n) => { a.Nombre = n.Nombre; a.Vigente = true; a.FechaFinVigencia = null; },
            Retirar, ct);

        await ActualizarVersionAsync(hojaBase, version, vigentes, ct);
        return $"{hojaBase}: {vigentes} vigentes";
    }

    // ── CódigoPostal — repartido en dos hojas ────────────────────────────────────────

    private async Task<string> ImportarCodigoPostalAsync(IWorkbook libro, CancellationToken ct)
    {
        const string hojaBase = "c_CodigoPostal";
        var partes = new[] { "c_CodigoPostal_Parte_1", "c_CodigoPostal_Parte_2" };
        var version = MetadatosDeVersion(HojaRequerida(libro, partes[0]));

        var nuevos = new Dictionary<string, SatCodigoPostal>();
        foreach (var nombreParte in partes)
        {
            var hojaExcel = libro.GetSheet(nombreParte);
            if (hojaExcel is null) continue;

            var filaEncabezado = FilaDeEncabezado(hojaExcel, "c_CodigoPostal");
            for (var r = filaEncabezado + 2; r <= hojaExcel.LastRowNum; r++) // +2: hay una segunda fila de sub-encabezados de huso horario
            {
                var fila = hojaExcel.GetRow(r);
                var clave = ClaveConCeros(fila, 0, 5);
                var claveEstado = Texto(fila, 1);
                // La última fila de la primera parte es un aviso de continuación, no un dato.
                if (clave is null || claveEstado is null || clave.StartsWith("Contin", StringComparison.OrdinalIgnoreCase)) continue;

                nuevos[clave] = new SatCodigoPostal
                {
                    Clave = clave,
                    ClaveEstado = claveEstado,
                    ClaveMunicipio = Texto(fila, 2),
                    ClaveLocalidad = Texto(fila, 3),
                    EstimuloFranjaFronteriza = Numero(fila, 4) == 1m,
                    FechaInicioVigencia = Fecha(fila, 5) ?? DateOnly.MinValue,
                    FechaFinVigencia = Fecha(fila, 6),
                    Vigente = Fecha(fila, 6) is null
                };
            }
        }

        var vigentes = await SincronizarAsync(
            db.SatCodigosPostales, nuevos, e => e.Clave,
            (a, n) =>
            {
                a.ClaveEstado = n.ClaveEstado;
                a.ClaveMunicipio = n.ClaveMunicipio;
                a.ClaveLocalidad = n.ClaveLocalidad;
                a.EstimuloFranjaFronteriza = n.EstimuloFranjaFronteriza;
                a.FechaFinVigencia = n.FechaFinVigencia;
                a.Vigente = n.Vigente;
            },
            Retirar, ct);

        await ActualizarVersionAsync(hojaBase, version, vigentes, ct);
        return $"{hojaBase}: {vigentes} vigentes";
    }

    // ── Mecánica común de sincronización ─────────────────────────────────────────────

    /// <summary>
    /// Compara lo leído del archivo contra lo que ya hay en la base: da de alta lo nuevo,
    /// actualiza lo que cambió, y deja lo que ya no aparece exactamente como estaba salvo
    /// por <paramref name="retirar"/> (ARQUITECTURA.md §7: una clave retirada no se borra).
    /// </summary>
    private async Task<int> SincronizarAsync<TClave, TEntidad>(
        DbSet<TEntidad> tabla,
        Dictionary<TClave, TEntidad> nuevos,
        Func<TEntidad, TClave> clave,
        Action<TEntidad, TEntidad> actualizarDesde,
        Action<TEntidad> retirar,
        CancellationToken ct)
        where TEntidad : class
        where TClave : notnull
    {
        var existentes = await tabla.ToDictionaryAsync(clave, ct);

        foreach (var (k, nuevo) in nuevos)
        {
            if (existentes.TryGetValue(k, out var actual))
                actualizarDesde(actual, nuevo);
            else
                tabla.Add(nuevo);
        }

        foreach (var (k, actual) in existentes)
            if (!nuevos.ContainsKey(k))
                retirar(actual);

        await db.SaveChangesAsync(ct);
        return nuevos.Count;
    }

    private static void Retirar(SatMoneda e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatTipoDeComprobante e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatRegimenFiscal e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatUsoCfdi e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatClaveProdServ e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatClaveUnidad e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatImpuesto e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatTipoFactor e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatTasaOCuota e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatEstado e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatMunicipio e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }

    // Colonia y CodigoPostal no traen "vigente" propio bien poblado en el archivo del SAT
    // (colonia ni siquiera trae fecha), así que sí se retiran de verdad si desaparecen.
    private static void Retirar(SatColonia e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }
    private static void Retirar(SatCodigoPostal e) { e.Vigente = false; e.FechaFinVigencia ??= Hoy(); }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task ActualizarVersionAsync(string catalogo, (string Version, string Revision, DateOnly? Publicacion) meta, int vigentes, CancellationToken ct)
    {
        var registro = await db.CatalogoVersiones.FindAsync([catalogo], ct);
        if (registro is null)
        {
            registro = new Data.Entidades.Plataforma.Catalogos.CatalogoVersion
            {
                Catalogo = catalogo,
                VersionCatalogo = meta.Version,
                RevisionCatalogo = meta.Revision
            };
            db.CatalogoVersiones.Add(registro);
        }

        registro.VersionCatalogo = meta.Version;
        registro.RevisionCatalogo = meta.Revision;
        registro.FechaPublicacion = meta.Publicacion;
        registro.FechaCargaUtc = DateTime.UtcNow;
        registro.RenglonesVigentes = vigentes;

        await db.SaveChangesAsync(ct);
    }

    // ── Lectura de celdas ─────────────────────────────────────────────────────────────

    private static ISheet HojaRequerida(IWorkbook libro, string nombre)
        => libro.GetSheet(nombre) ?? throw new InvalidOperationException($"El archivo no tiene la hoja '{nombre}'.");

    private static int FilaDeEncabezado(ISheet hoja, string marca)
    {
        var limite = Math.Min(hoja.LastRowNum, 10);
        for (var r = 0; r <= limite; r++)
        {
            var fila = hoja.GetRow(r);
            if (fila is null) continue;

            for (var c = 0; c < fila.LastCellNum; c++)
                if (string.Equals(Texto(fila, c), marca, StringComparison.OrdinalIgnoreCase))
                    return r;
        }

        throw new InvalidOperationException($"No se encontró el encabezado '{marca}' en las primeras filas de '{hoja.SheetName}'.");
    }

    private static (string Version, string Revision, DateOnly? Publicacion) MetadatosDeVersion(ISheet hoja)
    {
        var filaEncabezado = FilaDeEncabezado(hoja, "Versión CFDI");
        var valores = hoja.GetRow(filaEncabezado + 1);
        return (Texto(valores, 1) ?? "?", Texto(valores, 2) ?? "?", Fecha(valores, 3));
    }

    /// <summary>
    /// Tipo real de la celda. Una celda de fórmula no guarda un tipo propio sino el del
    /// resultado que Excel dejó cacheado, y hay que preguntarlo aparte.
    ///
    /// <para><b>Por qué esto existe</b></para>
    /// El archivo del SAT tiene celdas con fórmulas donde uno esperaría números: la tasa de
    /// IEPS del 25 % está escrita como <c>=25/100</c>. Comparar <c>CellType</c> contra
    /// <c>Numeric</c> a secas descartaba ese renglón <b>en silencio</b>, y el catálogo
    /// quedaba incompleto sin que nada fallara. Se descubrió en la fase 6, contando los
    /// renglones importados contra los de la hoja.
    /// </para>
    /// </summary>
    private static CellType TipoReal(ICell celda)
        => celda.CellType == CellType.Formula ? celda.CachedFormulaResultType : celda.CellType;

    private static string? Texto(IRow? fila, int columna)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null) return null;

        var tipo = TipoReal(celda);
        if (tipo == CellType.Blank) return null;

        var valor = tipo == CellType.Numeric
            ? celda.NumericCellValue.ToString(CultureInfo.InvariantCulture)
            : tipo == CellType.String
                ? celda.StringCellValue
                : celda.ToString();

        valor = valor?.Trim();
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    /// <summary>Para claves que el SAT a veces escribe como número: reconstruye los ceros a la izquierda.</summary>
    private static string? ClaveConCeros(IRow? fila, int columna, int digitosMinimos)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null) return null;

        var tipo = TipoReal(celda);
        if (tipo == CellType.Blank) return null;

        if (tipo == CellType.Numeric)
            return ((long)celda.NumericCellValue).ToString(CultureInfo.InvariantCulture).PadLeft(digitosMinimos, '0');

        var texto = (tipo == CellType.String ? celda.StringCellValue : celda.ToString())?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    private static decimal? Numero(IRow? fila, int columna)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null || TipoReal(celda) != CellType.Numeric) return null;
        return (decimal)celda.NumericCellValue;
    }

    private static DateOnly? Fecha(IRow? fila, int columna)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null || TipoReal(celda) != CellType.Numeric || !DateUtil.IsCellDateFormatted(celda)) return null;

        var valor = celda.DateCellValue;
        return valor is null ? null : DateOnly.FromDateTime(valor.Value);
    }

    private static bool SiNo(IRow? fila, int columna)
        => Texto(fila, columna) is { } t
           && (t.Equals("Sí", StringComparison.OrdinalIgnoreCase) || t.Equals("Si", StringComparison.OrdinalIgnoreCase));
}
