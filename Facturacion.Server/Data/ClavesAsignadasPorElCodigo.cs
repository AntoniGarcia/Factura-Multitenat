using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Facturacion.Server.Data;

/// Toda clave Guid la asigna el código. Con la configuración por omisión, EF toma un renglón
/// nuevo que ya trae clave como existente y lo marca modificado en vez de alta (UPDATE vacío o
/// rechazo del sellado de empresa). Usuario queda fuera: su modelo lo arma Identity.
internal sealed class ClavesAsignadasPorElCodigo : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelo, IConventionContext<IConventionModelBuilder> contexto)
    {
        foreach (var entidad in modelo.Metadata.GetEntityTypes())
        {
            if (typeof(Usuario).IsAssignableFrom(entidad.ClrType)) continue;

            if (entidad.FindPrimaryKey() is { Properties: [var clave] } && clave.ClrType == typeof(Guid))
                clave.Builder.ValueGenerated(ValueGenerated.Never);
        }
    }
}

// Si nadie asignó la clave, falla antes del guardado y nombra la entidad, en lugar de chocar
// con la llave primaria por Guid.Empty.
internal sealed class AltaSinClaveInterceptor : SaveChangesInterceptor
{
    public static AltaSinClaveInterceptor Instancia { get; } = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData datos, InterceptionResult<int> resultado)
    {
        Revisar(datos.Context);
        return base.SavingChanges(datos, resultado);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData datos, InterceptionResult<int> resultado, CancellationToken ct = default)
    {
        Revisar(datos.Context);
        return base.SavingChangesAsync(datos, resultado, ct);
    }

    private static void Revisar(DbContext? baseDeDatos)
    {
        if (baseDeDatos is null) return;

        foreach (var entrada in baseDeDatos.ChangeTracker.Entries())
        {
            if (entrada.State != EntityState.Added) continue;

            if (entrada.Metadata.FindPrimaryKey() is { Properties: [var clave] } &&
                clave.ClrType == typeof(Guid) &&
                clave.ValueGenerated == ValueGenerated.Never &&
                entrada.Property(clave.Name).CurrentValue is Guid valor && valor == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Se intentó dar de alta '{entrada.Metadata.ClrType.Name}' sin asignar su Id.");
            }
        }
    }
}
