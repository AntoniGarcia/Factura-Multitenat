using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class DatosObraConfiguracion : IEntityTypeConfiguration<DatosObra>
{
    public void Configure(EntityTypeBuilder<DatosObra> configuracion)
    {
        configuracion.ToTable("DatosObra");
        configuracion.HasKey(x => x.Id);
        configuracion.HasIndex(x => x.ComprobanteId).IsUnique();
        configuracion.HasOne(x => x.Comprobante).WithOne()
            .HasForeignKey<DatosObra>(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);
        configuracion.Property(x => x.TipoObra).HasMaxLength(10);
        configuracion.Property(x => x.Retenciones).HasPrecision(18, 6);
        configuracion.Property(x => x.Devoluciones).HasPrecision(18, 6);
        configuracion.Property(x => x.PorcentajeAmortizacion).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeRetenciones).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeDevoluciones).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeIva).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeDeduccion1).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeDeduccion2).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeDeduccion3).HasPrecision(8, 4);
        configuracion.Property(x => x.PorcentajeDeduccion4).HasPrecision(8, 4);
        configuracion.Property(x => x.NombreDeduccion1).HasMaxLength(60);
        configuracion.Property(x => x.NombreDeduccion2).HasMaxLength(60);
        configuracion.Property(x => x.NombreDeduccion3).HasMaxLength(60);
        configuracion.Property(x => x.NombreDeduccion4).HasMaxLength(60);
    }
}
