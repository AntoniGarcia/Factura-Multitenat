using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatSubTipoRemolqueConfiguracion : IEntityTypeConfiguration<SatSubTipoRemolque>
{
    public void Configure(EntityTypeBuilder<SatSubTipoRemolque> c)
    {
        c.ToTable("SatSubTipoRemolque"); c.HasKey(x => x.Clave);
        c.Property(x => x.Clave).HasMaxLength(6);
        c.Property(x => x.Descripcion).HasMaxLength(500);
    }
}
