using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class ComprobanteConfiguracion : IEntityTypeConfiguration<Comprobante>
{
    public void Configure(EntityTypeBuilder<Comprobante> constructor)
    {
        constructor.ToTable("Comprobantes");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Estatus).HasMaxLength(16);
        constructor.Property(c => c.Serie).HasMaxLength(25);
        constructor.Property(c => c.TipoDeComprobante).HasMaxLength(1);
        constructor.Property(c => c.LugarExpedicion).HasMaxLength(5);
        constructor.Property(c => c.Moneda).HasMaxLength(3);
        constructor.Property(c => c.FormaPago).HasMaxLength(2);
        constructor.Property(c => c.MetodoPago).HasMaxLength(3);
        constructor.Property(c => c.Exportacion).HasMaxLength(2);
        constructor.Property(c => c.CondicionesDePago).HasMaxLength(1000);
        constructor.Property(c => c.Observaciones).HasMaxLength(2000);

        constructor.Property(c => c.EmisorRfc).HasMaxLength(13);
        constructor.Property(c => c.EmisorNombre).HasMaxLength(254);
        constructor.Property(c => c.EmisorRegimenFiscal).HasMaxLength(3);

        constructor.Property(c => c.ReceptorRfc).HasMaxLength(13);
        constructor.Property(c => c.ReceptorNombre).HasMaxLength(254);
        constructor.Property(c => c.ReceptorRegimenFiscal).HasMaxLength(3);
        constructor.Property(c => c.ReceptorDomicilioFiscal).HasMaxLength(5);
        // Cuatro y no tres: casi todas las claves de c_UsoCFDI son de tres caracteres, pero
        // CP01 (pagos) y CN01 (nómina) son de cuatro. Estaba en tres desde B0 y lo destapó el
        // complemento de pagos, que siempre usa CP01; el catálogo de clientes ya la tenía bien.
        constructor.Property(c => c.ReceptorUsoCfdi).HasMaxLength(4);

        constructor.Property(c => c.GlobalPeriodicidad).HasMaxLength(2);
        constructor.Property(c => c.GlobalMeses).HasMaxLength(2);

        // Dinero a seis decimales, cálculo y almacenamiento; la presentación redondea a dos
        // (CLAUDE.md §5). El tipo de cambio también: el SAT admite hasta seis.
        constructor.Property(c => c.TipoCambio).HasPrecision(18, 6);
        constructor.Property(c => c.SubTotal).HasPrecision(18, 6);
        constructor.Property(c => c.Descuento).HasPrecision(18, 6);
        constructor.Property(c => c.TotalImpuestosTrasladados).HasPrecision(18, 6);
        constructor.Property(c => c.TotalImpuestosRetenidos).HasPrecision(18, 6);
        constructor.Property(c => c.Total).HasPrecision(18, 6);

        constructor.Property(c => c.NoCertificadoEmisor).HasMaxLength(20);
        constructor.Property(c => c.NoCertificadoSat).HasMaxLength(20);
        constructor.Property(c => c.RutaXml).HasMaxLength(400);

        constructor.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascada hacia los hijos: conceptos, relacionados e intentos no tienen vida propia
        // fuera de su comprobante. Solo se ejerce al borrar un borrador nunca timbrado, que
        // es la única excepción a «nada se borra» (CLAUDE.md §5).
        constructor.HasMany(c => c.Conceptos)
            .WithOne(x => x.Comprobante)
            .HasForeignKey(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(c => c.Relacionados)
            .WithOne(x => x.Comprobante)
            .HasForeignKey(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(c => c.Intentos)
            .WithOne(x => x.Comprobante)
            .HasForeignKey(x => x.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        // El folio no se repite dentro de su serie. Filtrado porque un borrador todavía no
        // tiene folio y puede haber muchos así: sin el filtro, el segundo borrador de la
        // empresa chocaría contra el primero.
        constructor.HasIndex(c => new { c.EmpresaId, c.SerieId, c.Folio })
            .IsUnique()
            .HasFilter("[Folio] IS NOT NULL")
            .HasDatabaseName("IX_Comprobantes_FolioPorSerie");

        // El UUID es único en todo el sistema, no por empresa: lo asigna el SAT.
        constructor.HasIndex(c => c.Uuid)
            .IsUnique()
            .HasFilter("[Uuid] IS NOT NULL")
            .HasDatabaseName("IX_Comprobantes_Uuid");

        // La consulta del listado: una empresa, un estatus, ordenado por fecha descendente.
        constructor.HasIndex(c => new { c.EmpresaId, c.Estatus, c.FechaEmisionUtc });

        // La conciliación busca los que llevan mucho en 'timbrando'; y el tablero cuenta por
        // estatus dentro de un periodo. Las dos entran por fecha dentro de la empresa.
        constructor.HasIndex(c => new { c.EmpresaId, c.FechaEmisionUtc });
    }
}

public sealed class ComprobanteRelacionadoConfiguracion : IEntityTypeConfiguration<ComprobanteRelacionado>
{
    public void Configure(EntityTypeBuilder<ComprobanteRelacionado> constructor)
    {
        constructor.ToTable("ComprobantesRelacionados");

        constructor.HasKey(r => r.Id);

        constructor.Property(r => r.TipoRelacion).HasMaxLength(2);

        // El mismo UUID no se relaciona dos veces en el mismo comprobante: el SAT lo rechaza.
        constructor.HasIndex(r => new { r.ComprobanteId, r.UuidRelacionado })
            .IsUnique()
            .HasDatabaseName("IX_ComprobantesRelacionados_SinRepetir");
    }
}
