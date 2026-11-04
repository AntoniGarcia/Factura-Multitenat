using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class FacturaDeCompraConfiguracion : IEntityTypeConfiguration<FacturaDeCompra>
{
    public void Configure(EntityTypeBuilder<FacturaDeCompra> b)
    {
        b.ToTable("FacturasDeCompras");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.CompraId).IsUnique();
        b.HasOne<CompraTimbres>().WithMany().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CuentaId, x.CreadaUtc });
        b.Property(x => x.Estado).HasMaxLength(20);
        b.Property(x => x.EmisorRfc).HasMaxLength(13);
        b.Property(x => x.EmisorNombre).HasMaxLength(254);
        b.Property(x => x.EmisorRegimen).HasMaxLength(3);
        b.Property(x => x.EmisorCodigoPostal).HasMaxLength(5);
        b.Property(x => x.EmisorZonaHoraria).HasMaxLength(64);
        b.Property(x => x.Serie).HasMaxLength(25);
        b.Property(x => x.VersionProveedor).HasMaxLength(8);
        b.Property(x => x.ReceptorRfc).HasMaxLength(13);
        b.Property(x => x.ReceptorNombre).HasMaxLength(254);
        b.Property(x => x.ReceptorRegimen).HasMaxLength(3);
        b.Property(x => x.ReceptorCodigoPostal).HasMaxLength(5);
        b.Property(x => x.UsoCfdi).HasMaxLength(3);
        b.Property(x => x.Descripcion).HasMaxLength(1000);
        b.Property(x => x.ClaveProdServ).HasMaxLength(8);
        b.Property(x => x.ClaveUnidad).HasMaxLength(3);
        b.Property(x => x.FormaPago).HasMaxLength(2);
        b.Property(x => x.Version).IsRowVersion();
    }
}
