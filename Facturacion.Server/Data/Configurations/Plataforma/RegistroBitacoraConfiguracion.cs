using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class RegistroBitacoraConfiguracion : IEntityTypeConfiguration<RegistroBitacora>
{
    public void Configure(EntityTypeBuilder<RegistroBitacora> constructor)
    {
        constructor.ToTable("Bitacora");

        constructor.HasKey(b => b.Id);
        constructor.Property(b => b.Id).ValueGeneratedOnAdd();

        constructor.Property(b => b.Entidad).HasMaxLength(128);
        constructor.Property(b => b.EntidadId).HasMaxLength(64);
        constructor.Property(b => b.Accion).HasMaxLength(64);
        constructor.Property(b => b.TraceId).HasMaxLength(64);
        constructor.Property(b => b.IpOrigen).HasMaxLength(45);

        // Se consulta por empresa y periodo, y por cuenta y periodo para los eventos que
        // ocurren antes de que haya empresa activa.
        constructor.HasIndex(b => new { b.EmpresaId, b.MomentoUtc });
        constructor.HasIndex(b => new { b.CuentaId, b.MomentoUtc });

        // Qué hizo un operador y cuándo: es la consulta con la que se audita quién acreditó
        // un pago. Filtrado porque la inmensa mayoría de los renglones no son suyos.
        constructor.HasIndex(b => new { b.OperadorId, b.MomentoUtc })
            .HasFilter("[OperadorId] IS NOT NULL")
            .HasDatabaseName("IX_Bitacora_PorOperador");

        // A propósito sin llaves foráneas: la bitácora tiene que sobrevivir a lo que
        // registra. Un renglón que ya no puede explicar qué pasó no sirve de nada.
    }
}
