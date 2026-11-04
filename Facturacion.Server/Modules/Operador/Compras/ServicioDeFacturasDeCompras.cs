using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Operador;
using Facturacion.Shared.Plataforma;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Compras;

public sealed class ServicioDeFacturasDeCompras(
    AppDbContext db, IServicioCatalogosSat catalogos,
    IContextoDeOperador operador, IHttpContextAccessor accesor)
{
    public async Task<Resultado<FacturaDeCompraDto>> ObtenerAsync(Guid compraId, CancellationToken ct)
    {
        var compra = await CompraAsync(compraId, ct);
        if (compra is null) return NoEncontrada();
        if (ValidarCompra(compra) is { } error) return error;
        // El proveedor consulta facturas de todas sus cuentas; la ruta exige permiso de operador.
        var factura = await db.FacturasDeCompras.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompraId == compraId, ct);
        if (factura is not null) return ADto(factura);

        var fiscal = await db.ConfiguracionFiscalProveedor.AsNoTracking().SingleOrDefaultAsync(ct);
        if (fiscal is null) return ErrorNegocio.Conflicto("proveedor-sin-datos-fiscales", "Configura primero los datos fiscales del proveedor.");
        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == compra.EmpresaId && x.CuentaId == compra.CuentaId, ct);
        if (empresa is null || empresa.Rfc != compra.EmpresaRfcAlComprar)
            return ErrorNegocio.Conflicto("receptor-compra-inconsistente", "Revisa la empresa que realizó la compra.");
        var producto = compra.ClaveProdServAlComprar;
        var unidad = compra.ClaveUnidadAlComprar;
        if (producto is null && unidad is null)
        {
            // Las compras anteriores a esta captura toman una propuesta del paquete antes del primer guardado.
            var paquete = await db.Paquetes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == compra.PaqueteId &&
                (x.CuentaId == null || x.CuentaId == compra.CuentaId), ct);
            producto = paquete?.ClaveProdServ; unidad = paquete?.ClaveUnidad;
        }
        return new FacturaDeCompraDto(null, compra.Id, "borrador", Emisor(fiscal),
            new(compra.EmpresaRfcAlComprar, empresa.NombreFiscal, empresa.RegimenFiscal, "", ""),
            new($"{compra.NombrePaquete} - {compra.CantidadTimbres} timbres", compra.CantidadTimbres, ClavesSatPredeterminadasDePaquetes.Producto(producto), ClavesSatPredeterminadasDePaquetes.Unidad(unidad),
                compra.Subtotal, compra.Iva, compra.TasaIva, compra.PrecioTotal), "", null, fiscal.Version);
    }

    public async Task<Resultado<FacturaDeCompraDto>> GuardarAsync(
        Guid operadorId, Guid compraId, PeticionGuardarFacturaCompra p, CancellationToken ct)
    {
        if (!await db.OperadoresPlataforma.AnyAsync(x => x.Id == operadorId && x.Activo, ct))
            return ErrorNegocio.SinPermiso("operador-no-activo", "Tu acceso de operador no está disponible.");
        var compraInicial = await CompraAsync(compraId, ct);
        if (compraInicial is null) return NoEncontrada();

        // La cuenta se resuelve desde la compra en SQL. Se conservan el filtro y el sellado de escritura.
        var cuenta = new ContextoEmpresaFijo(cuenta: compraInicial.CuentaId);
        await using var datos = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(db.Database.GetConnectionString(), cuenta).Options, cuenta);
        var compra = await datos.ComprasTimbres.AsNoTracking().SingleOrDefaultAsync(x => x.Id == compraId, ct);
        if (compra is null) return NoEncontrada();
        if (ValidarCompra(compra) is { } errorCompra) return errorCompra;
        var factura = await datos.FacturasDeCompras.SingleOrDefaultAsync(x => x.CompraId == compraId, ct);
        if (factura is null ? p.Version is not null :
            p.Version is not { Length: 8 } || !factura.Version.AsSpan().SequenceEqual(p.Version))
            return Desactualizada();
        if (factura is not null && factura.Estado is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("factura-no-editable", "Solo se pueden modificar borradores o facturas con un rechazo confirmado.");
        if (await ValidarAsync(compra.EmpresaRfcAlComprar, p, ct) is { } error) return error;

        var antes = factura is null ? null : ADto(factura);
        if (factura is null)
        {
            var fiscal = await datos.ConfiguracionFiscalProveedor.AsNoTracking().SingleOrDefaultAsync(ct);
            if (fiscal is null) return ErrorNegocio.Conflicto("proveedor-sin-datos-fiscales", "Configura primero los datos fiscales del proveedor.");
            if (p.VersionProveedor is not { Length: 8 } || !fiscal.Version.AsSpan().SequenceEqual(p.VersionProveedor))
                return ErrorNegocio.Conflicto("emisor-modificado", "Los datos del proveedor cambiaron. Abre de nuevo la factura.");
            factura = new FacturaDeCompra
            {
                Id = Guid.NewGuid(), CompraId = compra.Id, CuentaId = compra.CuentaId, EmpresaId = compra.EmpresaId,
                EmisorRfc = fiscal.Rfc, EmisorNombre = fiscal.NombreFiscal, EmisorRegimen = fiscal.RegimenFiscal,
                EmisorCodigoPostal = fiscal.CodigoPostalExpedicion, EmisorZonaHoraria = fiscal.ZonaHoraria,
                Serie = fiscal.Serie, VersionProveedor = fiscal.Version.ToArray(),
                ReceptorRfc = compra.EmpresaRfcAlComprar, ReceptorNombre = "", ReceptorRegimen = "",
                ReceptorCodigoPostal = "", UsoCfdi = "", Descripcion = "", ClaveProdServ = "", ClaveUnidad = "", FormaPago = "",
                CantidadTimbres = compra.CantidadTimbres, Subtotal = compra.Subtotal, Iva = compra.Iva,
                TasaIva = compra.TasaIva, Total = compra.PrecioTotal,
                CreadaUtc = DateTime.UtcNow, CreadaPorOperadorId = operadorId
            };
            datos.FacturasDeCompras.Add(factura);
        }
        factura.ReceptorNombre = NombreFiscal.Normalizar(p.NombreReceptor).Normalizado;
        factura.ReceptorRegimen = p.RegimenReceptor.Trim(); factura.ReceptorCodigoPostal = p.CodigoPostalReceptor.Trim();
        factura.UsoCfdi = p.UsoCfdi.Trim(); factura.Descripcion = p.Descripcion.Trim();
        factura.ClaveProdServ = p.ClaveProdServ.Trim(); factura.ClaveUnidad = p.ClaveUnidad.Trim();
        factura.FormaPago = p.FormaPago.Trim(); factura.ActualizadaUtc = DateTime.UtcNow;
        factura.ActualizadaPorOperadorId = operadorId;
        new ServicioDeBitacora(datos, cuenta, operador, accesor).Registrar(
            "FacturaDeCompra", factura.Id.ToString(), antes is null ? "factura_compra_creada" : "factura_compra_actualizada",
            antes, ADto(factura), empresaId: compra.EmpresaId, operadorId: operadorId);
        try { await datos.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Desactualizada(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return Desactualizada(); }
        return ADto(factura);
    }

    public async Task<IReadOnlyList<ClaveSatDto>> OpcionesAsync(string catalogo, string? texto, string? clave, CancellationToken ct)
    {
        if (catalogo is "c_ClaveProdServ" or "c_ClaveUnidad" or "c_CodigoPostal")
        {
            if (!string.IsNullOrWhiteSpace(clave) && clave.Length <= 8)
                return await catalogos.ResolverAsync(catalogo, clave, ct) is { } valor ? [valor] : [];
            return !string.IsNullOrWhiteSpace(texto) && texto.Trim().Length is >= 3 and <= 100
                ? await catalogos.BuscarAsync(catalogo, texto.Trim(), 50, ct) : [];
        }
        return catalogo switch
        {
            "c_RegimenFiscal" => await Simples(db.SatRegimenesFiscales, catalogo, ct),
            "c_UsoCFDI" => (await Simples(db.SatUsosCfdi, catalogo, ct)).Where(x => UsoDeFactura(x.Clave)).ToList(),
            "c_FormaPago" => (await Simples(db.SatFormasPago, catalogo, ct)).Where(x => x.Clave is not ("99" or "30")).ToList(),
            _ => []
        };
    }

    private static Task<List<ClaveSatDto>> Simples<T>(DbSet<T> tabla, string catalogo, CancellationToken ct)
        where T : class, ISatCatalogoSimple
        => tabla.AsNoTracking().Where(x => x.Vigente).OrderBy(x => x.Clave)
            .Select(x => new ClaveSatDto(catalogo, x.Clave, x.Descripcion, x.Vigente)).ToListAsync(ct);

    internal async Task<ErrorNegocio?> ValidarAsync(string rfc, PeticionGuardarFacturaCompra p, CancellationToken ct)
    {
        if (p.NombreReceptor is null || p.NombreReceptor.Length > 254 || string.IsNullOrWhiteSpace(p.NombreReceptor)
            || p.NombreReceptor.Contains('|') || p.NombreReceptor.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(p.Descripcion) || p.Descripcion.Length > 1000 || p.Descripcion.Contains('|')
            || p.Descripcion.Any(c => char.IsControl(c) && c is not ('\n' or '\r')))
            return ErrorNegocio.Validacion("factura-compra-texto-invalido", "Revisa la razón social y la descripción del paquete.");
        var validacionRfc = Rfc.Validar(rfc);
        if (!validacionRfc.EsValido)
            return ErrorNegocio.Validacion("receptor-rfc-invalido",
                $"RFC de la empresa compradora: {validacionRfc.Mensaje}");
        if (Rfc.EsGenerico(rfc))
            return ErrorNegocio.Validacion("receptor-rfc-invalido", "La empresa compradora debe tener un RFC propio.");
        var regimen = (p.RegimenReceptor ?? "").Trim();
        var fiscal = await db.SatRegimenesFiscales.AsNoTracking().SingleOrDefaultAsync(x => x.Clave == regimen && x.Vigente, ct);
        if (fiscal is null || (Rfc.EsPersonaMoral(rfc) ? !fiscal.AplicaMoral : !fiscal.AplicaFisica))
            return ErrorNegocio.Validacion("receptor-regimen-invalido", "Selecciona un régimen compatible con el receptor.");
        var uso = (p.UsoCfdi ?? "").Trim();
        if (!UsoDeFactura(uso))
            return ErrorNegocio.Validacion("receptor-uso-no-aplicable", "Selecciona un uso de CFDI para la factura de compra. Pagos y Nómina no aplican.");
        if (!await catalogos.EsUsoCfdiCompatibleAsync(uso, regimen, Rfc.EsPersonaMoral(rfc), ct))
            return ErrorNegocio.Validacion("receptor-uso-incompatible", "El uso de CFDI no corresponde al régimen del receptor.");
        var requeridas = new[] { ("c_CodigoPostal", p.CodigoPostalReceptor, 5), ("c_ClaveProdServ", p.ClaveProdServ, 8),
            ("c_ClaveUnidad", p.ClaveUnidad, 3), ("c_FormaPago", p.FormaPago, 2) };
        foreach (var (catalogo, valor, maximo) in requeridas)
        {
            if (string.IsNullOrWhiteSpace(valor) || valor.Trim().Length > maximo ||
                await catalogos.ResolverAsync(catalogo, valor.Trim(), ct) is not { Vigente: true })
                return ErrorNegocio.Validacion("clave-factura-compra-invalida", "Selecciona claves vigentes para el código postal, concepto y pago.");
        }
        if (p.FormaPago.Trim() is "99" or "30")
            return ErrorNegocio.Validacion("forma-pago-compra-invalida", "Selecciona la forma en que se recibió el pago.");
        return null;
    }

    private static bool UsoDeFactura(string clave) => clave.Length == 3 && clave is not ("CP01" or "CN01");

    private Task<CompraTimbres?> CompraAsync(Guid id, CancellationToken ct)
        // Consulta global exclusiva del operador; el navegador solo identifica la compra.
        => db.ComprasTimbres.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    internal static ErrorNegocio? ValidarCompra(CompraTimbres c)
        => c.Estado != EstadosDeCompra.Pagada || c.AcreditadaUtc is null
            ? ErrorNegocio.Conflicto("compra-sin-pago", "Solo se pueden facturar compras pagadas.")
            : c.CantidadTimbres <= 0 || c.PrecioTotal < 0 || c.Subtotal < 0 || c.Iva < 0 ||
              c.TasaIva is < 0 or > 1 || c.Subtotal + c.Iva != c.PrecioTotal
                ? ErrorNegocio.Conflicto("compra-importes-inconsistentes", "Revisa los importes guardados de la compra.") : null;
    private static ErrorNegocio NoEncontrada() => ErrorNegocio.NoEncontrado("compra-no-encontrada", "Esa compra no existe.");
    private static ErrorNegocio Desactualizada() => ErrorNegocio.Conflicto("factura-compra-modificada", "La factura cambió. Ciérrala y vuelve a abrirla.");
    private static EmisorFacturaCompraDto Emisor(ConfiguracionFiscalProveedor f)
        => new(f.Rfc, f.NombreFiscal, f.RegimenFiscal, f.CodigoPostalExpedicion, f.ZonaHoraria, f.Serie);
    private static FacturaDeCompraDto ADto(FacturaDeCompra f)
        => new(f.Id, f.CompraId, f.Estado, new(f.EmisorRfc, f.EmisorNombre, f.EmisorRegimen,
            f.EmisorCodigoPostal, f.EmisorZonaHoraria, f.Serie),
            new(f.ReceptorRfc, f.ReceptorNombre, f.ReceptorRegimen, f.ReceptorCodigoPostal, f.UsoCfdi),
            new(f.Descripcion, f.CantidadTimbres, f.ClaveProdServ, f.ClaveUnidad, f.Subtotal, f.Iva, f.TasaIva, f.Total),
            f.FormaPago, f.Version.Length == 0 ? null : f.Version, f.VersionProveedor) { Folio = f.Estado == "timbrado" ? f.Folio : null, Uuid = f.Uuid, FechaTimbradoUtc = f.FechaTimbradoUtc };
}
