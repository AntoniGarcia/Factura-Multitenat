using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

/// <summary>
/// Complementa lo que ya configuró <c>IdentityUserContext</c>. La tabla conserva el nombre
/// de Identity (<c>AspNetUsers</c>): renombrarla solo dejaría un esquema mitad en español
/// y mitad en inglés, porque las demás tablas de Identity seguirían con su nombre.
/// </summary>
public sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> constructor)
    {
        constructor.Property(u => u.Nombre).HasMaxLength(254);
        constructor.Property(u => u.TemaPreferido).HasMaxLength(16);

        constructor.HasOne(u => u.Cuenta)
            .WithMany(c => c.Usuarios)
            .HasForeignKey(u => u.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);

        constructor.HasIndex(u => u.CuentaId);

        // Un solo titular por cuenta: la base lo garantiza aunque el código se equivoque.
        // Con nombre propio: sin él, EF reconfiguraría el índice de arriba en lugar de crear otro.
        constructor.HasIndex(u => u.CuentaId, "IX_AspNetUsers_CuentaId_Titular")
            .IsUnique()
            .HasFilter("[EsTitular] = 1");
    }
}
