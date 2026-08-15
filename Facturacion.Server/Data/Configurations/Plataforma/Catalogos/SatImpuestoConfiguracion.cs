using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatImpuestoConfiguracion : IEntityTypeConfiguration<SatImpuesto>
{
    public void Configure(EntityTypeBuilder<SatImpuesto> constructor)
    {
        constructor.ToTable("SatImpuesto");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(3);
        constructor.Property(x => x.Descripcion).HasMaxLength(64);
        constructor.Property(x => x.LocalOFederal).HasMaxLength(16);
    }
}
