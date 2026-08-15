using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ReservaFolioConfiguracion : IEntityTypeConfiguration<ReservaFolio>
{
    public void Configure(EntityTypeBuilder<ReservaFolio> constructor)
    {
        constructor.ToTable("ReservasFolio");

        constructor.HasKey(r => r.Id);

        constructor.Property(r => r.Estado).HasMaxLength(16);

        constructor.HasOne(r => r.Serie)
            .WithMany()
            .HasForeignKey(r => r.SerieId)
            .OnDelete(DeleteBehavior.Restrict);

        // Es la red de seguridad de la reserva: aunque alguien lograra saltarse el
        // procedimiento almacenado, la base no acepta dos veces el mismo folio de la misma
        // serie. Un duplicado aquí es un comprobante rechazado por el SAT.
        constructor.HasIndex(r => new { r.SerieId, r.Folio })
            .IsUnique()
            .HasDatabaseName("IX_ReservasFolio_SinFolioRepetido");

        // El barrido de reservas viejas busca por estado y antigüedad.
        constructor.HasIndex(r => new { r.EmpresaId, r.Estado });
    }
}
