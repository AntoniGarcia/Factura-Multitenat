using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Data;

/// <summary>
/// Contexto único de la aplicación. Es uno de los cuatro archivos compartidos entre las dos
/// mitades (REPARTO-EQUIPO.md §5), así que aquí solo van los <c>DbSet</c> y las dos líneas
/// que aplican configuraciones y convenciones.
/// <para>
/// <b>Nunca Fluent API en este archivo.</b> Cada entidad lleva su
/// <c>IEntityTypeConfiguration</c> en <c>Data/Configurations/</c>. Así un conflicto de Git
/// entre las dos mitades se reduce a líneas contiguas de <c>DbSet</c>.
/// </para>
/// <para>
/// Deriva de <see cref="IdentityUserContext{TUser, TKey}"/> y no de <c>IdentityDbContext</c>:
/// esa otra clase base trae las tablas de roles, y CLAUDE.md §4 prohíbe autorizar por rol.
/// Al no existir como tablas, la prohibición la impone el esquema y no la buena voluntad.
/// </para>
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> opciones, IContextoEmpresaInterno contexto)
    : IdentityUserContext<Usuario, Guid>(opciones)
{
    /// <summary>
    /// Empresa que alimenta el filtro global de consulta. Se fija al construir el contexto,
    /// desde el claim de la petición. Es pública porque el filtro la lee como miembro del
    /// contexto: así Entity Framework la reevalúa en cada consulta.
    /// </summary>
    public Guid? EmpresaActual { get; } = contexto.EmpresaActual;

    // ── DbSet de la mitad A — plataforma, identidad y catálogos ─────────────────────────
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<UsuarioEmpresa> UsuariosEmpresas => Set<UsuarioEmpresa>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<UsuarioEmpresaPermiso> UsuariosEmpresasPermisos => Set<UsuarioEmpresaPermiso>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ClaveIdempotencia> ClavesIdempotencia => Set<ClaveIdempotencia>();
    public DbSet<RegistroBitacora> Bitacora => Set<RegistroBitacora>();

    // ── DbSet de la mitad B — documentos, timbrado y salidas ────────────────────────────
    // (los agrega el módulo de documentos)

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        base.OnModelCreating(constructor);
        constructor.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        constructor.AplicarFiltroDeEmpresa(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructor)
        => constructor.AplicarConvencionesDeFacturacion();
}
