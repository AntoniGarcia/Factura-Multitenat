using Facturacion.Server.Data.Entidades.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class IntentoTimbradoConfiguracion : IEntityTypeConfiguration<IntentoTimbrado>
{
    public void Configure(EntityTypeBuilder<IntentoTimbrado> constructor)
    {
        constructor.ToTable("IntentosTimbrado");

        constructor.HasKey(i => i.Id);

        constructor.Property(i => i.Resultado).HasMaxLength(32);
        constructor.Property(i => i.ClaveIdempotencia).HasMaxLength(128);
        constructor.Property(i => i.CodigoError).HasMaxLength(32);
        constructor.Property(i => i.MensajeError).HasMaxLength(2000);

        // Sin tope: un CFDI con muchos conceptos pasa de sobra cualquier límite razonable, y
        // truncarlo dejaría un XML que ya no se puede reenviar a conciliar.
        constructor.Property(i => i.XmlEnviado).HasColumnType("nvarchar(max)");

        constructor.HasIndex(i => new { i.ComprobanteId, i.Numero })
            .IsUnique()
            .HasDatabaseName("IX_IntentosTimbrado_NumeroPorComprobante");

        // La consulta de la conciliación: intentos que salieron y nunca volvieron. Se filtra
        // el índice porque los que ya cerraron no le interesan a nadie, y son la mayoría.
        constructor.HasIndex(i => new { i.EmpresaId, i.IniciadoUtc })
            .HasFilter("[Resultado] = 'en_vuelo'")
            .HasDatabaseName("IX_IntentosTimbrado_EnVuelo");
    }
}

public sealed class SolicitudCancelacionConfiguracion : IEntityTypeConfiguration<SolicitudCancelacion>
{
    public void Configure(EntityTypeBuilder<SolicitudCancelacion> constructor)
    {
        constructor.ToTable("SolicitudesCancelacion");

        constructor.HasKey(s => s.Id);

        constructor.Property(s => s.Motivo).HasMaxLength(2);
        constructor.Property(s => s.Estado).HasMaxLength(32);
        constructor.Property(s => s.CodigoRespuesta).HasMaxLength(32);
        constructor.Property(s => s.MensajeRespuesta).HasMaxLength(2000);

        constructor.HasOne(s => s.Comprobante)
            .WithMany()
            .HasForeignKey(s => s.ComprobanteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un comprobante puede acumular varias solicitudes —la primera se rechaza, se
        // corrige y se vuelve a pedir—, así que no hay índice único por comprobante. Lo que
        // se consulta es la última, y por eso el índice ordena por fecha.
        constructor.HasIndex(s => new { s.ComprobanteId, s.SolicitadaUtc })
            .HasDatabaseName("IX_SolicitudesCancelacion_PorComprobante");

        // Las que siguen abiertas: es lo que revisa el seguimiento contra el SAT.
        constructor.HasIndex(s => new { s.EmpresaId, s.Estado });
    }
}
