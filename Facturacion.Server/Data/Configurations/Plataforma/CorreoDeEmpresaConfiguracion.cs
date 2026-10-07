using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class CorreoDeEmpresaConfiguracion : IEntityTypeConfiguration<CorreoDeEmpresa>
{
    public void Configure(EntityTypeBuilder<CorreoDeEmpresa> constructor)
    {
        constructor.ToTable("CorreosDeEmpresa");

        constructor.HasKey(c => c.EmpresaId);

        constructor.HasOne(c => c.Empresa)
            .WithOne()
            .HasForeignKey<CorreoDeEmpresa>(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        constructor.Property(c => c.Servidor).HasMaxLength(253);
        constructor.Property(c => c.Usuario).HasMaxLength(254);
        constructor.Property(c => c.ContrasenaCifrada).HasMaxLength(2048);
        constructor.Property(c => c.RemitenteNombre).HasMaxLength(128);
        constructor.Property(c => c.RemitenteCorreo).HasMaxLength(254);
        constructor.Property(c => c.NombreRemitenteSistema).HasMaxLength(128);
        constructor.Property(c => c.ResponderA).HasMaxLength(254);
    }
}
