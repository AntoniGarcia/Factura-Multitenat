using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class EmpresaConfiguracion : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> constructor)
    {
        constructor.ToTable("Empresas");

        constructor.HasKey(e => e.Id);

        constructor.Property(e => e.Rfc).HasMaxLength(13);
        constructor.Property(e => e.NombreFiscal).HasMaxLength(254);
        constructor.Property(e => e.RegimenFiscal).HasMaxLength(3);
        constructor.Property(e => e.CodigoPostalExpedicion).HasMaxLength(5);
        constructor.Property(e => e.ZonaHoraria).HasMaxLength(64);

        constructor.Property(e => e.Calle).HasMaxLength(128);
        constructor.Property(e => e.NumeroExterior).HasMaxLength(32);
        constructor.Property(e => e.NumeroInterior).HasMaxLength(32);
        constructor.Property(e => e.Referencia).HasMaxLength(256);
        constructor.Property(e => e.Colonia).HasMaxLength(128);
        constructor.Property(e => e.Localidad).HasMaxLength(128);
        constructor.Property(e => e.Municipio).HasMaxLength(128);
        constructor.Property(e => e.Estado).HasMaxLength(128);
        constructor.Property(e => e.Pais).HasMaxLength(64);
        constructor.Property(e => e.CodigoPostal).HasMaxLength(5);
        constructor.Property(e => e.Telefono).HasMaxLength(32);
        constructor.Property(e => e.CorreoContacto).HasMaxLength(254);

        constructor.Property(e => e.LogoRuta).HasMaxLength(256);
        constructor.Property(e => e.LogoTipoMime).HasMaxLength(64);
        constructor.Property(e => e.LogoNombreOriginal).HasMaxLength(256);

        // El RFC es único dentro de la cuenta, no en toda la base: dos cuentas distintas
        // pueden ser dos despachos que administran la misma empresa.
        constructor.HasIndex(e => new { e.CuentaId, e.Rfc }).IsUnique();

        constructor.HasOne(e => e.Cuenta)
            .WithMany(c => c.Empresas)
            .HasForeignKey(e => e.CuentaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
