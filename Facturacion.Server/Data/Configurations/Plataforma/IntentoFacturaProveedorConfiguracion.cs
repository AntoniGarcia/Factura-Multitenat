using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class SerieFacturaProveedorConfiguracion : IEntityTypeConfiguration<SerieFacturaProveedor>
{
    public void Configure(EntityTypeBuilder<SerieFacturaProveedor> b)
    {
        b.ToTable("SeriesFacturasProveedor");
        b.HasKey(x => new { x.Rfc, x.Serie });
        b.Property(x => x.Rfc).HasMaxLength(13);
        b.Property(x => x.Serie).HasMaxLength(25);
    }
}

public sealed class IntentoFacturaProveedorConfiguracion : IEntityTypeConfiguration<IntentoFacturaProveedor>
{
    public void Configure(EntityTypeBuilder<IntentoFacturaProveedor> b)
    {
        b.ToTable("IntentosFacturasProveedor");
        b.HasKey(x => x.Id);
        b.HasOne<FacturaDeCompra>().WithMany().HasForeignKey(x => x.FacturaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CertificadoProveedor>().WithMany().HasForeignKey(x => x.CertificadoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperadorId, x.ClaveSolicitud }).IsUnique();
        b.HasIndex(x => x.FacturaId).IsUnique().HasFilter("[Estado] = N'timbrando'");
        b.HasIndex(x => new { x.Estado, x.ActualizadaUtc });
        b.Property(x => x.ClaveSolicitud).HasMaxLength(128);
        b.Property(x => x.VersionSolicitada).HasMaxLength(8);
        b.Property(x => x.Estado).HasMaxLength(20);
        b.Property(x => x.CodigoError).HasMaxLength(100);
        b.Property(x => x.NoCertificado).HasMaxLength(40);
        b.Property(x => x.NoCertificadoSat).HasMaxLength(40);
    }
}
