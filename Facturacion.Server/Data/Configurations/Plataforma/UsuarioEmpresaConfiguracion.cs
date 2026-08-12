using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class UsuarioEmpresaConfiguracion : IEntityTypeConfiguration<UsuarioEmpresa>
{
    public void Configure(EntityTypeBuilder<UsuarioEmpresa> constructor)
    {
        constructor.ToTable("UsuariosEmpresas");

        constructor.HasKey(ue => new { ue.UsuarioId, ue.EmpresaId });

        // Restrict en todas las llaves: en este sistema nada se borra físicamente
        // (CLAUDE.md §5), así que un borrado en cascada solo puede ser un accidente.
        constructor.HasOne(ue => ue.Usuario)
            .WithMany(u => u.Empresas)
            .HasForeignKey(ue => ue.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        constructor.HasOne(ue => ue.Empresa)
            .WithMany(e => e.Usuarios)
            .HasForeignKey(ue => ue.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        constructor.HasIndex(ue => ue.EmpresaId);
    }
}
