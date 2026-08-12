using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class RefreshTokenConfiguracion : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> constructor)
    {
        constructor.ToTable("RefreshTokens");

        constructor.HasKey(t => t.Id);

        // SHA-256 en base64 ocupa 44 caracteres.
        constructor.Property(t => t.HashToken).HasMaxLength(64);
        constructor.Property(t => t.MotivoRevocacion).HasMaxLength(128);
        constructor.Property(t => t.IpCreacion).HasMaxLength(45);
        constructor.Property(t => t.AgenteUsuario).HasMaxLength(256);

        constructor.HasIndex(t => t.HashToken).IsUnique();

        // Invalidar una familia completa es la operación caliente del circuito de identidad:
        // ocurre cada vez que se detecta la reutilización de un token.
        constructor.HasIndex(t => t.FamiliaId);

        constructor.HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
