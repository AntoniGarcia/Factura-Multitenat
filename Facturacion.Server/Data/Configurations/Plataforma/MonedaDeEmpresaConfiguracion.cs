using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class MonedaDeEmpresaConfiguracion : IEntityTypeConfiguration<MonedaDeEmpresa>
{
    public void Configure(EntityTypeBuilder<MonedaDeEmpresa> constructor)
    {
        constructor.ToTable("MonedasDeEmpresa");

        constructor.HasKey(m => new { m.EmpresaId, m.Moneda });

        // Sin llave foránea al catálogo, para que una recarga del SAT no choque; el servicio valida la clave.
        constructor.Property(m => m.Moneda).HasMaxLength(3).IsUnicode(false);

        constructor.HasOne(m => m.Empresa)
            .WithMany()
            .HasForeignKey(m => m.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
