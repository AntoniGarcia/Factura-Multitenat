using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

/// <summary>
/// El índice de texto completo sobre <c>Descripcion</c> y <c>PalabrasSimilares</c> no se
/// declara aquí: Fluent API no tiene forma de expresar <c>FULLTEXT INDEX</c> de SQL Server.
/// Se crea con SQL crudo dentro de la migración que da de alta esta tabla.
/// </summary>
public sealed class SatClaveProdServConfiguracion : IEntityTypeConfiguration<SatClaveProdServ>
{
    public void Configure(EntityTypeBuilder<SatClaveProdServ> constructor)
    {
        constructor.ToTable("SatClaveProdServ");
        constructor.HasKey(x => x.Clave);
        constructor.Property(x => x.Clave).HasMaxLength(8);
        constructor.Property(x => x.Descripcion).HasMaxLength(512);
        constructor.Property(x => x.IncluirIvaTrasladado).HasMaxLength(16);
        constructor.Property(x => x.IncluirIepsTrasladado).HasMaxLength(16);
        constructor.Property(x => x.ComplementoQueDebeIncluir).HasMaxLength(128);
        constructor.Property(x => x.PalabrasSimilares).HasMaxLength(1024);
    }
}
