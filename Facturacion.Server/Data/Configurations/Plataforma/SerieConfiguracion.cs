using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class SerieConfiguracion : IEntityTypeConfiguration<Serie>
{
    public void Configure(EntityTypeBuilder<Serie> constructor)
    {
        constructor.ToTable("Series");

        constructor.HasKey(s => s.Id);

        constructor.Property(s => s.Prefijo).HasMaxLength(25);
        constructor.Property(s => s.TipoComprobante).HasMaxLength(1);

        constructor.HasOne(s => s.Empresa)
            .WithMany()
            .HasForeignKey(s => s.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // El prefijo es único dentro de la empresa: dos series con el mismo prefijo
        // producirían folios repetidos para el SAT, que identifica el comprobante por
        // RFC + serie + folio.
        constructor.HasIndex(s => new { s.EmpresaId, s.Prefijo }).IsUnique();
    }
}
