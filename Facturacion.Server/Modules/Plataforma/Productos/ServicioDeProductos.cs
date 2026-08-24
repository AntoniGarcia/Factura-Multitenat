using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Productos;

/// <summary>
/// Catálogo de productos de la empresa activa. Implementa
/// <see cref="IServicioProductos"/> del contrato congelado, que es lo que la mitad B consume
/// para armar un concepto.
/// </summary>
public sealed class ServicioDeProductos(
    AppDbContext baseDeDatos,
    ValidadorDeProducto validador,
    IServicioDeBitacora bitacora) : IServicioProductos
{
    public async Task<ProductoParaConceptoDto?> ObtenerParaConceptoAsync(Guid productoId, CancellationToken ct)
    {
        var producto = await baseDeDatos.Productos
            .AsNoTracking()
            .Include(p => p.Impuestos)
            .FirstOrDefaultAsync(p => p.Id == productoId, ct);

        if (producto is null || !producto.Activo) return null;

        return new ProductoParaConceptoDto(
            producto.Id,
            producto.ClaveProdServ,
            producto.ClaveUnidad,
            producto.UnidadTexto,
            producto.Descripcion,
            producto.ValorUnitario,
            producto.ObjetoImp,
            [.. producto.Impuestos.Select(i =>
                new ImpuestoProductoDto(i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion))]);
    }

    public async Task<PaginaDeProductos> ListarAsync(
        string? texto, bool? activos, int pagina, int tamano, string? orden, bool descendente, CancellationToken ct)
    {
        var consulta = baseDeDatos.Productos.AsNoTracking();

        // Tres estados: null todos, true activos, false dados de baja (ver ServicioDeClientes).
        if (activos is { } valor) consulta = consulta.Where(p => p.Activo == valor);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var buscado = texto.Trim();
            consulta = consulta.Where(p =>
                p.Descripcion.Contains(buscado) || p.ClaveProdServ.Contains(buscado) || p.UnidadTexto.Contains(buscado));
        }

        var total = await consulta.CountAsync(ct);

        consulta = (orden, descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(p => p.CodigoInterno),
            ("codigo", true) => consulta.OrderByDescending(p => p.CodigoInterno),
            ("precio", false) => consulta.OrderBy(p => p.ValorUnitario),
            ("precio", true) => consulta.OrderByDescending(p => p.ValorUnitario),
            (_, true) => consulta.OrderByDescending(p => p.Descripcion),
            _ => consulta.OrderBy(p => p.Descripcion)
        };

        var elementos = await consulta
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(p => new ProductoEnListaDto(
                p.Id, p.CodigoInterno, p.Descripcion, p.UnidadTexto,
                p.ValorUnitario, p.PesoKg, p.ClaveProdServ, p.Activo))
            .ToListAsync(ct);

        return new PaginaDeProductos(elementos, total);
    }

    public async Task<ProductoDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var producto = await baseDeDatos.Productos
            .AsNoTracking()
            .Include(p => p.Impuestos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return producto is null ? null : ADto(producto);
    }

    public Task<List<Producto>> ListarParaExportarAsync(bool? activos, CancellationToken ct)
    {
        IQueryable<Producto> consulta = baseDeDatos.Productos.AsNoTracking().Include(p => p.Impuestos);

        if (activos is { } valor) consulta = consulta.Where(p => p.Activo == valor);

        return consulta.OrderBy(p => p.CodigoInterno).ToListAsync(ct);
    }

    public async Task<Resultado<ProductoDto>> CrearAsync(PeticionGuardarProducto peticion, CancellationToken ct)
    {
        var error = await validador.ValidarAsync(peticion, ct);
        if (error is not null) return error;

        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            CodigoInterno = await SiguienteCodigoAsync(ct),
            ClaveProdServ = peticion.ClaveProdServ,
            ClaveUnidad = peticion.ClaveUnidad,
            UnidadTexto = peticion.UnidadTexto.Trim(),
            Descripcion = peticion.Descripcion.Trim(),
            ValorUnitario = peticion.ValorUnitario,
            PesoKg = peticion.PesoKg,
            ObjetoImp = peticion.ObjetoImp,
            Activo = peticion.Activo,
            FechaAltaUtc = DateTime.UtcNow,
            Impuestos = [.. peticion.Impuestos.Select(AImpuesto)]
        };

        baseDeDatos.Productos.Add(producto);

        bitacora.Registrar(
            EntidadesDeBitacora.Producto, producto.Id.ToString(), AccionesDeBitacora.ProductoCreado,
            despues: ADto(producto));

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(producto);
    }

    public async Task<Resultado<ProductoDto>> ActualizarAsync(
        Guid id, PeticionGuardarProducto peticion, CancellationToken ct)
    {
        var producto = await baseDeDatos.Productos
            .Include(p => p.Impuestos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (producto is null)
            return ErrorNegocio.NoEncontrado("producto-no-encontrado", "No se encontró ese producto.");

        var error = await validador.ValidarAsync(peticion, ct);
        if (error is not null) return error;

        var antes = ADto(producto);

        producto.ClaveProdServ = peticion.ClaveProdServ;
        producto.ClaveUnidad = peticion.ClaveUnidad;
        producto.UnidadTexto = peticion.UnidadTexto.Trim();
        producto.Descripcion = peticion.Descripcion.Trim();
        producto.ValorUnitario = peticion.ValorUnitario;
        producto.PesoKg = peticion.PesoKg;
        producto.ObjetoImp = peticion.ObjetoImp;
        producto.Activo = peticion.Activo;
        producto.FechaModificacionUtc = DateTime.UtcNow;

        // La configuración fiscal se reemplaza entera: es más simple y más seguro que
        // intentar casar renglón por renglón, y el histórico no vive aquí sino congelado
        // dentro de cada comprobante ya timbrado.
        //
        // Va en dos pasos dentro de una transacción, y no en un solo SaveChanges:
        //
        //  1. Quitar el hijo de la colección no lo borra. EF lo deja en estado Modified
        //     —intentando desligarlo— y como la llave foránea no admite nulos, el UPDATE no
        //     afecta ningún renglón y revienta con DbUpdateConcurrencyException. Se comprobó
        //     con una prueba dirigida que imprimió el estado de la entidad.
        //  2. Aunque se marcaran como borrados, el índice único (ProductoId, Impuesto,
        //     EsRetencion) obliga a que el borrado ocurra ANTES del alta: reemplazar el IVA
        //     por otro IVA chocaría con el renglón viejo si EF ordenara al revés.
        //
        // La transacción es lo que mantiene el producto y sus impuestos consistentes: sin
        // ella, un fallo entre los dos pasos dejaría el producto sin configuración fiscal.
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        baseDeDatos.ProductosImpuestos.RemoveRange(producto.Impuestos);
        await baseDeDatos.SaveChangesAsync(ct);

        foreach (var impuesto in peticion.Impuestos)
        {
            var nuevo = AImpuesto(impuesto);
            nuevo.ProductoId = producto.Id;
            baseDeDatos.ProductosImpuestos.Add(nuevo);
        }

        bitacora.Registrar(
            EntidadesDeBitacora.Producto, producto.Id.ToString(), AccionesDeBitacora.ProductoActualizado,
            antes, new { peticion.Descripcion, peticion.ValorUnitario, peticion.ObjetoImp, peticion.Impuestos });

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return (await ObtenerAsync(id, ct))!;
    }

    public async Task<Resultado<ProductoDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct)
    {
        var producto = await baseDeDatos.Productos
            .Include(p => p.Impuestos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (producto is null)
            return ErrorNegocio.NoEncontrado("producto-no-encontrado", "No se encontró ese producto.");

        if (producto.Activo == activo) return ADto(producto);

        producto.Activo = activo;
        producto.FechaModificacionUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.Producto, producto.Id.ToString(),
            activo ? AccionesDeBitacora.ProductoReactivado : AccionesDeBitacora.ProductoDesactivado,
            antes: new { Activo = !activo }, despues: new { Activo = activo });

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(producto);
    }

    /// <summary>
    /// Guarda los renglones válidos de una importación ya revisada. Los que fallen al
    /// guardar se devuelven, no se tragan: el usuario tiene que saber qué no entró.
    /// </summary>
    public async Task<ResultadoDeImportacion> ImportarAsync(
        IReadOnlyList<RenglonDeImportacion> renglones, CancellationToken ct)
    {
        var creados = 0;
        var actualizados = 0;
        var rechazados = new List<RenglonDeImportacion>();

        foreach (var renglon in renglones)
        {
            if (renglon.Producto is null)
            {
                rechazados.Add(renglon);
                continue;
            }

            // Se reconoce por clave del SAT + descripción: es lo que distingue un producto
            // en la práctica y permite recargar el mismo archivo sin duplicar el catálogo.
            var existente = await baseDeDatos.Productos
                .Include(p => p.Impuestos)
                .FirstOrDefaultAsync(p => p.ClaveProdServ == renglon.Producto.ClaveProdServ
                                          && p.Descripcion == renglon.Producto.Descripcion, ct);

            var resultado = existente is null
                ? await CrearAsync(renglon.Producto, ct)
                : await ActualizarAsync(existente.Id, renglon.Producto, ct);

            if (resultado.EsFallo)
                rechazados.Add(renglon with { Error = resultado.Error!.Mensaje });
            else if (existente is null)
                creados++;
            else
                actualizados++;
        }

        return new ResultadoDeImportacion(creados, actualizados, rechazados);
    }

    /// <summary>
    /// Siguiente consecutivo. Vale lo mismo que en clientes: un código repetido lo ataja el
    /// índice único y no tiene efecto fiscal, a diferencia de un folio.
    /// </summary>
    private async Task<int> SiguienteCodigoAsync(CancellationToken ct)
        => await baseDeDatos.Productos.MaxAsync(p => (int?)p.CodigoInterno, ct) + 1 ?? 1;

    private static ProductoImpuesto AImpuesto(ImpuestoDeProductoDto i) => new()
    {
        Id = Guid.NewGuid(),
        Impuesto = i.Impuesto,
        TipoFactor = i.TipoFactor,
        TasaOCuota = i.TasaOCuota,
        EsRetencion = i.EsRetencion
    };

    private static ProductoDto ADto(Producto p) => new(
        p.Id, p.CodigoInterno, p.ClaveProdServ, p.ClaveUnidad, p.UnidadTexto, p.Descripcion,
        p.ValorUnitario, p.PesoKg, p.ObjetoImp,
        [.. p.Impuestos.Select(i => new ImpuestoDeProductoDto(i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion))],
        p.Activo);
}
