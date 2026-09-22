using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class DatosNotariaConfiguracion : IEntityTypeConfiguration<DatosNotaria>
{
    public void Configure(EntityTypeBuilder<DatosNotaria> configuracion)
    {
        configuracion.ToTable("DatosNotaria");
        configuracion.HasKey(x => x.Id);
        configuracion.Property(x => x.MontoOperacion).HasPrecision(18, 6);
        configuracion.Property(x => x.SubtotalOperacion).HasPrecision(18, 6);
        configuracion.Property(x => x.IvaOperacion).HasPrecision(18, 6);
        configuracion.HasIndex(x => x.ComprobanteId).IsUnique();
        configuracion.HasOne(x => x.Comprobante).WithOne()
            .HasForeignKey<DatosNotaria>(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);
        configuracion.HasMany(x => x.Inmuebles).WithOne(x => x.DatosNotaria)
            .HasForeignKey(x => x.DatosNotariaId)
            .OnDelete(DeleteBehavior.Cascade);
        configuracion.HasMany(x => x.Partes).WithOne(x => x.DatosNotaria)
            .HasForeignKey(x => x.DatosNotariaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParteNotarialConfiguracion : IEntityTypeConfiguration<ParteNotarial>
{
    public void Configure(EntityTypeBuilder<ParteNotarial> configuracion)
    {
        configuracion.ToTable("PartesNotariales");
        configuracion.HasKey(x => x.Id);
        configuracion.Property(x => x.Rol).HasMaxLength(12);
        configuracion.Property(x => x.Nombre).HasMaxLength(254);
        configuracion.Property(x => x.ApellidoPaterno).HasMaxLength(200);
        configuracion.Property(x => x.ApellidoMaterno).HasMaxLength(200);
        configuracion.Property(x => x.Rfc).HasMaxLength(13);
        configuracion.Property(x => x.Curp).HasMaxLength(18);
        configuracion.Property(x => x.Porcentaje).HasPrecision(5, 2);
        configuracion.HasIndex(x => new { x.DatosNotariaId, x.Rol, x.Orden }).IsUnique();
    }
}

public sealed class InmuebleNotarialConfiguracion : IEntityTypeConfiguration<InmuebleNotarial>
{
    public void Configure(EntityTypeBuilder<InmuebleNotarial> configuracion)
    {
        configuracion.ToTable("InmueblesNotariales");
        configuracion.HasKey(x => x.Id);
        configuracion.Property(x => x.TipoInmueble).HasMaxLength(2);
        configuracion.Property(x => x.Calle).HasMaxLength(150);
        configuracion.Property(x => x.NumeroExterior).HasMaxLength(55);
        configuracion.Property(x => x.NumeroInterior).HasMaxLength(30);
        configuracion.Property(x => x.Colonia).HasMaxLength(100);
        configuracion.Property(x => x.Localidad).HasMaxLength(100);
        configuracion.Property(x => x.Referencia).HasMaxLength(100);
        configuracion.Property(x => x.Municipio).HasMaxLength(100);
        configuracion.Property(x => x.Estado).HasMaxLength(2);
        configuracion.Property(x => x.Pais).HasMaxLength(3);
        configuracion.Property(x => x.CodigoPostal).HasMaxLength(5);
        configuracion.HasIndex(x => new { x.DatosNotariaId, x.Orden }).IsUnique();
    }
}
