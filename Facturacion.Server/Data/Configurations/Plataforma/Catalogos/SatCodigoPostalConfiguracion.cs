using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatCodigoPostalConfiguracion : IEntityTypeConfiguration<SatCodigoPostal>
{
    public void Configure(EntityTypeBuilder<SatCodigoPostal> constructor)
    {
        constructor.ToTable("SatCodigoPostal");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(5);
        constructor.Property(x => x.ClaveEstado).HasMaxLength(3);
        constructor.Property(x => x.ClaveMunicipio).HasMaxLength(3);
        constructor.Property(x => x.ClaveLocalidad).HasMaxLength(4);
    }
}
