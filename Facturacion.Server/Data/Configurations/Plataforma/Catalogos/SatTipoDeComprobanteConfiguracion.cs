using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatTipoDeComprobanteConfiguracion : IEntityTypeConfiguration<SatTipoDeComprobante>
{
    public void Configure(EntityTypeBuilder<SatTipoDeComprobante> constructor)
    {
        constructor.ToTable("SatTipoDeComprobante");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(1);
        constructor.Property(x => x.Descripcion).HasMaxLength(254);
    }
}
