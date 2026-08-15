using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

// Un IEntityTypeConfiguration por entidad, agrupados en un archivo porque las entidades que
// configuran también viven agrupadas en CatalogosSimples.cs: misma razón, mismo lugar.

public sealed class SatFormaPagoConfiguracion : IEntityTypeConfiguration<SatFormaPago>
{
    public void Configure(EntityTypeBuilder<SatFormaPago> c)
    {
        c.ToTable("SatFormaPago");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(4);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatExportacionConfiguracion : IEntityTypeConfiguration<SatExportacion>
{
    public void Configure(EntityTypeBuilder<SatExportacion> c)
    {
        c.ToTable("SatExportacion");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(2);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatMetodoPagoConfiguracion : IEntityTypeConfiguration<SatMetodoPago>
{
    public void Configure(EntityTypeBuilder<SatMetodoPago> c)
    {
        c.ToTable("SatMetodoPago");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(3);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatPeriodicidadConfiguracion : IEntityTypeConfiguration<SatPeriodicidad>
{
    public void Configure(EntityTypeBuilder<SatPeriodicidad> c)
    {
        c.ToTable("SatPeriodicidad");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(2);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatMesConfiguracion : IEntityTypeConfiguration<SatMes>
{
    public void Configure(EntityTypeBuilder<SatMes> c)
    {
        c.ToTable("SatMes");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(2);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatTipoRelacionConfiguracion : IEntityTypeConfiguration<SatTipoRelacion>
{
    public void Configure(EntityTypeBuilder<SatTipoRelacion> c)
    {
        c.ToTable("SatTipoRelacion");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(2);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatPaisConfiguracion : IEntityTypeConfiguration<SatPais>
{
    public void Configure(EntityTypeBuilder<SatPais> c)
    {
        c.ToTable("SatPais");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(3);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}

public sealed class SatObjetoImpConfiguracion : IEntityTypeConfiguration<SatObjetoImp>
{
    public void Configure(EntityTypeBuilder<SatObjetoImp> c)
    {
        c.ToTable("SatObjetoImp");
        c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(2);
        c.Property(x => x.Descripcion).HasMaxLength(254);
    }
}
