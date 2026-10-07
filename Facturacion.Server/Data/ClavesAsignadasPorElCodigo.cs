using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Facturacion.Server.Data;

/// <summary>
/// Declara que toda clave primaria <see cref="Guid"/> la asigna el código, no Entity Framework.
///
/// <para><b>Por qué</b></para>
/// Todo el código asigna <c>Id = Guid.NewGuid()</c> al crear la entidad. Con la configuración
/// por omisión —clave generada al agregar— Entity Framework interpreta que un renglón que ya
/// trae clave existe en la base: si llega colgado de la colección de una entidad rastreada
/// (los conceptos de un comprobante, las mercancías de un traslado) lo marca como modificado
/// en vez de como alta. El resultado es un <c>UPDATE</c> que no afecta nada o, en las
/// entidades de empresa, el rechazo del sellado. Con la clave declarada como asignada, un
/// renglón nuevo es alta llegue como llegue.
///
/// <para><b>Qué queda fuera</b></para>
/// <see cref="Usuario"/>: su modelo lo arma Identity. Las claves que no son <see cref="Guid"/>
/// (la bitácora con identidad de SQL Server, la configuración del sistema) tampoco cambian.
/// </summary>
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

/// <summary>
/// La otra cara de <see cref="ClavesAsignadasPorElCodigo"/>: si nadie asigna la clave, Entity
/// Framework ya no la genera y el renglón entraría con <see cref="Guid.Empty"/>. El primero
/// pasaría y el segundo chocaría con la llave primaria, con un error que no dice qué entidad
/// se olvidó. Esto falla antes, nombrándola.
/// </summary>
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
