using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatMaterialPeligrosoConfiguracion : IEntityTypeConfiguration<SatMaterialPeligroso>
{
    public void Configure(EntityTypeBuilder<SatMaterialPeligroso> c)
    {
        c.ToTable("SatMaterialPeligroso"); c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(4);
        c.Property(x => x.Descripcion).HasMaxLength(8000);
    }
}

public sealed class SatTipoEmbalajeConfiguracion : IEntityTypeConfiguration<SatTipoEmbalaje>
{
    public void Configure(EntityTypeBuilder<SatTipoEmbalaje> c)
    {
        c.ToTable("SatTipoEmbalaje"); c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(5);
        c.Property(x => x.Descripcion).HasMaxLength(500);
    }
}
