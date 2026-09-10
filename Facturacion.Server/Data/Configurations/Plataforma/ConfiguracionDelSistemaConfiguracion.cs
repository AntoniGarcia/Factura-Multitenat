using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ConfiguracionDelSistemaConfiguracion : IEntityTypeConfiguration<ConfiguracionDelSistema>
{
    public void Configure(EntityTypeBuilder<ConfiguracionDelSistema> constructor)
    {
        constructor.ToTable("ConfiguracionDelSistema", tabla =>
            tabla.HasCheckConstraint("CK_ConfiguracionDelSistema_Unica", "[Id] = 1"));

        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.ServidorSmtp).HasMaxLength(253);
        constructor.Property(c => c.UsuarioSmtp).HasMaxLength(254);
        constructor.Property(c => c.ContrasenaSmtpCifrada).HasMaxLength(2048);
        constructor.Property(c => c.RemitenteCorreo).HasMaxLength(254);
        constructor.Property(c => c.RemitenteNombre).HasMaxLength(128);
        constructor.Property(c => c.NombreDelSistema).HasMaxLength(128);
        constructor.Property(c => c.AsuntoVerificacion).HasMaxLength(128);
        constructor.Property(c => c.AsuntoContrasena).HasMaxLength(128);
        constructor.Property(c => c.CuerpoVerificacion).HasMaxLength(8000);
        constructor.Property(c => c.CuerpoContrasena).HasMaxLength(8000);
    }
}
