using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class UsuarioEmpresaPermisoConfiguracion : IEntityTypeConfiguration<UsuarioEmpresaPermiso>
{
    public void Configure(EntityTypeBuilder<UsuarioEmpresaPermiso> constructor)
    {
        constructor.ToTable("UsuariosEmpresasPermisos");

        constructor.HasKey(p => new { p.UsuarioId, p.EmpresaId, p.PermisoClave });

        constructor.Property(p => p.PermisoClave).HasMaxLength(32);

        constructor.HasOne(p => p.UsuarioEmpresa)
            .WithMany(ue => ue.Permisos)
            .HasForeignKey(p => new { p.UsuarioId, p.EmpresaId })
            .OnDelete(DeleteBehavior.Restrict);

        // La llave foránea al catálogo es lo que impide guardar un permiso inventado:
        // la validación no puede depender solo del código de la aplicación.
        constructor.HasOne(p => p.Permiso)
            .WithMany()
            .HasForeignKey(p => p.PermisoClave)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
