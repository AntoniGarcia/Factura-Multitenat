using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatEstadoConfiguracion : IEntityTypeConfiguration<SatEstado>
{
    public void Configure(EntityTypeBuilder<SatEstado> constructor)
    {
        constructor.ToTable("SatEstado");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(3);
        constructor.Property(x => x.ClavePais).HasMaxLength(3);
        constructor.Property(x => x.Nombre).HasMaxLength(254);
    }
}
