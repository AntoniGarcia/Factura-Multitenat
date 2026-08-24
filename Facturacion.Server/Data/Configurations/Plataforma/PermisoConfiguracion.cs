using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClavesPermiso = Facturacion.Shared.Comun.Permisos;

namespace Facturacion.Server.Data.Configurations.Plataforma;

/// <summary>
/// Siembra los seis permisos en la migración. Las claves salen de
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

        constructor.HasData(
            new Permiso { Clave = ClavesPermiso.Timbrar, Descripcion = "Emitir y timbrar comprobantes" },
            new Permiso { Clave = ClavesPermiso.Cancelar, Descripcion = "Cancelar comprobantes timbrados" },
            new Permiso { Clave = ClavesPermiso.AdministrarUsuarios, Descripcion = "Dar de alta usuarios y asignar permisos" },
            new Permiso { Clave = ClavesPermiso.ComprarTimbres, Descripcion = "Comprar paquetes de timbres" },
            new Permiso { Clave = ClavesPermiso.VerReportes, Descripcion = "Consultar reportes de la empresa" },
            new Permiso { Clave = ClavesPermiso.ConfigurarEmpresa, Descripcion = "Configurar la empresa, sus series y sus certificados" });
    }
}
