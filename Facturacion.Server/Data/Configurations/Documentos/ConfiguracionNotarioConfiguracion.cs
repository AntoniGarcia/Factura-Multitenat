using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Documentos;

public sealed class ConfiguracionNotarioConfiguracion : IEntityTypeConfiguration<ConfiguracionNotario>
{
    public void Configure(EntityTypeBuilder<ConfiguracionNotario> configuracion)
    {
        configuracion.ToTable("ConfiguracionesNotario");
        configuracion.HasKey(x => x.EmpresaId);
        configuracion.Property(x => x.Curp).HasMaxLength(18);
        configuracion.Property(x => x.Estado).HasMaxLength(3);
        configuracion.Property(x => x.Adscripcion).HasMaxLength(255);
        configuracion.HasOne<Empresa>().WithOne()
            .HasForeignKey<ConfiguracionNotario>(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
