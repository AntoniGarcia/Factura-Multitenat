using Facturacion.Server.Data.Entidades.Transporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Transporte;

public sealed class VehiculoConfiguracion : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> constructor)
    {
        constructor.ToTable("Vehiculos");
        constructor.HasKey(x => x.Id);
        constructor.Property(x => x.Clave).HasMaxLength(30);
        constructor.Property(x => x.Descripcion).HasMaxLength(250);
        constructor.Property(x => x.ConfiguracionAutotransporte).HasMaxLength(10);
        constructor.Property(x => x.Placa).HasMaxLength(20);
        constructor.Property(x => x.Aseguradora).HasMaxLength(150);
        constructor.Property(x => x.Poliza).HasMaxLength(50);
        constructor.Property(x => x.TipoPermiso).HasMaxLength(10);
        constructor.Property(x => x.NumeroPermiso).HasMaxLength(50);
        constructor.Property(x => x.PesoBrutoVehicular).HasPrecision(18, 6);
        constructor.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(x => new { x.EmpresaId, x.Clave }).IsUnique();
        constructor.HasIndex(x => new { x.EmpresaId, x.Activo, x.Descripcion });
    }
}

public sealed class FiguraTransporteConfiguracion : IEntityTypeConfiguration<FiguraTransporte>
{
    public void Configure(EntityTypeBuilder<FiguraTransporte> constructor)
    {
        constructor.ToTable("FigurasTransporte");
        constructor.HasKey(x => x.Id);
        constructor.Property(x => x.Clave).HasMaxLength(30);
        constructor.Property(x => x.TipoFigura).HasMaxLength(10);
        constructor.Property(x => x.Rfc).HasMaxLength(13);
        constructor.Property(x => x.Nombre).HasMaxLength(254);
        constructor.Property(x => x.NumeroLicencia).HasMaxLength(50);
        constructor.Property(x => x.Calle).HasMaxLength(150);
        constructor.Property(x => x.NumeroExterior).HasMaxLength(55);
        constructor.Property(x => x.NumeroInterior).HasMaxLength(55);
        constructor.Property(x => x.Estado).HasMaxLength(3);
        constructor.Property(x => x.Municipio).HasMaxLength(3);
        constructor.Property(x => x.CodigoPostal).HasMaxLength(5);
        constructor.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(x => new { x.EmpresaId, x.Clave }).IsUnique();
        constructor.HasIndex(x => new { x.EmpresaId, x.Activo, x.Nombre });
    }
}
