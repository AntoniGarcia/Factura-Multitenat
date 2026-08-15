using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma;

public sealed class CertificadoCsdConfiguracion : IEntityTypeConfiguration<CertificadoCsd>
{
    public void Configure(EntityTypeBuilder<CertificadoCsd> constructor)
    {
        constructor.ToTable("CertificadosCsd");

        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.NumeroSerie).HasMaxLength(20);
        constructor.Property(c => c.RutaCer).HasMaxLength(256);
        constructor.Property(c => c.RutaKey).HasMaxLength(256);

        // El texto cifrado de Data Protection crece bastante respecto al claro; 1024 sobra
        // para una contraseña de CSD y evita tener que migrar la columna después.
        constructor.Property(c => c.ContrasenaCifrada).HasMaxLength(1024);

        constructor.HasOne(c => c.Empresa)
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un solo certificado activo por empresa, impuesto por la base y no por el código:
        // dos certificados activos harían que el sellado dependiera del orden de la consulta.
        constructor.HasIndex(c => c.EmpresaId)
            .HasFilter("[Activo] = 1")
            .IsUnique()
            .HasDatabaseName("IX_CertificadosCsd_UnoActivoPorEmpresa");
    }
}
