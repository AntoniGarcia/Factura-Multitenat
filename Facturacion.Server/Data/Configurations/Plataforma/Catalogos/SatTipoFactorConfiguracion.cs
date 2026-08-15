using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatTipoFactorConfiguracion : IEntityTypeConfiguration<SatTipoFactor>
{
    public void Configure(EntityTypeBuilder<SatTipoFactor> constructor)
    {
        constructor.ToTable("SatTipoFactor");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(16);
    }
}
