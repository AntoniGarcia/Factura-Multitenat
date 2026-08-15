using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatMonedaConfiguracion : IEntityTypeConfiguration<SatMoneda>
{
    public void Configure(EntityTypeBuilder<SatMoneda> constructor)
    {
        constructor.ToTable("SatMoneda");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(3);
        constructor.Property(x => x.Descripcion).HasMaxLength(254);
    }
}
