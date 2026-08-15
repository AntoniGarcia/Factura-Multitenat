using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

/// <summary>
/// El índice de texto completo sobre <c>Nombre</c> se crea con SQL crudo en la migración,
/// igual que <see cref="SatMunicipioConfiguracion"/>. El índice normal sobre
/// <see cref="SatColonia.ClaveCodigoPostal"/> es el que resuelve "todas las colonias de este
/// código postal" sin recorrer la tabla completa.
/// </summary>
public sealed class SatColoniaConfiguracion : IEntityTypeConfiguration<SatColonia>
{
    public void Configure(EntityTypeBuilder<SatColonia> constructor)
    {
        constructor.ToTable("SatColonia");
        constructor.HasKey(x => new { x.Clave, x.ClaveCodigoPostal });
        constructor.Property(x => x.Clave).HasMaxLength(4);
        constructor.Property(x => x.ClaveCodigoPostal).HasMaxLength(5);
        constructor.Property(x => x.Nombre).HasMaxLength(254);

        constructor.HasIndex(x => x.ClaveCodigoPostal);

        // Única y de una sola columna: es lo que SQL Server exige como llave de un índice
        // de texto completo (ver comentario en la entidad).
        constructor.Property(x => x.IdInterno).UseIdentityColumn();
        constructor.HasIndex(x => x.IdInterno).IsUnique();
    }
}
