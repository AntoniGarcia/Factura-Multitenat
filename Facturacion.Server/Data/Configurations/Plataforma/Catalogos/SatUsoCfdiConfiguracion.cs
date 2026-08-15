using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatUsoCfdiConfiguracion : IEntityTypeConfiguration<SatUsoCfdi>
{
    public void Configure(EntityTypeBuilder<SatUsoCfdi> constructor)
    {
        constructor.ToTable("SatUsoCfdi");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(4);
        constructor.Property(x => x.Descripcion).HasMaxLength(254);
        constructor.Property(x => x.RegimenesFiscalesAplicables).HasMaxLength(512);
    }
}
