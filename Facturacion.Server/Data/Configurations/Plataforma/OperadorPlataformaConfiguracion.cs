using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class OperadorPlataformaConfiguracion : IEntityTypeConfiguration<OperadorPlataforma>
{
    public void Configure(EntityTypeBuilder<OperadorPlataforma> constructor)
    {
        constructor.ToTable("OperadoresPlataforma");

        constructor.HasKey(o => o.Id);

        constructor.Property(o => o.Nombre).HasMaxLength(128);
        constructor.Property(o => o.Correo).HasMaxLength(254);
        constructor.Property(o => o.CorreoNormalizado).HasMaxLength(254);

        // Cabe de sobra el formato de IPasswordHasher, que en base64 ronda los 90 caracteres.
        constructor.Property(o => o.HashContrasena).HasMaxLength(256);

        // Por aquí entra el inicio de sesión, y dos operadores no pueden compartir correo.
        constructor.HasIndex(o => o.CorreoNormalizado)
            .IsUnique()
            .HasDatabaseName("IX_OperadoresPlataforma_Correo");

        constructor.HasMany(o => o.RefreshTokens)
            .WithOne(t => t.Operador)
            .HasForeignKey(t => t.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RefreshTokenOperadorConfiguracion : IEntityTypeConfiguration<RefreshTokenOperador>
{
    public void Configure(EntityTypeBuilder<RefreshTokenOperador> constructor)
    {
        constructor.ToTable("RefreshTokensOperador");

        constructor.HasKey(t => t.Id);

        // SHA-256 en base64 ocupa 44 caracteres.
        constructor.Property(t => t.HashToken).HasMaxLength(64);
        constructor.Property(t => t.MotivoRevocacion).HasMaxLength(128);
        constructor.Property(t => t.IpCreacion).HasMaxLength(45);
        constructor.Property(t => t.AgenteUsuario).HasMaxLength(256);

        constructor.HasIndex(t => t.HashToken).IsUnique();

        // Invalidar la familia entera es lo que ocurre al detectar un token reutilizado.
        constructor.HasIndex(t => t.FamiliaId);
    }
}
