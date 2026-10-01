using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class EnvioDeCorreoConfiguracion : IEntityTypeConfiguration<EnvioDeCorreo>
{
    public void Configure(EntityTypeBuilder<EnvioDeCorreo> constructor)
    {
        constructor.ToTable("EnviosDeCorreo");

        constructor.HasKey(e => e.Id);

        // Diez direcciones de hasta 254 caracteres, más separadores: el tope que acepta el envío.
        constructor.Property(e => e.Destinatarios).HasMaxLength(2600);
        constructor.Property(e => e.CopiaOculta).HasMaxLength(254);
        constructor.Property(e => e.Asunto).HasMaxLength(200);
        constructor.Property(e => e.Error).HasMaxLength(64);

        // Igual que las solicitudes de cancelación: sin vida propia fuera de su comprobante.
        constructor.HasOne(e => e.Comprobante)
            .WithMany()
            .HasForeignKey(e => e.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        // El historial de un comprobante, del más reciente al más viejo.
        constructor.HasIndex(e => new { e.ComprobanteId, e.EnviadoUtc })
            .HasDatabaseName("IX_EnviosDeCorreo_PorComprobante");
    }
}
