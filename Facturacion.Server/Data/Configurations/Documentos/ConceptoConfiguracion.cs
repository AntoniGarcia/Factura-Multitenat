using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class ConceptoConfiguracion : IEntityTypeConfiguration<Concepto>
{
    public void Configure(EntityTypeBuilder<Concepto> constructor)
    {
        constructor.ToTable("Conceptos");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.ClaveProdServ).HasMaxLength(8);
        constructor.Property(c => c.ClaveUnidad).HasMaxLength(20);
        constructor.Property(c => c.UnidadTexto).HasMaxLength(64);
        constructor.Property(c => c.NoIdentificacion).HasMaxLength(100);
        constructor.Property(c => c.Descripcion).HasMaxLength(1000);
        constructor.Property(c => c.ObjetoImp).HasMaxLength(2);

        // Cantidad también a seis decimales: se factura por kilos y por horas, no solo por
        // piezas enteras.
        constructor.Property(c => c.Cantidad).HasPrecision(18, 6);
        constructor.Property(c => c.ValorUnitario).HasPrecision(18, 6);
        constructor.Property(c => c.Importe).HasPrecision(18, 6);
        constructor.Property(c => c.Descuento).HasPrecision(18, 6);

        constructor.HasMany(c => c.Impuestos)
            .WithOne(i => i.Concepto)
            .HasForeignKey(i => i.ConceptoId)
            .OnDelete(DeleteBehavior.Cascade);

        // El XML respeta el orden de captura, así que no puede haber dos renglones en la
        // misma posición.
        constructor.HasIndex(c => new { c.ComprobanteId, c.Orden })
            .IsUnique()
            .HasDatabaseName("IX_Conceptos_OrdenPorComprobante");
    }
}

public sealed class ImpuestoConceptoConfiguracion : IEntityTypeConfiguration<ImpuestoConcepto>
{
    public void Configure(EntityTypeBuilder<ImpuestoConcepto> constructor)
    {
        constructor.ToTable("ConceptosImpuestos");

        constructor.HasKey(i => i.Id);

        constructor.Property(i => i.Impuesto).HasMaxLength(3);
        constructor.Property(i => i.TipoFactor).HasMaxLength(16);

        constructor.Property(i => i.TasaOCuota).HasPrecision(18, 6);
        constructor.Property(i => i.Base).HasPrecision(18, 6);
        constructor.Property(i => i.Importe).HasPrecision(18, 6);

        // El mismo impuesto no se declara dos veces en el mismo sentido dentro de un
        // renglón: dos traslados de IVA en un concepto son rechazo del PAC.
        constructor.HasIndex(i => new { i.ConceptoId, i.Impuesto, i.EsRetencion })
            .IsUnique()
            .HasDatabaseName("IX_ConceptosImpuestos_SinImpuestoRepetido");
    }
}
