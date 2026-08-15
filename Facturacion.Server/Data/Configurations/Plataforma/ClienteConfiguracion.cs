using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> constructor)
    {
        constructor.ToTable("Clientes");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Rfc).HasMaxLength(13);
        constructor.Property(c => c.Nombre).HasMaxLength(254);
        constructor.Property(c => c.RegimenFiscal).HasMaxLength(3);
        constructor.Property(c => c.DomicilioFiscalCp).HasMaxLength(5);
        constructor.Property(c => c.ResidenciaFiscal).HasMaxLength(3);
        constructor.Property(c => c.NumRegIdTrib).HasMaxLength(40);

        constructor.Property(c => c.UsoCfdiPreferido).HasMaxLength(4);
        constructor.Property(c => c.MetodoPagoPreferido).HasMaxLength(3);
        constructor.Property(c => c.FormaPagoPreferida).HasMaxLength(4);

        constructor.Property(c => c.Telefono).HasMaxLength(32);
        constructor.Property(c => c.CorreoPrincipal).HasMaxLength(254);

        constructor.Property(c => c.Calle).HasMaxLength(128);
        constructor.Property(c => c.NumeroExterior).HasMaxLength(32);
        constructor.Property(c => c.NumeroInterior).HasMaxLength(32);
        constructor.Property(c => c.Colonia).HasMaxLength(128);
        constructor.Property(c => c.Localidad).HasMaxLength(128);
        constructor.Property(c => c.Referencia).HasMaxLength(256);
        constructor.Property(c => c.Municipio).HasMaxLength(128);
        constructor.Property(c => c.Estado).HasMaxLength(128);
        constructor.Property(c => c.Pais).HasMaxLength(64);
        constructor.Property(c => c.CodigoPostal).HasMaxLength(5);

        constructor.HasOne(c => c.Empresa)
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // La clave que teclean los contadores no se puede repetir dentro de la empresa.
        constructor.HasIndex(c => new { c.EmpresaId, c.ClaveInterna })
            .IsUnique()
            .HasDatabaseName("IX_Clientes_ClaveInternaPorEmpresa");

        // CLAUDE.md §5: el RFC es único POR EMPRESA —dos empresas pueden facturarle al mismo
        // cliente— y los dos genéricos quedan fuera del índice porque, por definición, se
        // repiten: todo el público en general comparte XAXX010101000.
        //
        // El filtro no incluye Activo a propósito: dar de baja a un cliente no libera su RFC.
        // Aquí nada se borra, así que lo correcto es reactivar el registro que ya existe, no
        // crear un segundo cliente con el mismo RFC y perder el historial repartido en dos.
        //
        // Se escribe con dos <> unidos por AND y no con NOT IN: SQL Server solo admite
        // comparaciones simples en el WHERE de un índice filtrado, y NOT IN falla con
        // «Incorrect syntax near 'NOT'». Se descubrió aplicando la migración, no leyéndola.
        constructor.HasIndex(c => new { c.EmpresaId, c.Rfc })
            .IsUnique()
            .HasFilter($"[Rfc] <> '{Rfc.GenericoNacional}' AND [Rfc] <> '{Rfc.GenericoExtranjero}'")
            .HasDatabaseName("IX_Clientes_RfcPorEmpresa");

        // La lista filtra por activos y ordena por nombre; es la consulta más frecuente.
        constructor.HasIndex(c => new { c.EmpresaId, c.Activo, c.Nombre });
    }
}
