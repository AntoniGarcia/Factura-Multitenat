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
        constructor.HasData(
            new Permiso { Clave = ClavesPermiso.VerDocumentos, Descripcion = "Consultar y descargar comprobantes" },
            new Permiso { Clave = ClavesPermiso.EmitirFactura, Descripcion = "Emitir facturas básicas" },
            new Permiso { Clave = ClavesPermiso.EmitirNotaria, Descripcion = "Emitir facturas con complemento de Notarios Públicos" },
            new Permiso { Clave = ClavesPermiso.EmitirCartaPorte, Descripcion = "Emitir traslados con Carta Porte" },
            new Permiso { Clave = ClavesPermiso.EmitirComercioExterior, Descripcion = "Emitir facturas con Comercio Exterior" },
            new Permiso { Clave = ClavesPermiso.EmitirObra, Descripcion = "Emitir facturas de obra" },
            new Permiso { Clave = ClavesPermiso.EmitirPago, Descripcion = "Emitir complementos de pago" },
            new Permiso { Clave = ClavesPermiso.Cancelar, Descripcion = "Cancelar comprobantes timbrados" },
            new Permiso { Clave = ClavesPermiso.EnviarCorreo, Descripcion = "Enviar comprobantes por correo" },
            new Permiso { Clave = ClavesPermiso.AdministrarClientes, Descripcion = "Dar de alta, editar y dar de baja clientes" },
            new Permiso { Clave = ClavesPermiso.AdministrarProductos, Descripcion = "Dar de alta, editar y dar de baja productos" },
            new Permiso { Clave = ClavesPermiso.AdministrarVehiculos, Descripcion = "Dar de alta, editar y dar de baja vehículos" },
            new Permiso { Clave = ClavesPermiso.AdministrarFiguras, Descripcion = "Dar de alta, editar y dar de baja figuras de transporte" },
            new Permiso { Clave = ClavesPermiso.ComprarTimbres, Descripcion = "Consultar el saldo y comprar paquetes de timbres" },
            new Permiso { Clave = ClavesPermiso.MiEmpresa, Descripcion = "Editar los datos fiscales y los certificados de la empresa" },
            new Permiso { Clave = ClavesPermiso.Configuracion, Descripcion = "Configurar correo, impuestos, logo, Notaría y series de la empresa" });
    }
}
