using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatClaveUnidadConfiguracion : IEntityTypeConfiguration<SatClaveUnidad>
{
    public void Configure(EntityTypeBuilder<SatClaveUnidad> constructor)
    {
        constructor.ToTable("SatClaveUnidad");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(20);
        constructor.Property(x => x.Nombre).HasMaxLength(254);
        // 512 se quedó corto de verdad: el SAT tiene descripciones de hasta 551 caracteres
        // para algunas unidades heredadas. Se comprobó midiendo el archivo real, no adivinando.
        constructor.Property(x => x.Descripcion).HasMaxLength(1024);
        constructor.Property(x => x.Nota).HasMaxLength(512);
        constructor.Property(x => x.Simbolo).HasMaxLength(32);
    }
}
