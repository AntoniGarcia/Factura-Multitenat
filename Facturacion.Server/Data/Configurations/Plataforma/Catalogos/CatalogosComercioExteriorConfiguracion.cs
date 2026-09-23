using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatIncotermConfiguracion : IEntityTypeConfiguration<SatIncoterm>
{
    public void Configure(EntityTypeBuilder<SatIncoterm> entidad)
    {
        entidad.ToTable("SatIncoterm");
        entidad.HasKey(x => x.Clave);
        entidad.Property(x => x.Clave).HasMaxLength(10);
        entidad.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public sealed class SatUnidadAduanaConfiguracion : IEntityTypeConfiguration<SatUnidadAduana>
{
    public void Configure(EntityTypeBuilder<SatUnidadAduana> entidad)
    {
        entidad.ToTable("SatUnidadAduana");
        entidad.HasKey(x => x.Clave);
        entidad.Property(x => x.Clave).HasMaxLength(10);
        entidad.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public sealed class SatFraccionArancelariaConfiguracion : IEntityTypeConfiguration<SatFraccionArancelaria>
{
    public void Configure(EntityTypeBuilder<SatFraccionArancelaria> entidad)
    {
        entidad.ToTable("SatFraccionArancelaria");
        entidad.HasKey(x => x.Clave);
        entidad.Property(x => x.Clave).HasMaxLength(12);
        entidad.Property(x => x.Descripcion).HasMaxLength(2000);
        entidad.HasIndex(x => new { x.Vigente, x.Clave });
    }
}
