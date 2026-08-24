using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

/// <summary>
/// El índice de texto completo sobre <c>Descripcion</c> se crea con SQL crudo en la
/// migración: es uno de los dos catálogos que pide ARQUITECTURA.md §7 para la búsqueda de código
/// postal por nombre de municipio.
/// <para>
/// Sin llave foránea hacia <see cref="SatCodigoPostal"/> ni hacia <see cref="SatEstado"/>:
/// son catálogos independientes que solo comparten claves de texto, y forzar la relación
/// obligaría a cargar los cuatro catálogos en un orden exacto para que una fila con datos
/// incompletos del propio SAT no tumbe la importación completa.
/// </para>
/// </summary>
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
