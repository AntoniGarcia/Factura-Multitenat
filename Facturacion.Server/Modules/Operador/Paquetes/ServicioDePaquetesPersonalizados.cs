using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Paquetes;

/// <summary>Ofertas exclusivas que el operador prepara para una cuenta: cualquiera de sus empresas puede comprarlas.</summary>
public sealed class ServicioDePaquetesPersonalizados(
    AppDbContext baseDeDatos,
    IServicioDeBitacora bitacora)
{
    public async Task<IReadOnlyList<PaquetePersonalizadoDto>> ListarAsync(
        Guid cuentaId, CancellationToken ct)
        => await baseDeDatos.Paquetes
            .AsNoTracking()
            .Where(p => p.CuentaId == cuentaId)
            .OrderByDescending(p => p.Activo)
            .ThenBy(p => p.Orden)
            .Select(p => new PaquetePersonalizadoDto(
                p.Id, p.Nombre, p.CantidadTimbres, p.PrecioPorTimbre, p.PrecioTotal,
                p.VigenciaMeses, p.Activo,
                baseDeDatos.ComprasTimbres.IgnoreQueryFilters().Count(c => c.PaqueteId == p.Id)))
            .ToListAsync(ct);

    public async Task<Resultado<PaquetePersonalizadoDto>> CrearAsync(
        Guid operadorId, Guid cuentaId,
        PeticionGuardarPaquetePersonalizado peticion, CancellationToken ct)
    {
        if (Validar(peticion) is { } error) return error;
        if (!await ExisteCuentaAsync(cuentaId, ct))
            return ErrorNegocio.NoEncontrado("cuenta-no-encontrada", "Esa cuenta no existe.");

        var paquete = new Paquete
        {
            Id = Guid.NewGuid(),
            CuentaId = cuentaId,
            Nombre = peticion.Nombre.Trim(),
            CantidadTimbres = peticion.CantidadTimbres,
            PrecioTotal = Math.Round(peticion.PrecioTotal, 6, MidpointRounding.AwayFromZero),
            PrecioPorTimbre = Math.Round(
                peticion.PrecioTotal / peticion.CantidadTimbres, 6, MidpointRounding.AwayFromZero),
            VigenciaMeses = peticion.VigenciaMeses,
            Orden = await SiguienteOrdenAsync(cuentaId, ct),
            Activo = true
        };

        baseDeDatos.Paquetes.Add(paquete);
        Registrar(paquete, AccionesDeBitacora.PaqueteCreado, null, cuentaId, operadorId);
        await baseDeDatos.SaveChangesAsync(ct);
        return ADto(paquete, 0);
    }

    public async Task<Resultado<PaquetePersonalizadoDto>> ActualizarAsync(
        Guid operadorId, Guid cuentaId, Guid paqueteId,
        PeticionGuardarPaquetePersonalizado peticion, CancellationToken ct)
    {
        if (Validar(peticion) is { } error) return error;

        var paquete = await baseDeDatos.Paquetes.SingleOrDefaultAsync(
            p => p.Id == paqueteId && p.CuentaId == cuentaId, ct);
        if (paquete is null)
            return ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete no pertenece a la cuenta.");

        var antes = Retrato(paquete);
        paquete.Nombre = peticion.Nombre.Trim();
        paquete.CantidadTimbres = peticion.CantidadTimbres;
        paquete.PrecioTotal = Math.Round(peticion.PrecioTotal, 6, MidpointRounding.AwayFromZero);
        paquete.PrecioPorTimbre = Math.Round(
            paquete.PrecioTotal / paquete.CantidadTimbres, 6, MidpointRounding.AwayFromZero);
        paquete.VigenciaMeses = peticion.VigenciaMeses;

        Registrar(paquete, AccionesDeBitacora.PaqueteActualizado, antes, cuentaId, operadorId);
        await baseDeDatos.SaveChangesAsync(ct);
        return await ReleerAsync(paquete, ct);
    }

    public async Task<Resultado<PaquetePersonalizadoDto>> CambiarActivoAsync(
        Guid operadorId, Guid cuentaId, Guid paqueteId,
        bool activo, CancellationToken ct)
    {
        var paquete = await baseDeDatos.Paquetes.SingleOrDefaultAsync(
            p => p.Id == paqueteId && p.CuentaId == cuentaId, ct);
        if (paquete is null)
            return ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete no pertenece a la cuenta.");

        if (paquete.Activo != activo)
        {
            paquete.Activo = activo;
            Registrar(
                paquete,
                activo ? AccionesDeBitacora.PaqueteReactivado : AccionesDeBitacora.PaqueteDesactivado,
                null,
                cuentaId,
                operadorId);
            await baseDeDatos.SaveChangesAsync(ct);
        }

        return await ReleerAsync(paquete, ct);
    }

    private Task<bool> ExisteCuentaAsync(Guid cuentaId, CancellationToken ct)
        => baseDeDatos.Cuentas.AsNoTracking().AnyAsync(c => c.Id == cuentaId, ct);

    private async Task<int> SiguienteOrdenAsync(Guid cuentaId, CancellationToken ct)
        => (await baseDeDatos.Paquetes
            .Where(p => p.CuentaId == cuentaId)
            .MaxAsync(p => (int?)p.Orden, ct) ?? 0) + 1;

    private async Task<PaquetePersonalizadoDto> ReleerAsync(Paquete paquete, CancellationToken ct)
        => ADto(paquete, await baseDeDatos.ComprasTimbres.IgnoreQueryFilters()
            .CountAsync(c => c.PaqueteId == paquete.Id, ct));

    private static PaquetePersonalizadoDto ADto(Paquete paquete, int compras)
        => new(
            paquete.Id, paquete.Nombre, paquete.CantidadTimbres, paquete.PrecioPorTimbre,
            paquete.PrecioTotal, paquete.VigenciaMeses, paquete.Activo, compras);

    private static ErrorNegocio? Validar(PeticionGuardarPaquetePersonalizado peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre))
            return ErrorNegocio.Validacion("nombre-requerido", "El paquete necesita un nombre.");
        if (peticion.Nombre.Trim().Length > 100)
            return ErrorNegocio.Validacion("nombre-muy-largo", "El nombre no puede pasar de 100 caracteres.");
        if (peticion.CantidadTimbres is < 1 or > 1_000_000)
            return ErrorNegocio.Validacion("cantidad-invalida", "La cantidad debe estar entre 1 y 1,000,000.");
        if (peticion.PrecioTotal is < 0.01m or > 10_000_000m)
            return ErrorNegocio.Validacion("precio-invalido", "El total debe estar entre $0.01 y $10,000,000.");
        if (peticion.VigenciaMeses is < 1 or > 24)
            return ErrorNegocio.Validacion("vigencia-invalida", "La vigencia debe estar entre 1 y 24 meses.");
        return null;
    }

    private void Registrar(
        Paquete paquete, string accion, object? antes, Guid cuentaId, Guid operadorId)
        => bitacora.Registrar(
            EntidadesDeBitacora.Paquete,
            paquete.Id.ToString(),
            accion,
            antes,
            Retrato(paquete),
            cuentaId: cuentaId,
            operadorId: operadorId);

    private static object Retrato(Paquete paquete) => new
    {
        paquete.CuentaId,
        paquete.Nombre,
        paquete.CantidadTimbres,
        paquete.PrecioPorTimbre,
        paquete.PrecioTotal,
        paquete.VigenciaMeses,
        paquete.Activo
    };
}
