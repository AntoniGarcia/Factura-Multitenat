using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class PermisoOperadorConfiguracion : IEntityTypeConfiguration<PermisoOperador>
{
    public void Configure(EntityTypeBuilder<PermisoOperador> constructor)
    {
        constructor.ToTable("OperadoresPermisos");

        constructor.HasKey(p => new { p.OperadorId, p.Permiso });

        constructor.Property(p => p.Permiso).HasMaxLength(64);

        constructor.HasOne(p => p.Operador)
            .WithMany(o => o.Permisos)
            .HasForeignKey(p => p.OperadorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice para consultar operadores por permiso (útil en bitácora y auditoría).
        constructor.HasIndex(p => p.Permiso)
            .HasDatabaseName("IX_OperadoresPermisos_Permiso");
    }
}