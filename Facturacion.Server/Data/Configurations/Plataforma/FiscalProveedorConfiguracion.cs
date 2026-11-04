using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class FiscalProveedorConfiguracion : IEntityTypeConfiguration<ConfiguracionFiscalProveedor>
{
    public void Configure(EntityTypeBuilder<ConfiguracionFiscalProveedor> b)
    {
        b.ToTable("ConfiguracionFiscalProveedor", t =>
            t.HasCheckConstraint("CK_ConfiguracionFiscalProveedor_Unica", "[Id] = 1"));
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Rfc).HasMaxLength(13);
        b.Property(c => c.NombreFiscal).HasMaxLength(254);
        b.Property(c => c.RegimenFiscal).HasMaxLength(3);
        b.Property(c => c.CodigoPostalExpedicion).HasMaxLength(5);
        b.Property(c => c.ZonaHoraria).HasMaxLength(64);
        b.Property(c => c.Serie).HasMaxLength(25);
        b.Property(c => c.Version).IsRowVersion();
    }
}

public sealed class CertificadoProveedorConfiguracion : IEntityTypeConfiguration<CertificadoProveedor>
{
    public void Configure(EntityTypeBuilder<CertificadoProveedor> b)
    {
        b.ToTable("CertificadosProveedor");
        b.HasKey(c => c.Id);
        b.Property(c => c.Rfc).HasMaxLength(13);
        b.Property(c => c.NumeroSerie).HasMaxLength(40);
        b.Property(c => c.ContrasenaCifrada).HasMaxLength(3000);
        b.HasIndex(c => new { c.ConfiguracionId, c.NumeroSerie }).IsUnique();
        b.HasIndex(c => c.ConfiguracionId).IsUnique().HasFilter("[Activo] = 1");
        b.HasOne<ConfiguracionFiscalProveedor>().WithMany().HasForeignKey(c => c.ConfiguracionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
