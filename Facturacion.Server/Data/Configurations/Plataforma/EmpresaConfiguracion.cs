using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class EmpresaConfiguracion : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> constructor)
    {
        constructor.ToTable("Empresas");

        constructor.HasKey(e => e.Id);

        constructor.Property(e => e.Rfc).HasMaxLength(13);
        constructor.Property(e => e.NombreFiscal).HasMaxLength(254);
        constructor.Property(e => e.RegimenFiscal).HasMaxLength(3);
        constructor.Property(e => e.CodigoPostalExpedicion).HasMaxLength(5);
        constructor.Property(e => e.ZonaHoraria).HasMaxLength(64);

        // El RFC es único dentro de la cuenta, no en toda la base: dos cuentas distintas
        // pueden ser dos despachos que administran la misma empresa.
        constructor.HasIndex(e => new { e.CuentaId, e.Rfc }).IsUnique();

        constructor.HasOne(e => e.Cuenta)
            .WithMany(c => c.Empresas)
            .HasForeignKey(e => e.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
