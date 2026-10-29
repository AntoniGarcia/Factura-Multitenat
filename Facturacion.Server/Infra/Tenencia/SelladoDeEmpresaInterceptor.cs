using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Sella <c>EmpresaId</c> en todo renglón nuevo y bloquea que un renglón existente cambie de
/// empresa.
/// <para>
/// El filtro global solo protege lecturas: un <c>Add</c> con la empresa equivocada, o un
/// cambio que mueva un renglón de una empresa a otra, entrarían sin que nadie se entere.
/// Esta es la otra mitad del aislamiento.
/// </para>
/// <para>
/// Las violaciones lanzan excepción y no devuelven error de negocio: no son un caso que el
/// usuario pueda corregir, son un defecto del código.
/// </para>
/// </summary>
public sealed class SelladoDeEmpresaInterceptor(IContextoEmpresaInterno contexto) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData datos, InterceptionResult<int> resultado)
    {
        Sellar(datos.Context);
        return base.SavingChanges(datos, resultado);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData datos, InterceptionResult<int> resultado, CancellationToken ct = default)
    {
        Sellar(datos.Context);
        return base.SavingChangesAsync(datos, resultado, ct);
    }

    private void Sellar(DbContext? baseDeDatos)
    {
        if (baseDeDatos is null) return;

        foreach (var entrada in baseDeDatos.ChangeTracker.Entries<IEntidadDeEmpresa>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    if (contexto.EmpresaActual is not { } empresa)
                        throw new InvalidOperationException(
                            $"Se intentó dar de alta '{entrada.Entity.GetType().Name}' sin empresa activa.");

                    if (entrada.Entity.EmpresaId == Guid.Empty)
                        entrada.Entity.EmpresaId = empresa;
                    else if (entrada.Entity.EmpresaId != empresa)
                        throw new InvalidOperationException(
                            $"Se intentó dar de alta '{entrada.Entity.GetType().Name}' en una empresa " +
                            "distinta de la activa.");
                    break;

                case EntityState.Modified:
                    if (entrada.Property(e => e.EmpresaId).IsModified)
                        throw new InvalidOperationException(
                            $"Se intentó mover '{entrada.Entity.GetType().Name}' de una empresa a otra.");
                    VerificarEmpresaActiva();
                    break;

                case EntityState.Deleted:
                    VerificarEmpresaActiva();
                    break;
            }

            void VerificarEmpresaActiva()
            {
                // Los procesos globales y el operador conservan su alcance explícito sin
                // empresa activa. Una petición de empresa nunca puede escribir otra tenencia.
                if (contexto.EmpresaActual is not { } activa) return;
                var original = entrada.Property(e => e.EmpresaId).OriginalValue;
                if (entrada.Entity.EmpresaId != activa || original != activa)
                    throw new InvalidOperationException(
                        $"Se intentó modificar o eliminar '{entrada.Entity.GetType().Name}' de una empresa distinta de la activa.");
            }
        }

        // Lo mismo para lo que es de la cuenta y comparten sus empresas (IEntidadDeCuenta).
        foreach (var entrada in baseDeDatos.ChangeTracker.Entries<IEntidadDeCuenta>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    if (contexto.CuentaActual is not { } cuenta)
                        throw new InvalidOperationException(
                            $"Se intentó dar de alta '{entrada.Entity.GetType().Name}' sin cuenta.");

                    if (entrada.Entity.CuentaId == Guid.Empty)
                        entrada.Entity.CuentaId = cuenta;
                    else if (entrada.Entity.CuentaId != cuenta)
                        throw new InvalidOperationException(
                            $"Se intentó dar de alta '{entrada.Entity.GetType().Name}' en otra cuenta.");
                    break;

                case EntityState.Modified:
                    if (entrada.Property(e => e.CuentaId).IsModified)
                        throw new InvalidOperationException(
                            $"Se intentó mover '{entrada.Entity.GetType().Name}' de una cuenta a otra.");
                    VerificarCuentaActiva();
                    break;

                case EntityState.Deleted:
                    VerificarCuentaActiva();
                    break;
            }

            void VerificarCuentaActiva()
            {
                if (contexto.CuentaActual is not { } activa) return;
                if (entrada.Property(e => e.CuentaId).OriginalValue != activa)
                    throw new InvalidOperationException(
                        $"Se intentó modificar o eliminar '{entrada.Entity.GetType().Name}' de otra cuenta.");
            }
        }
    }
}
