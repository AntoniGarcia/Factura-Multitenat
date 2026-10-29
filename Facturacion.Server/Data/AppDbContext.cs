using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Data;

/// <summary>Contexto de datos e identidad sin tablas de roles. Las configuraciones de entidades se mantienen en Data/Configurations.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> opciones, IContextoEmpresaInterno contexto)
    : IdentityUserContext<Usuario, Guid>(opciones)
{
    /// <summary>
    /// Empresa que alimenta el filtro global de consulta. Se fija al construir el contexto,
    /// desde el claim de la petición. Es pública porque el filtro la lee como miembro del
    /// contexto: así Entity Framework la reevalúa en cada consulta.
    /// </summary>
    public Guid? EmpresaActual { get; } = contexto.EmpresaActual;

    /// <summary>Cuenta que alimenta el filtro de <see cref="IEntidadDeCuenta"/>, con el mismo mecanismo.</summary>
    public Guid? CuentaActual { get; } = contexto.CuentaActual;

    // Plataforma, identidad y catálogos.
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<UsuarioEmpresa> UsuariosEmpresas => Set<UsuarioEmpresa>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<UsuarioEmpresaPermiso> UsuariosEmpresasPermisos => Set<UsuarioEmpresaPermiso>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AltaPendiente> AltasPendientes => Set<AltaPendiente>();
    public DbSet<OperadorPlataforma> OperadoresPlataforma => Set<OperadorPlataforma>();
    public DbSet<RefreshTokenOperador> RefreshTokensOperador => Set<RefreshTokenOperador>();
    public DbSet<ConfiguracionDelSistema> ConfiguracionesDelSistema => Set<ConfiguracionDelSistema>();
    public DbSet<ClaveIdempotencia> ClavesIdempotencia => Set<ClaveIdempotencia>();
    public DbSet<RegistroBitacora> Bitacora => Set<RegistroBitacora>();
    public DbSet<ConfiguracionEmpresa> ConfiguracionesEmpresa => Set<ConfiguracionEmpresa>();
    public DbSet<CorreoDeEmpresa> CorreosDeEmpresa => Set<CorreoDeEmpresa>();
    public DbSet<CertificadoCsd> CertificadosCsd => Set<CertificadoCsd>();
    public DbSet<Serie> Series => Set<Serie>();
    public DbSet<ReservaFolio> ReservasFolio => Set<ReservaFolio>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImpuesto> ProductosImpuestos => Set<ProductoImpuesto>();
    public DbSet<Paquete> Paquetes => Set<Paquete>();
    public DbSet<Membresia> Membresias => Set<Membresia>();
    public DbSet<BolsaTimbres> BolsasTimbres => Set<BolsaTimbres>();
    public DbSet<MovimientoTimbre> MovimientosTimbre => Set<MovimientoTimbre>();
    public DbSet<ReservaTimbre> ReservasTimbre => Set<ReservaTimbre>();
    public DbSet<CompraTimbres> ComprasTimbres => Set<CompraTimbres>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<FiguraTransporte> FigurasTransporte => Set<FiguraTransporte>();
    public DbSet<TrasladoCartaPorte> TrasladosCartaPorte => Set<TrasladoCartaPorte>();
    public DbSet<UbicacionCartaPorte> UbicacionesCartaPorte => Set<UbicacionCartaPorte>();
    public DbSet<MercanciaCartaPorte> MercanciasCartaPorte => Set<MercanciaCartaPorte>();
    public DbSet<ConfiguracionNotario> ConfiguracionesNotario => Set<ConfiguracionNotario>();
    public DbSet<DatosNotaria> DatosNotaria => Set<DatosNotaria>();
    public DbSet<InmuebleNotarial> InmueblesNotariales => Set<InmuebleNotarial>();
    public DbSet<ParteNotarial> PartesNotariales => Set<ParteNotarial>();
    public DbSet<DatosObra> DatosObra => Set<DatosObra>();
    public DbSet<DatosComercioExterior> DatosComercioExterior => Set<DatosComercioExterior>();

    // ── Catálogos del SAT — sin EmpresaId, fuera del filtro de empresa ───
    public DbSet<CatalogoVersion> CatalogoVersiones => Set<CatalogoVersion>();
    public DbSet<SatFormaPago> SatFormasPago => Set<SatFormaPago>();
    public DbSet<SatExportacion> SatExportaciones => Set<SatExportacion>();
    public DbSet<SatMetodoPago> SatMetodosPago => Set<SatMetodoPago>();
    public DbSet<SatPeriodicidad> SatPeriodicidades => Set<SatPeriodicidad>();
    public DbSet<SatMes> SatMeses => Set<SatMes>();
    public DbSet<SatTipoRelacion> SatTiposRelacion => Set<SatTipoRelacion>();
    public DbSet<SatPais> SatPaises => Set<SatPais>();
    public DbSet<SatObjetoImp> SatObjetosImp => Set<SatObjetoImp>();
    public DbSet<SatMoneda> SatMonedas => Set<SatMoneda>();
    public DbSet<SatTipoDeComprobante> SatTiposDeComprobante => Set<SatTipoDeComprobante>();
    public DbSet<SatRegimenFiscal> SatRegimenesFiscales => Set<SatRegimenFiscal>();
    public DbSet<SatUsoCfdi> SatUsosCfdi => Set<SatUsoCfdi>();
    public DbSet<SatClaveProdServ> SatClavesProdServ => Set<SatClaveProdServ>();
    public DbSet<SatClaveUnidad> SatClavesUnidad => Set<SatClaveUnidad>();
    public DbSet<SatImpuesto> SatImpuestos => Set<SatImpuesto>();
    public DbSet<SatTipoFactor> SatTiposFactor => Set<SatTipoFactor>();
    public DbSet<SatTasaOCuota> SatTasasOCuota => Set<SatTasaOCuota>();
    public DbSet<SatEstado> SatEstados => Set<SatEstado>();
    public DbSet<SatMunicipio> SatMunicipios => Set<SatMunicipio>();
    public DbSet<SatColonia> SatColonias => Set<SatColonia>();
    public DbSet<SatCodigoPostal> SatCodigosPostales => Set<SatCodigoPostal>();
    public DbSet<SatConfiguracionAutotransporte> SatConfiguracionesAutotransporte => Set<SatConfiguracionAutotransporte>();
    public DbSet<SatTipoPermiso> SatTiposPermiso => Set<SatTipoPermiso>();
    public DbSet<SatFiguraTransporte> SatFigurasTransporte => Set<SatFiguraTransporte>();
    public DbSet<SatClaveProdServCartaPorte> SatClavesProdServCartaPorte => Set<SatClaveProdServCartaPorte>();
    public DbSet<SatIncoterm> SatIncoterms => Set<SatIncoterm>();
    public DbSet<SatUnidadAduana> SatUnidadesAduana => Set<SatUnidadAduana>();
    public DbSet<SatFraccionArancelaria> SatFraccionesArancelarias => Set<SatFraccionArancelaria>();

    // Documentos, timbrado y salidas.
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
    public DbSet<Concepto> Conceptos => Set<Concepto>();
    public DbSet<ImpuestoConcepto> ConceptosImpuestos => Set<ImpuestoConcepto>();
    public DbSet<ComprobanteRelacionado> ComprobantesRelacionados => Set<ComprobanteRelacionado>();
    public DbSet<IntentoTimbrado> IntentosTimbrado => Set<IntentoTimbrado>();
    public DbSet<SolicitudCancelacion> SolicitudesCancelacion => Set<SolicitudCancelacion>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<DocumentoPagado> PagosDocumentos => Set<DocumentoPagado>();
    public DbSet<ImpuestoDocumentoPagado> PagosDocumentosImpuestos => Set<ImpuestoDocumentoPagado>();
    public DbSet<EnvioDeCorreo> EnviosDeCorreo => Set<EnvioDeCorreo>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        base.OnModelCreating(constructor);
        constructor.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        constructor.AplicarFiltroDeEmpresa(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructor)
        => constructor.AplicarConvencionesDeFacturacion();
}
