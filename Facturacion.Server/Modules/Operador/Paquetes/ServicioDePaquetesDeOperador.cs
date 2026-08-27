using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Paquetes;

/// <summary>
/// Administración del catálogo de paquetes que el SaaS vende.
///
/// <para><b>Sin filtro de empresa que esquivar</b></para>
/// <c>Paquete</c> es catálogo del producto, no dato de un inquilino: no lleva <c>EmpresaId</c>
/// y por eso queda fuera del filtro global. Aquí <b>no hace falta</b>
/// <c>IgnoreQueryFilters</c>, y añadirlo por costumbre solo confundiría a quien audite dónde
/// se está esquivando el aislamiento de verdad.
///
/// <para><b>Cambiar un precio no reescribe la historia</b></para>
/// <c>CompraTimbres</c> copia nombre, cantidad y precios en el momento de comprar, así que
/// subir una tarifa no altera lo que ya se cobró. Por lo mismo, un paquete se retira de la
/// venta desactivándolo y nunca borrándolo (ARQUITECTURA.md §5).
/// </summary>
public sealed class ServicioDePaquetesDeOperador(
    AppDbContext baseDeDatos,
    IServicioDeBitacora bitacora)
{
    /// <summary>Todos, activos e inactivos: el operador administra el catálogo completo.</summary>
    public async Task<IReadOnlyList<PaqueteDeOperadorDto>> ListarAsync(CancellationToken ct)
        => await baseDeDatos.Paquetes
            .AsNoTracking()
            .OrderBy(p => p.Orden)
            .Select(p => new PaqueteDeOperadorDto(
                p.Id, p.Nombre, p.CantidadTimbres, p.PrecioPorTimbre, p.PrecioTotal,
                p.VigenciaMeses, p.Activo, p.Orden,
                // Las compras sí están bajo el filtro global, pero aquí se cuentan desde el
                // paquete y sin proyectar ningún dato de empresa: el operador ve el número,
                // no de quién es cada una.
                baseDeDatos.ComprasTimbres.IgnoreQueryFilters().Count(c => c.PaqueteId == p.Id)))
            .ToListAsync(ct);

    public async Task<PaqueteDeOperadorDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => await baseDeDatos.Paquetes
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PaqueteDeOperadorDto(
                p.Id, p.Nombre, p.CantidadTimbres, p.PrecioPorTimbre, p.PrecioTotal,
                p.VigenciaMeses, p.Activo, p.Orden,
                // IgnoreQueryFilters justificado: mismo caso que en el listado, se cuenta a
                // través de todas las empresas y no se proyecta ningún dato suyo.
                baseDeDatos.ComprasTimbres.IgnoreQueryFilters().Count(c => c.PaqueteId == p.Id)))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<PaqueteDeOperadorDto>> CrearAsync(
        PeticionGuardarPaquete peticion, CancellationToken ct)
    {
        if (Validar(peticion) is { } error) return error;

        var paquete = new Paquete
        {
            Id = Guid.NewGuid(),
            Nombre = peticion.Nombre.Trim(),
            CantidadTimbres = peticion.CantidadTimbres,
            PrecioTotal = peticion.PrecioTotal,
            PrecioPorTimbre = peticion.PrecioPorTimbre,
            VigenciaMeses = peticion.VigenciaMeses,
            Orden = peticion.Orden,
            // Nace a la venta: quien lo da de alta lo está creando para venderlo.
            Activo = true
        };

        baseDeDatos.Paquetes.Add(paquete);

        bitacora.Registrar(
            EntidadesDeBitacora.Paquete,
            paquete.Id.ToString(),
            AccionesDeBitacora.PaqueteCreado,
            despues: Retrato(paquete));

        await baseDeDatos.SaveChangesAsync(ct);

        return await ObtenerAsync(paquete.Id, ct) is { } creado
            ? creado
            : ErrorNegocio.Regla("paquete-no-creado", "No se pudo leer el paquete recién creado.");
    }

    public async Task<Resultado<PaqueteDeOperadorDto>> ActualizarAsync(
        Guid id, PeticionGuardarPaquete peticion, CancellationToken ct)
    {
        if (Validar(peticion) is { } error) return error;

        var paquete = await baseDeDatos.Paquetes.SingleOrDefaultAsync(p => p.Id == id, ct);

        if (paquete is null)
            return ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete no existe.");

        var antes = Retrato(paquete);

        paquete.Nombre = peticion.Nombre.Trim();
        paquete.CantidadTimbres = peticion.CantidadTimbres;
        paquete.PrecioTotal = peticion.PrecioTotal;
        paquete.PrecioPorTimbre = peticion.PrecioPorTimbre;
        paquete.VigenciaMeses = peticion.VigenciaMeses;
        paquete.Orden = peticion.Orden;

        bitacora.Registrar(
            EntidadesDeBitacora.Paquete,
            paquete.Id.ToString(),
            AccionesDeBitacora.PaqueteActualizado,
            antes: antes,
            despues: Retrato(paquete));

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(paquete.Id, ct);
    }

    /// <summary>
    /// Pone o quita el paquete de la venta. Desactivarlo lo saca del catálogo del inquilino
    /// sin tocar ninguna compra pasada; es la única forma de retirarlo.
    /// </summary>
    public async Task<Resultado<PaqueteDeOperadorDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var paquete = await baseDeDatos.Paquetes.SingleOrDefaultAsync(p => p.Id == id, ct);

        if (paquete is null)
            return ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete no existe.");

        if (paquete.Activo == activo)
            return await ReleerAsync(id, ct);

        paquete.Activo = activo;

        bitacora.Registrar(
            EntidadesDeBitacora.Paquete,
            paquete.Id.ToString(),
            activo ? AccionesDeBitacora.PaqueteReactivado : AccionesDeBitacora.PaqueteDesactivado,
            despues: Retrato(paquete));

        await baseDeDatos.SaveChangesAsync(ct);

        return await ReleerAsync(id, ct);
    }

    /// <summary>
    /// Relee el paquete después de guardarlo, para devolverlo con los campos calculados. Que
    /// no esté sería una carrera con alguien retirándolo del catálogo; aun así se responde con
    /// un error de negocio y no con una excepción.
    /// </summary>
    private async Task<Resultado<PaqueteDeOperadorDto>> ReleerAsync(Guid id, CancellationToken ct)
        => await ObtenerAsync(id, ct) is { } paquete
            ? paquete
            : ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete ya no existe.");

    private static ErrorNegocio? Validar(PeticionGuardarPaquete peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre))
            return ErrorNegocio.Validacion("nombre-requerido", "El paquete necesita un nombre.");

        if (peticion.Nombre.Trim().Length > 100)
            return ErrorNegocio.Validacion("nombre-muy-largo", "El nombre no puede pasar de 100 caracteres.");

        if (peticion.CantidadTimbres <= 0)
            return ErrorNegocio.Validacion("cantidad-invalida", "La cantidad de timbres tiene que ser mayor que cero.");

        if (peticion.PrecioTotal <= 0)
            return ErrorNegocio.Validacion("precio-invalido", "El precio total tiene que ser mayor que cero.");

        if (peticion.PrecioPorTimbre <= 0)
            return ErrorNegocio.Validacion(
                "precio-unitario-invalido", "El precio por timbre tiene que ser mayor que cero.");

        if (peticion.VigenciaMeses < 1)
            return ErrorNegocio.Validacion("vigencia-invalida", "La vigencia tiene que ser de al menos un mes.");

        if (peticion.Orden < 0)
            return ErrorNegocio.Validacion("orden-invalido", "El orden no puede ser negativo.");

        return null;
    }

    private static object Retrato(Paquete paquete) => new
    {
        paquete.Nombre,
        paquete.CantidadTimbres,
        paquete.PrecioPorTimbre,
        paquete.PrecioTotal,
        paquete.VigenciaMeses,
        paquete.Activo,
        paquete.Orden
    };
}
