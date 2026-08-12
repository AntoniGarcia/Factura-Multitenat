using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class CuentaConfiguracion : IEntityTypeConfiguration<Cuenta>
{
    public void Configure(EntityTypeBuilder<Cuenta> constructor)
    {
        constructor.ToTable("Cuentas");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Nombre).HasMaxLength(254);
        constructor.Property(c => c.CorreoContacto).HasMaxLength(254);
    }
}
