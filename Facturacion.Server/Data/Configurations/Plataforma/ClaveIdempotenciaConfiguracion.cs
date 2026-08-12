using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ClaveIdempotenciaConfiguracion : IEntityTypeConfiguration<ClaveIdempotencia>
{
    public void Configure(EntityTypeBuilder<ClaveIdempotencia> constructor)
    {
        constructor.ToTable("ClavesIdempotencia");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Clave).HasMaxLength(128);
        constructor.Property(c => c.Endpoint).HasMaxLength(256);
        constructor.Property(c => c.HashPeticion).HasMaxLength(64);

        // La unicidad incluye la empresa: una clave repetida entre empresas distintas es
        // legítima, y sin la empresa en el índice una empresa podría recibir la respuesta
        // guardada de otra.
        constructor.HasIndex(c => new { c.EmpresaId, c.Clave }).IsUnique();

        // Lo recorre la purga cada hora.
        constructor.HasIndex(c => c.ExpiraUtc);
    }
}
