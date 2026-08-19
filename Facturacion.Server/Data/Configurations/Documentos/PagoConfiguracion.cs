using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class PagoConfiguracion : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> constructor)
    {
        constructor.ToTable("Pagos");

        constructor.HasKey(p => p.Id);

        constructor.Property(p => p.FormaDePagoP).HasMaxLength(2);
        constructor.Property(p => p.MonedaP).HasMaxLength(3);
        constructor.Property(p => p.NumOperacion).HasMaxLength(100);

        constructor.Property(p => p.RfcEmisorCtaOrd).HasMaxLength(13);
        constructor.Property(p => p.NomBancoOrdExt).HasMaxLength(300);
        constructor.Property(p => p.CtaOrdenante).HasMaxLength(50);
        constructor.Property(p => p.RfcEmisorCtaBen).HasMaxLength(13);
        constructor.Property(p => p.CtaBeneficiario).HasMaxLength(50);

        constructor.Property(p => p.Monto).HasPrecision(18, 6);
        constructor.Property(p => p.TipoCambioP).HasPrecision(18, 6);

        constructor.HasOne(p => p.Comprobante)
            .WithMany(c => c.Pagos)
            .HasForeignKey(p => p.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(p => p.Documentos)
            .WithOne(d => d.Pago)
            .HasForeignKey(d => d.PagoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un CFDI de pago lleva un solo nodo Pago en esta fase: la interfaz de §27 captura uno
        // por comprobante. El complemento admite varios, pero permitirlos sin pantalla que los
        // capture solo dejaría entrar datos que nadie puede revisar.
        constructor.HasIndex(p => p.ComprobanteId)
            .IsUnique()
            .HasDatabaseName("IX_Pagos_UnoPorComprobante");
    }
}

public sealed class DocumentoPagadoConfiguracion : IEntityTypeConfiguration<DocumentoPagado>
{
    public void Configure(EntityTypeBuilder<DocumentoPagado> constructor)
    {
        constructor.ToTable("PagosDocumentos");

        constructor.HasKey(d => d.Id);

        constructor.Property(d => d.Serie).HasMaxLength(25);
        constructor.Property(d => d.Folio).HasMaxLength(40);
        constructor.Property(d => d.MonedaDR).HasMaxLength(3);
        constructor.Property(d => d.ObjetoImpDR).HasMaxLength(2);

        constructor.Property(d => d.EquivalenciaDR).HasPrecision(18, 6);
        constructor.Property(d => d.ImpSaldoAnt).HasPrecision(18, 6);
        constructor.Property(d => d.ImpPagado).HasPrecision(18, 6);
        constructor.Property(d => d.ImpSaldoInsoluto).HasPrecision(18, 6);

        constructor.HasMany(d => d.Impuestos)
            .WithOne(i => i.DocumentoPagado)
            .HasForeignKey(i => i.DocumentoPagadoId)
            .OnDelete(DeleteBehavior.Cascade);

        // La misma factura no se abona dos veces dentro del mismo pago: serían dos
        // parcialidades con el mismo número y el SAT lo rechaza.
        constructor.HasIndex(d => new { d.PagoId, d.IdDocumento })
            .IsUnique()
            .HasDatabaseName("IX_PagosDocumentos_SinDocumentoRepetido");

        // Para resolver el saldo de una factura hay que sumar lo abonado en todos los pagos
        // anteriores, y esa consulta entra justo por aquí.
        constructor.HasIndex(d => d.IdDocumento)
            .HasDatabaseName("IX_PagosDocumentos_PorDocumento");
    }
}

public sealed class ImpuestoDocumentoPagadoConfiguracion : IEntityTypeConfiguration<ImpuestoDocumentoPagado>
{
    public void Configure(EntityTypeBuilder<ImpuestoDocumentoPagado> constructor)
    {
        constructor.ToTable("PagosDocumentosImpuestos");

        constructor.HasKey(i => i.Id);

        constructor.Property(i => i.Impuesto).HasMaxLength(3);
        constructor.Property(i => i.TipoFactor).HasMaxLength(16);

        constructor.Property(i => i.TasaOCuota).HasPrecision(18, 6);
        constructor.Property(i => i.Base).HasPrecision(18, 6);
        constructor.Property(i => i.Importe).HasPrecision(18, 6);

        // Mismo criterio que en ConceptosImpuestos: un impuesto no se declara dos veces en el
        // mismo sentido dentro del mismo documento pagado.
        constructor.HasIndex(i => new { i.DocumentoPagadoId, i.Impuesto, i.EsRetencion, i.TasaOCuota })
            .IsUnique()
            .HasDatabaseName("IX_PagosDocumentosImpuestos_SinImpuestoRepetido");
    }
}
