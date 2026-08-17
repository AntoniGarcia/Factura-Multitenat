using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class InvitacionConfiguracion : IEntityTypeConfiguration<Invitacion>
{
    public void Configure(EntityTypeBuilder<Invitacion> constructor)
    {
        constructor.ToTable("Invitaciones");

        constructor.HasKey(i => i.Id);

        constructor.Property(i => i.Correo).HasMaxLength(254);
        constructor.Property(i => i.Nombre).HasMaxLength(128);
        constructor.Property(i => i.HashToken).HasMaxLength(64);
        constructor.Property(i => i.PermisosClaves).HasMaxLength(256);

        constructor.HasIndex(i => i.HashToken).IsUnique();

        // Una sola invitación pendiente por correo y por empresa: reenviar reutiliza el
        // mismo renglón (nuevo token, misma fila) en vez de acumular duplicados.
        constructor.HasIndex(i => new { i.EmpresaId, i.Correo })
            .IsUnique()
            .HasFilter("[AceptadaUtc] IS NULL AND [RevocadaUtc] IS NULL")
            .HasDatabaseName("IX_Invitaciones_PendientePorCorreoYEmpresa");
    }
}
