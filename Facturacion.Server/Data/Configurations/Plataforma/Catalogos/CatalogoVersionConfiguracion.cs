using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class CatalogoVersionConfiguracion : IEntityTypeConfiguration<CatalogoVersion>
{
    public void Configure(EntityTypeBuilder<CatalogoVersion> constructor)
    {
        constructor.ToTable("CatalogoVersion");
        constructor.HasKey(c => c.Catalogo);
        constructor.Property(c => c.Catalogo).HasMaxLength(32);
        constructor.Property(c => c.VersionCatalogo).HasMaxLength(16);
        constructor.Property(c => c.RevisionCatalogo).HasMaxLength(16);
    }
}
