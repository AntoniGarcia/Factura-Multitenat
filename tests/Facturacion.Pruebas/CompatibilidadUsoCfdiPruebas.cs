using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Pruebas;

/// <summary>
/// La prueba obligatoria de la fase 3 (PROMPT-FASES-A.md): que
/// <see cref="ServicioCatalogosSat.EsUsoCfdiCompatibleAsync"/> acierte en al menos seis
/// combinaciones de régimen y uso, incluyendo 616 + S01 del público en general
/// (CLAUDE.md §7).
/// <para>
/// Los datos que siembra son reales, tomados del archivo del SAT cargado en desarrollo
/// —no inventados—: <c>c_UsoCFDI</c> trae su propia matriz de compatibilidad en la columna
/// "Régimen Fiscal Receptor", y esta prueba comprueba que <see cref="ServicioCatalogosSat"/>
/// la interpreta bien, no que la matriz en sí sea correcta.
/// </para>
/// </summary>
public sealed class CompatibilidadUsoCfdiPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasCatalogos;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private AppDbContext Contexto()
    {
        var tenencia = new ContextoEmpresaFijo();
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(Conexion, tenencia)
            .Options;

        return new AppDbContext(opciones, tenencia);
    }

    public async Task InitializeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        db.SatUsosCfdi.AddRange(
            NuevoUso("S01", "Sin efectos fiscales.", fisica: true, moral: true,
                "601, 603, 605, 606, 608, 610, 611, 612, 614, 616, 620, 621, 622, 623, 624, 607, 615, 625, 626"),
            NuevoUso("G01", "Adquisición de mercancías.", fisica: true, moral: true,
                "601, 603, 606, 612, 620, 621, 622, 623, 624, 625,626"),
            NuevoUso("D01", "Honorarios médicos, dentales y gastos hospitalarios.", fisica: true, moral: false,
                "605, 606, 608, 611, 612, 614, 607, 615, 625"),
            NuevoUso("CN01", "Nómina", fisica: true, moral: false, "605"));

        db.SatRegimenesFiscales.AddRange(
            NuevoRegimen("601", "General de Ley Personas Morales", fisica: false, moral: true),
            NuevoRegimen("605", "Sueldos y Salarios e Ingresos Asimilados a Salarios", fisica: true, moral: false),
            NuevoRegimen("616", "Sin obligaciones fiscales", fisica: true, moral: false));

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }

    [Theory]
    // El caso exigido por CLAUDE.md §7: público en general.
    [InlineData("S01", "616", false, true)]
    [InlineData("G01", "601", true, true)]
    [InlineData("D01", "605", false, true)]
    // 616 no está en la lista de regímenes de G01: incompatible aunque el tipo de persona encaje.
    [InlineData("G01", "616", false, false)]
    // D01 no aplica a personas morales, aunque 605 sí esté en su lista de regímenes.
    [InlineData("D01", "605", true, false)]
    // CN01 solo admite el régimen 605.
    [InlineData("CN01", "616", false, false)]
    public async Task Evalua_la_compatibilidad_correctamente(
        string usoCfdi, string regimenReceptor, bool esPersonaMoral, bool compatibleEsperado)
    {
        await using var db = Contexto();
        var servicio = new ServicioCatalogosSat(db);

        var compatible = await servicio.EsUsoCfdiCompatibleAsync(usoCfdi, regimenReceptor, esPersonaMoral, CancellationToken.None);

        Assert.Equal(compatibleEsperado, compatible);
    }

    private static SatUsoCfdi NuevoUso(string clave, string descripcion, bool fisica, bool moral, string regimenes) => new()
    {
        Clave = clave,
        Descripcion = descripcion,
        AplicaFisica = fisica,
        AplicaMoral = moral,
        RegimenesFiscalesAplicables = regimenes,
        FechaInicioVigencia = new DateOnly(2022, 1, 1),
        Vigente = true
    };

    private static SatRegimenFiscal NuevoRegimen(string clave, string descripcion, bool fisica, bool moral) => new()
    {
        Clave = clave,
        Descripcion = descripcion,
        AplicaFisica = fisica,
        AplicaMoral = moral,
        FechaInicioVigencia = new DateOnly(2022, 1, 1),
        Vigente = true
    };
}
