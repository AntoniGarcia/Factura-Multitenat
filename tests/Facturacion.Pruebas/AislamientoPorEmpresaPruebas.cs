using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Pruebas;

/// <summary>
/// El aislamiento por empresa es la propiedad que sostiene todo el sistema: si falla, una
/// empresa ve los datos de otra. Se prueba contra SQL Server y no contra un proveedor en
/// memoria, porque lo que hay que comprobar es el SQL que se genera de verdad.
/// <para>
/// Comprueba además el riesgo concreto de esta implementación: el filtro lee un miembro del
/// <c>DbContext</c>, y el modelo de Entity Framework se cachea. Si el valor quedara fijado
/// en el modelo compilado, el segundo contexto vería los renglones del primero.
/// </para>
/// <para>
/// Necesita SQL Server en la instancia por defecto. Crea y destruye su propia base.
/// </para>
/// </summary>
public sealed class AislamientoPorEmpresaPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebas;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _llantera = Guid.NewGuid();
    private readonly Guid _cementera = Guid.NewGuid();

    private static AppDbContext Contexto(Guid? empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(Conexion, tenencia)
            .Options;

        return new AppDbContext(opciones, tenencia);
    }

    public async Task InitializeAsync()
    {
        await using var sinEmpresa = Contexto(null);
        await sinEmpresa.Database.EnsureDeletedAsync();
        await sinEmpresa.Database.MigrateAsync();

        sinEmpresa.Cuentas.Add(new Cuenta
        {
            Id = _cuenta,
            Nombre = "Grupo de prueba",
            CorreoContacto = "pruebas@ejemplo.mx",
            FechaAltaUtc = DateTime.UtcNow
        });

        sinEmpresa.Empresas.AddRange(
            NuevaEmpresa(_llantera, "Llantera", "LLA010101AAA"),
            NuevaEmpresa(_cementera, "Cementera", "CEM010101AAA"));

        await sinEmpresa.SaveChangesAsync();

        await AgregarClave(_llantera, "clave-de-la-llantera");
        await AgregarClave(_cementera, "clave-de-la-cementera");
    }

    public async Task DisposeAsync()
    {
        await using var sinEmpresa = Contexto(null);
        await sinEmpresa.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Una_empresa_solo_ve_sus_propios_renglones()
    {
        await using var llantera = Contexto(_llantera);

        var claves = await llantera.ClavesIdempotencia.ToListAsync();

        var clave = Assert.Single(claves);
        Assert.Equal("clave-de-la-llantera", clave.Clave);
        Assert.Equal(_llantera, clave.EmpresaId);
    }

    [Fact]
    public async Task Cambiar_de_empresa_cambia_lo_que_se_ve()
    {
        // Dos contextos vivos a la vez: si el filtro se hubiera quedado fijado en el modelo
        // compilado, el segundo devolvería lo mismo que el primero.
        await using var llantera = Contexto(_llantera);
        await using var cementera = Contexto(_cementera);

        var deLaLlantera = await llantera.ClavesIdempotencia.SingleAsync();
        var deLaCementera = await cementera.ClavesIdempotencia.SingleAsync();

        Assert.Equal(_llantera, deLaLlantera.EmpresaId);
        Assert.Equal(_cementera, deLaCementera.EmpresaId);
    }

    [Fact]
    public async Task Sin_empresa_activa_no_se_ve_ningun_renglon()
    {
        // Falla cerrado: ante un error de configuración se ve de menos, nunca de más.
        await using var sinEmpresa = Contexto(null);

        Assert.Empty(await sinEmpresa.ClavesIdempotencia.ToListAsync());
    }

    [Fact]
    public async Task No_se_puede_dar_de_alta_un_renglon_sin_empresa_activa()
    {
        await using var sinEmpresa = Contexto(null);
        sinEmpresa.ClavesIdempotencia.Add(NuevaClave("clave-huerfana"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sinEmpresa.SaveChangesAsync());
    }

    [Fact]
    public async Task No_se_puede_dar_de_alta_un_renglon_en_una_empresa_ajena()
    {
        await using var llantera = Contexto(_llantera);

        var ajena = NuevaClave("clave-ajena");
        ajena.EmpresaId = _cementera;
        llantera.ClavesIdempotencia.Add(ajena);

        await Assert.ThrowsAsync<InvalidOperationException>(() => llantera.SaveChangesAsync());
    }

    private async Task AgregarClave(Guid empresa, string clave)
    {
        await using var contexto = Contexto(empresa);
        contexto.ClavesIdempotencia.Add(NuevaClave(clave));
        await contexto.SaveChangesAsync();
    }

    private static ClaveIdempotencia NuevaClave(string clave) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = Guid.NewGuid(),
        Clave = clave,
        Endpoint = "/api/timbres/comprar",
        HashPeticion = "sin-cuerpo",
        CodigoEstado = 200,
        RespuestaJson = "{}",
        CreadoUtc = DateTime.UtcNow,
        ExpiraUtc = DateTime.UtcNow.AddHours(24)
    };

    private Empresa NuevaEmpresa(Guid id, string nombre, string rfc) => new()
    {
        Id = id,
        CuentaId = _cuenta,
        Rfc = rfc,
        NombreFiscal = nombre,
        RegimenFiscal = "601",
        CodigoPostalExpedicion = "42000",
        ZonaHoraria = "Central Standard Time (Mexico)",
        FechaAltaUtc = DateTime.UtcNow
    };
}
