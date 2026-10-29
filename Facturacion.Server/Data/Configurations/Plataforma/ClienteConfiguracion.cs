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

        constructor.HasOne(c => c.Cuenta)
            .WithMany()
            .HasForeignKey(c => c.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);

        // La clave que teclean los contadores no se puede repetir dentro de la cuenta.
        constructor.HasIndex(c => new { c.CuentaId, c.ClaveInterna })
            .IsUnique()
            .HasDatabaseName("IX_Clientes_ClaveInternaPorCuenta");

        // El RFC es único por cuenta, salvo los genéricos. Desactivar un cliente no libera su RFC: debe reactivarse el registro existente. El índice filtrado usa comparaciones simples compatibles con SQL Server.
        constructor.HasIndex(c => new { c.CuentaId, c.Rfc })
            .IsUnique()
            .HasFilter($"[Rfc] <> '{Rfc.GenericoNacional}' AND [Rfc] <> '{Rfc.GenericoExtranjero}'")
            .HasDatabaseName("IX_Clientes_RfcPorCuenta");

        // La lista filtra por activos y ordena por nombre; es la consulta más frecuente.
        constructor.HasIndex(c => new { c.CuentaId, c.Activo, c.Nombre });

        constructor.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(c => c.EmpresaAltaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
