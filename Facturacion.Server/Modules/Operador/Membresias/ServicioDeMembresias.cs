using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Membresias;

/// <summary>
/// Las suscripciones de las cuentas contratantes: alta, renovación y baja.
///
/// <para><b>Sin filtro global que ayude</b></para>
/// <c>Membresia</c> no lleva <c>EmpresaId</c>, así que queda fuera del filtro de EF y el
/// acotado por cuenta hay que escribirlo a mano en cada consulta. Es la misma advertencia que
/// ya trae el servicio de compras del inquilino.
/// </summary>
public sealed class ServicioDeMembresias(
    AppDbContext baseDeDatos,
    IServicioDeBitacora bitacora)
{
    /// <summary>Histórico completo de una cuenta, de la más reciente a la más vieja.</summary>
    public async Task<IReadOnlyList<MembresiaDeOperadorDto>> ListarAsync(Guid cuentaId, CancellationToken ct)
        => await baseDeDatos.Membresias
            .AsNoTracking()
            .Where(m => m.CuentaId == cuentaId)
            .OrderByDescending(m => m.FinUtc)
            .Select(m => new MembresiaDeOperadorDto(
                m.Id, m.CuentaId, m.Cuenta.Nombre, m.InicioUtc, m.FinUtc, m.Estado, m.CreadaUtc))
            .ToListAsync(ct);

    /// <summary>
    /// Da de alta o renueva la suscripción de una cuenta.
    /// <para>
    /// No deja dos activas solapadas: al registrar una nueva, la activa anterior se marca como
    /// vencida. Con dos vigentes a la vez, cualquier consulta de "¿hasta cuándo tiene servicio
    /// esta cuenta?" tendría dos respuestas.
    /// </para>
    /// </summary>
    public async Task<Resultado<MembresiaDeOperadorDto>> RegistrarAsync(
        PeticionRegistrarMembresia peticion, CancellationToken ct)
    {
        if (peticion.FinUtc <= peticion.InicioUtc)
            return ErrorNegocio.Validacion(
                "vigencia-invalida", "La fecha de fin tiene que ser posterior a la de inicio.");

        var cuenta = await baseDeDatos.Cuentas
            .SingleOrDefaultAsync(c => c.Id == peticion.CuentaId, ct);

        if (cuenta is null)
            return ErrorNegocio.NoEncontrado("cuenta-no-encontrada", "Esa cuenta no existe.");

        var activas = await baseDeDatos.Membresias
            .Where(m => m.CuentaId == peticion.CuentaId && m.Estado == EstadosDeMembresia.Activa)
            .ToListAsync(ct);

        foreach (var anterior in activas)
            anterior.Estado = EstadosDeMembresia.Vencida;

        var membresia = new Membresia
        {
            Id = Guid.NewGuid(),
            CuentaId = peticion.CuentaId,
            InicioUtc = peticion.InicioUtc,
            FinUtc = peticion.FinUtc,
            Estado = EstadosDeMembresia.Activa,
            CreadaUtc = DateTime.UtcNow
        };

        baseDeDatos.Membresias.Add(membresia);

        bitacora.Registrar(
            EntidadesDeBitacora.Membresia,
            membresia.Id.ToString(),
            AccionesDeBitacora.MembresiaRegistrada,
            despues: new { cuenta.Nombre, membresia.InicioUtc, membresia.FinUtc },
            cuentaId: peticion.CuentaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return new MembresiaDeOperadorDto(
            membresia.Id, membresia.CuentaId, cuenta.Nombre,
            membresia.InicioUtc, membresia.FinUtc, membresia.Estado, membresia.CreadaUtc);
    }

    /// <summary>Da de baja una suscripción antes de su fecha de fin.</summary>
    public async Task<Resultado<bool>> CancelarAsync(Guid membresiaId, CancellationToken ct)
    {
        var membresia = await baseDeDatos.Membresias
            .Include(m => m.Cuenta)
            .SingleOrDefaultAsync(m => m.Id == membresiaId, ct);

        if (membresia is null)
            return ErrorNegocio.NoEncontrado("membresia-no-encontrada", "Esa membresía no existe.");

        if (membresia.Estado != EstadosDeMembresia.Activa)
            return ErrorNegocio.Conflicto(
                "membresia-no-activa", "Esa membresía ya no está activa.");

        membresia.Estado = EstadosDeMembresia.Cancelada;

        bitacora.Registrar(
            EntidadesDeBitacora.Membresia,
            membresia.Id.ToString(),
            AccionesDeBitacora.MembresiaCancelada,
            despues: new { membresia.Cuenta.Nombre, membresia.FinUtc },
            cuentaId: membresia.CuentaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }
}
