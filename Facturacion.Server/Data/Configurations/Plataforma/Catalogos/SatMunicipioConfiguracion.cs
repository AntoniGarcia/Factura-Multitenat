using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

/// <summary>El índice de texto completo se crea en la migración. No usa llaves foráneas entre catálogos SAT para permitir cargas independientes.</summary>
public sealed class SatMunicipioConfiguracion : IEntityTypeConfiguration<SatMunicipio>
{
    public void Configure(EntityTypeBuilder<SatMunicipio> constructor)
    {
        constructor.ToTable("SatMunicipio");
        constructor.HasKey(x => new { x.Clave, x.ClaveEstado });
        constructor.Property(x => x.Clave).HasMaxLength(3);
        constructor.Property(x => x.ClaveEstado).HasMaxLength(3);
        constructor.Property(x => x.Descripcion).HasMaxLength(254);

        // Única y de una sola columna: es lo que SQL Server exige como llave de un índice
        // de texto completo (ver comentario en la entidad).
        constructor.Property(x => x.IdInterno).UseIdentityColumn();
        constructor.HasIndex(x => x.IdInterno).IsUnique();
    }
}
