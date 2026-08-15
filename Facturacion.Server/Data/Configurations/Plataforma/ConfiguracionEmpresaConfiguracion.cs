using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ConfiguracionEmpresaConfiguracion : IEntityTypeConfiguration<ConfiguracionEmpresa>
{
    public void Configure(EntityTypeBuilder<ConfiguracionEmpresa> constructor)
    {
        constructor.ToTable("ConfiguracionesEmpresa");

        // La llave primaria es la empresa: una configuración por empresa, sin poder duplicarla.
        constructor.HasKey(c => c.EmpresaId);

        constructor.HasOne(c => c.Empresa)
            .WithOne()
            .HasForeignKey<ConfiguracionEmpresa>(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
