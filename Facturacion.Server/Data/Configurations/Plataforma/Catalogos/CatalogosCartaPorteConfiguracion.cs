using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatConfiguracionAutotransporteConfiguracion : IEntityTypeConfiguration<SatConfiguracionAutotransporte>
{
    public void Configure(EntityTypeBuilder<SatConfiguracionAutotransporte> constructor)
        => Configurar(constructor, "SatConfiguracionAutotransporte");

    private static void Configurar(EntityTypeBuilder<SatConfiguracionAutotransporte> constructor, string tabla)
    {
        constructor.ToTable(tabla);
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(10);
        constructor.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public sealed class SatTipoPermisoConfiguracion : IEntityTypeConfiguration<SatTipoPermiso>
{
    public void Configure(EntityTypeBuilder<SatTipoPermiso> constructor)
    {
        constructor.ToTable("SatTipoPermiso");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(10);
        constructor.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public sealed class SatFiguraTransporteConfiguracion : IEntityTypeConfiguration<SatFiguraTransporte>
{
    public void Configure(EntityTypeBuilder<SatFiguraTransporte> constructor)
    {
        constructor.ToTable("SatFiguraTransporte");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(10);
        constructor.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public sealed class SatClaveProdServCartaPorteConfiguracion : IEntityTypeConfiguration<SatClaveProdServCartaPorte>
{
    public void Configure(EntityTypeBuilder<SatClaveProdServCartaPorte> constructor)
    {
        constructor.ToTable("SatClaveProdServCartaPorte");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(8);
        constructor.Property(x => x.Descripcion).HasMaxLength(500);
        constructor.HasIndex(x => new { x.Vigente, x.Clave });
    }
}
