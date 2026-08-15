using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class PaqueteConfiguracion : IEntityTypeConfiguration<Paquete>
{
    public void Configure(EntityTypeBuilder<Paquete> constructor)
    {
        constructor.ToTable("Paquetes");

        constructor.HasKey(p => p.Id);

        constructor.Property(p => p.Nombre).HasMaxLength(100);

        // Los tres importes con la precisión de CLAUDE.md §5. El precio por timbre la
        // necesita de verdad: 1800.00 entre 1000 timbres da 1.80 exacto, pero un paquete de
        // 750 a 1350.00 da 1.8 periódico y con dos decimales se pierde.
        constructor.Property(p => p.PrecioPorTimbre).HasPrecision(18, 6);
        constructor.Property(p => p.PrecioTotal).HasPrecision(18, 6);

        constructor.HasIndex(p => new { p.Activo, p.Orden });

        // Los paquetes son catálogo del producto, no datos de un inquilino: van en la
        // migración igual que los permisos, para que existan en cualquier entorno sin
        // depender de un sembrado de desarrollo. Los identificadores son fijos y escritos a
        // mano a propósito: si fueran generados, cada migración crearía paquetes nuevos y
        // las compras viejas quedarían apuntando a un catálogo que ya no existe.
        constructor.HasData(
            new Paquete
            {
                Id = new Guid("9c1f0a10-0000-4000-8000-000000000001"),
                Nombre = "500 timbres",
                CantidadTimbres = 500,
                PrecioPorTimbre = 2.00m,
                PrecioTotal = 1000.00m,
                VigenciaMeses = 12,
                Activo = true,
                Orden = 1
            },
            new Paquete
            {
                Id = new Guid("9c1f0a10-0000-4000-8000-000000000002"),
                Nombre = "1000 timbres",
                CantidadTimbres = 1000,
                PrecioPorTimbre = 1.80m,
                PrecioTotal = 1800.00m,
                VigenciaMeses = 12,
                Activo = true,
                Orden = 2
            });
    }
}

public sealed class MembresiaConfiguracion : IEntityTypeConfiguration<Membresia>
{
    public void Configure(EntityTypeBuilder<Membresia> constructor)
    {
        constructor.ToTable("Membresias");

        constructor.HasKey(m => m.Id);

        constructor.Property(m => m.Estado).HasMaxLength(20);

        constructor.HasOne(m => m.Cuenta)
            .WithMany()
            .HasForeignKey(m => m.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una cuenta puede tener varias membresías a lo largo del tiempo —la del año pasado
        // vencida y la de este año activa—, así que el índice no es único. Se ordena por
        // fecha de fin para poder tomar la vigente sin recorrer el histórico.
        constructor.HasIndex(m => new { m.CuentaId, m.FinUtc });
    }
}

public sealed class BolsaTimbresConfiguracion : IEntityTypeConfiguration<BolsaTimbres>
{
    public void Configure(EntityTypeBuilder<BolsaTimbres> constructor)
    {
        constructor.ToTable("BolsasTimbres");

        constructor.HasKey(b => b.Id);

        constructor.HasOne(b => b.Empresa)
            .WithMany()
            .HasForeignKey(b => b.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Exactamente una bolsa por empresa. Sin esto, una carrera al crear la bolsa la
        // primera vez dejaría dos renglones y el saldo se partiría en dos.
        constructor.HasIndex(b => b.EmpresaId).IsUnique();
    }
}

public sealed class MovimientoTimbreConfiguracion : IEntityTypeConfiguration<MovimientoTimbre>
{
    public void Configure(EntityTypeBuilder<MovimientoTimbre> constructor)
    {
        constructor.ToTable("MovimientosTimbre");

        constructor.HasKey(m => m.Id);

        constructor.Property(m => m.Tipo).HasMaxLength(20);
        constructor.Property(m => m.Motivo).HasMaxLength(300);

        // El historial siempre se lee de lo más nuevo a lo más viejo dentro de una empresa.
        constructor.HasIndex(m => new { m.EmpresaId, m.MomentoUtc });
    }
}

public sealed class ReservaTimbreConfiguracion : IEntityTypeConfiguration<ReservaTimbre>
{
    public void Configure(EntityTypeBuilder<ReservaTimbre> constructor)
    {
        constructor.ToTable("ReservasTimbre");

        constructor.HasKey(r => r.Id);

        constructor.Property(r => r.Estado).HasMaxLength(20);
        constructor.Property(r => r.MotivoResolucion).HasMaxLength(300);

        // Índice filtrado a las que siguen apartadas: es exactamente lo que barre el proceso
        // de reservas abandonadas cada pocos minutos, y son un puñado frente al histórico
        // completo de reservas ya resueltas.
        constructor.HasIndex(r => r.CreadaUtc)
            .HasFilter($"[Estado] = '{EstadosDeReservaTimbre.Reservado}'");

        constructor.HasIndex(r => new { r.EmpresaId, r.ComprobanteId });
    }
}

public sealed class CompraTimbresConfiguracion : IEntityTypeConfiguration<CompraTimbres>
{
    public void Configure(EntityTypeBuilder<CompraTimbres> constructor)
    {
        constructor.ToTable("ComprasTimbres");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.NombrePaquete).HasMaxLength(100);
        constructor.Property(c => c.Estado).HasMaxLength(20);
        constructor.Property(c => c.PrecioPorTimbre).HasPrecision(18, 6);
        constructor.Property(c => c.PrecioTotal).HasPrecision(18, 6);

        // Sin llave foránea al paquete: el paquete es catálogo vivo y la compra ya copió lo
        // que le importa. Una foránea impediría retirar un paquete de la venta.
        constructor.HasIndex(c => new { c.EmpresaId, c.CreadaUtc });

        // Las pendientes de acreditar son las que el operador busca; son pocas frente al
        // histórico de compras ya pagadas.
        constructor.HasIndex(c => c.Estado)
            .HasFilter($"[Estado] = '{EstadosDeCompra.PendienteDePago}'");
    }
}
