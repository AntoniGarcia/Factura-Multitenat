using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class DatosComercioExteriorConfiguracion : IEntityTypeConfiguration<DatosComercioExterior>
{
    public void Configure(EntityTypeBuilder<DatosComercioExterior> configuracion)
    {
        configuracion.ToTable("DatosComercioExterior");
        configuracion.HasKey(x => x.Id);
        configuracion.HasIndex(x => x.ComprobanteId).IsUnique();
        configuracion.HasOne(x => x.Comprobante).WithOne()
            .HasForeignKey<DatosComercioExterior>(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);
        configuracion.Property(x => x.Contenido).HasColumnType("nvarchar(max)");
    }
}
