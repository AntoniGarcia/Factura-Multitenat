using Facturacion.Server.Data.Entidades.Transporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Transporte;

public sealed class TrasladoCartaPorteConfiguracion : IEntityTypeConfiguration<TrasladoCartaPorte>
{
    public void Configure(EntityTypeBuilder<TrasladoCartaPorte> c)
    {
        c.ToTable("TrasladosCartaPorte"); c.HasKey(x => x.Id);
        c.Property(x => x.DistanciaRecorridaKm).HasPrecision(18, 6);
        c.Property(x => x.PesoBrutoTotalKg).HasPrecision(18, 6);
        c.Property(x => x.TotalMercancias).HasPrecision(18, 6);
        c.Property(x => x.IdCcp).HasMaxLength(36);
        c.Property(x => x.VehiculoConfiguracionAutotransporte).HasMaxLength(10);
        c.Property(x => x.VehiculoPlaca).HasMaxLength(20);
        c.Property(x => x.VehiculoPesoBruto).HasPrecision(18, 6);
        c.Property(x => x.VehiculoAseguradora).HasMaxLength(150);
        c.Property(x => x.VehiculoPoliza).HasMaxLength(50);
        c.Property(x => x.VehiculoTipoPermiso).HasMaxLength(10);
        c.Property(x => x.VehiculoNumeroPermiso).HasMaxLength(50);
        c.Property(x => x.FiguraTipo).HasMaxLength(10);
        c.Property(x => x.FiguraRfc).HasMaxLength(13);
        c.Property(x => x.FiguraNombre).HasMaxLength(254);
        c.Property(x => x.FiguraNumeroLicencia).HasMaxLength(50);
        c.HasIndex(x => x.ComprobanteId).IsUnique();
        c.HasIndex(x => x.IdCcp).IsUnique().HasFilter("[IdCcp] IS NOT NULL");
        c.HasOne(x => x.Comprobante).WithOne()
            .HasForeignKey<TrasladoCartaPorte>(x => x.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        c.HasOne<Vehiculo>().WithMany().HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<FiguraTransporte>().WithMany().HasForeignKey(x => x.FiguraTransporteId).OnDelete(DeleteBehavior.Restrict);
        c.HasMany(x => x.Ubicaciones).WithOne(x => x.TrasladoCartaPorte).HasForeignKey(x => x.TrasladoCartaPorteId).OnDelete(DeleteBehavior.Cascade);
        c.HasMany(x => x.Mercancias).WithOne(x => x.TrasladoCartaPorte).HasForeignKey(x => x.TrasladoCartaPorteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UbicacionCartaPorteConfiguracion : IEntityTypeConfiguration<UbicacionCartaPorte>
{
    public void Configure(EntityTypeBuilder<UbicacionCartaPorte> c)
    {
        c.ToTable("UbicacionesCartaPorte"); c.HasKey(x => x.Id);
        c.Property(x => x.Tipo).HasMaxLength(8); c.Property(x => x.RfcRemitenteDestinatario).HasMaxLength(13);
        c.Property(x => x.NombreRemitenteDestinatario).HasMaxLength(254);
        c.Property(x => x.Calle).HasMaxLength(150); c.Property(x => x.NumeroExterior).HasMaxLength(55); c.Property(x => x.NumeroInterior).HasMaxLength(55);
        c.Property(x => x.Estado).HasMaxLength(3); c.Property(x => x.Municipio).HasMaxLength(3); c.Property(x => x.CodigoPostal).HasMaxLength(5);
        c.HasIndex(x => new { x.TrasladoCartaPorteId, x.Orden }).IsUnique();
    }
}

public sealed class MercanciaCartaPorteConfiguracion : IEntityTypeConfiguration<MercanciaCartaPorte>
{
    public void Configure(EntityTypeBuilder<MercanciaCartaPorte> c)
    {
        c.ToTable("MercanciasCartaPorte"); c.HasKey(x => x.Id);
        c.Property(x => x.ClaveProdServ).HasMaxLength(8); c.Property(x => x.ClaveUnidad).HasMaxLength(20); c.Property(x => x.Descripcion).HasMaxLength(1000);
        c.Property(x => x.Unidad).HasMaxLength(20); c.Property(x => x.Dimensiones).HasMaxLength(14);
        c.Property(x => x.Cantidad).HasPrecision(18, 6); c.Property(x => x.PesoEnKg).HasPrecision(18, 6);
        c.Property(x => x.PesoUnitarioKg).HasPrecision(18, 6);
        c.HasIndex(x => new { x.TrasladoCartaPorteId, x.Orden }).IsUnique();
    }
}
