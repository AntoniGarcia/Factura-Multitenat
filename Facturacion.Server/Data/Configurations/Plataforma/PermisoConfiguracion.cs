using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClavesPermiso = Facturacion.Shared.Comun.Permisos;

namespace Facturacion.Server.Data.Configurations.Plataforma;

/// <summary>
/// Siembra los permisos asignables en la migración. Las claves salen de
/// <c>Facturacion.Shared.Comun.Permisos</c>, que es la única fuente: si alguien agrega un
/// permiso allá y no aquí, la semilla queda incompleta y se nota de inmediato.
/// </summary>
public sealed class PermisoConfiguracion : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> constructor)
    {
        constructor.ToTable("Permisos");

        constructor.HasKey(p => p.Clave);

        constructor.Property(p => p.Clave).HasMaxLength(32);
        constructor.Property(p => p.Descripcion).HasMaxLength(128);

        // Solo las asignables: «titular» no se guarda, lo pone el token a quien registró la cuenta.
        constructor.HasData(ClavesPermiso.Asignables.Select(clave =>
            new Permiso { Clave = clave, Descripcion = ClavesPermiso.EtiquetaCorta(clave) }));
    }
}
