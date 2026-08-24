using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> constructor)
    {
        constructor.ToTable("Productos");

        constructor.HasKey(p => p.Id);

        constructor.Property(p => p.ClaveProdServ).HasMaxLength(8);
        constructor.Property(p => p.ClaveUnidad).HasMaxLength(20);
        constructor.Property(p => p.UnidadTexto).HasMaxLength(64);
        constructor.Property(p => p.Descripcion).HasMaxLength(1000);
        constructor.Property(p => p.ObjetoImp).HasMaxLength(2);

        constructor.HasOne(p => p.Empresa)
            .WithMany()
            .HasForeignKey(p => p.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Los impuestos son parte del producto, no una entidad con vida propia: al reemplazar
        // la configuración fiscal se borran los renglones viejos y se escriben los nuevos.
        // Es la única excepción razonable a «nada se borra» de ARQUITECTURA.md §5: no son datos
        // históricos —el histórico vive congelado dentro del comprobante— sino la
        // configuración actual del producto.
        constructor.HasMany(p => p.Impuestos)
            .WithOne(i => i.Producto)
            .HasForeignKey(i => i.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasIndex(p => new { p.EmpresaId, p.CodigoInterno })
            .IsUnique()
            .HasDatabaseName("IX_Productos_CodigoInternoPorEmpresa");

        // La lista filtra por activos y ordena por descripción; es la consulta más frecuente.
        constructor.HasIndex(p => new { p.EmpresaId, p.Activo, p.Descripcion });
    }
}

public sealed class ProductoImpuestoConfiguracion : IEntityTypeConfiguration<ProductoImpuesto>
{
    public void Configure(EntityTypeBuilder<ProductoImpuesto> constructor)
    {
        constructor.ToTable("ProductosImpuestos");

        constructor.HasKey(i => i.Id);

        constructor.Property(i => i.Impuesto).HasMaxLength(3);
        constructor.Property(i => i.TipoFactor).HasMaxLength(16);

        // El mismo impuesto no se puede configurar dos veces en el mismo sentido: un
        // concepto con dos traslados de IVA produce un comprobante que el SAT rechaza.
        constructor.HasIndex(i => new { i.ProductoId, i.Impuesto, i.EsRetencion })
            .IsUnique()
            .HasDatabaseName("IX_ProductosImpuestos_SinImpuestoRepetido");
    }
}
