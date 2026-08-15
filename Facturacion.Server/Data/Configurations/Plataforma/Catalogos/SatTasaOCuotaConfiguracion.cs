using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facturacion.Server.Data.Configurations.Plataforma.Catalogos;

public sealed class SatTasaOCuotaConfiguracion : IEntityTypeConfiguration<SatTasaOCuota>
{
    public void Configure(EntityTypeBuilder<SatTasaOCuota> constructor)
    {
        constructor.ToTable("SatTasaOCuota");
        constructor.HasKey(x => x.Clave);
        // Impuesto no es una clave corta de tres letras: el SAT a veces escribe frases
        // completas ahí ("IVA Crédito aplicado del 50%"), se comprobó contra el archivo real.
        constructor.Property(x => x.Clave).HasMaxLength(160);
        constructor.Property(x => x.RangoOFijo).HasMaxLength(16);
        constructor.Property(x => x.Impuesto).HasMaxLength(64);
        constructor.Property(x => x.Factor).HasMaxLength(16);
    }
}
