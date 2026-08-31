using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class AltaPendienteConfiguracion : IEntityTypeConfiguration<AltaPendiente>
{
    public void Configure(EntityTypeBuilder<AltaPendiente> constructor)
    {
        constructor.ToTable("AltasPendientes");

        constructor.HasKey(a => a.Id);

        constructor.Property(a => a.Correo).HasMaxLength(254);
        constructor.Property(a => a.Nombre).HasMaxLength(128);
        constructor.Property(a => a.NombreCuenta).HasMaxLength(200);
        constructor.Property(a => a.IpCreacion).HasMaxLength(45);

        // Cabe de sobra el formato de IPasswordHasher, que en base64 ronda los 90 caracteres.
        constructor.Property(a => a.HashCodigo).HasMaxLength(256);

        // Se busca por correo al verificar. NO es único: un mismo correo puede tener varias
        // altas si alguien pidió el código y volvió a pedirlo, y el índice único convertiría
        // ese segundo intento en un error del servidor.
        constructor.HasIndex(a => a.Correo)
            .HasDatabaseName("IX_AltasPendientes_Correo");

        // Para la purga de caducadas, que barre por fecha.
        constructor.HasIndex(a => a.ExpiraUtc)
            .HasDatabaseName("IX_AltasPendientes_Expira");
    }
}
