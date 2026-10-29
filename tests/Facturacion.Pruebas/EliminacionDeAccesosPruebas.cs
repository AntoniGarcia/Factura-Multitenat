using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Server.Modules.Plataforma.Usuarios;
using Facturacion.Server.Modules.Plataforma.Clientes;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

public sealed class EliminacionDeAccesosPruebas : IAsyncLifetime
{
    private readonly string _nombreBase = "FacturacionPruebasEliminar_" + Guid.NewGuid().ToString("N");
    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Guid _titular = Guid.NewGuid();
    private const string Contrasena = "PruebaSegura123";

    private AppDbContext Abrir()
    {
        var contexto = new ContextoEmpresaFijo(_empresa, _cuenta, _titular);
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(
            $"Server=.;Database={_nombreBase};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False", contexto).Options;
        return new(opciones, contexto);
    }

    public async Task InitializeAsync()
    {
        await using var db = Abrir();
        await db.Database.EnsureCreatedAsync();
        db.Cuentas.Add(new Cuenta { Id = _cuenta, Nombre = "Prueba", CorreoContacto = "prueba@example.com" });
        db.Empresas.Add(Empresa(_empresa));
        db.Users.Add(Usuario(_titular, true));
        db.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = _titular, EmpresaId = _empresa });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        // Cada ejecución elimina únicamente su base temporal, nunca la del proyecto.
        if (!_nombreBase.StartsWith("FacturacionPruebasEliminar_", StringComparison.Ordinal))
            throw new InvalidOperationException("Base de prueba inesperada.");
        await using var db = Abrir();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Usuario_exige_contrasena_aislamiento_y_ausencia_de_historial()
    {
        await using var db = Abrir();
        var contexto = new ContextoEmpresaFijo(_empresa, _cuenta, _titular);
        var bitacora = new ServicioDeBitacora(db, contexto, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServiceCollection().AddLogging().AddSingleton(db);
        servicios.AddIdentityCore<Usuario>().AddEntityFrameworkStores<AppDbContext>();
        using var proveedor = servicios.BuildServiceProvider();
        var servicio = new ServicioDeUsuarios(db, proveedor.GetRequiredService<UserManager<Usuario>>(), bitacora, contexto,
            new ServicioDeRefreshTokens(db, bitacora, NullLogger<ServicioDeRefreshTokens>.Instance),
            Options.Create(new OpcionesDeSoporte()), new ControlDeIntentos(memoria));

        var id = Guid.NewGuid();
        db.Users.Add(Usuario(id));
        db.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = id, EmpresaId = _empresa });
        db.UsuariosEmpresasPermisos.Add(new UsuarioEmpresaPermiso { UsuarioId = id, EmpresaId = _empresa,
            PermisoClave = Facturacion.Shared.Comun.Permisos.DocumentosListado, OtorgadoPorUsuarioId = _titular });
        bitacora.Registrar(EntidadesDeBitacora.UsuarioEmpresa, id.ToString(), AccionesDeBitacora.UsuarioCreado, usuarioId: id);
        await db.SaveChangesAsync();
        Assert.Equal("contrasena-incorrecta", (await servicio.EliminarAsync(id, "incorrecta", default)).Error?.Codigo);
        Assert.True(await db.Users.AnyAsync(x => x.Id == id));
        Assert.Equal("usuario-protegido", (await servicio.EliminarAsync(_titular, Contrasena, default)).Error?.Codigo);
        Assert.Equal("usuario-no-encontrado", (await servicio.EliminarAsync(Guid.NewGuid(), Contrasena, default)).Error?.Codigo);

        var otraEmpresa = Guid.NewGuid();
        db.Empresas.Add(Empresa(otraEmpresa));
        await db.SaveChangesAsync();
        await using (var otroDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Configurar(
            db.Database.GetConnectionString(), new ContextoEmpresaFijo(otraEmpresa)).Options, new ContextoEmpresaFijo(otraEmpresa)))
        {
            otroDb.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = id, EmpresaId = otraEmpresa });
            await otroDb.SaveChangesAsync();
        }
        var otraCuenta = Guid.NewGuid();
        var ajeno = Usuario(Guid.NewGuid());
        ajeno.CuentaId = otraCuenta;
        db.Cuentas.Add(new Cuenta { Id = otraCuenta, Nombre = "Ajena", CorreoContacto = "ajena@example.com" });
        db.Users.Add(ajeno);
        await db.SaveChangesAsync();
        Assert.Equal("usuario-no-encontrado", (await servicio.EliminarAsync(ajeno.Id, Contrasena, default)).Error?.Codigo);

        var historico = Guid.NewGuid();
        db.Users.Add(Usuario(historico));
        db.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = historico, EmpresaId = _empresa });
        bitacora.Registrar(EntidadesDeBitacora.Cliente, Guid.NewGuid().ToString(), AccionesDeBitacora.ClienteCreado, usuarioId: historico);
        await db.SaveChangesAsync();
        Assert.Equal("usuario-con-historial", (await servicio.EliminarAsync(historico, Contrasena, default)).Error?.Codigo);
        Assert.True((await servicio.EliminarAsync(id, Contrasena, default)).EsExito);
        Assert.False(await db.Users.AnyAsync(x => x.Id == id));
        Assert.False(await db.UsuariosEmpresas.AnyAsync(x => x.UsuarioId == id));
        Assert.False(await db.UsuariosEmpresasPermisos.AnyAsync(x => x.UsuarioId == id));
        Assert.True(await db.Bitacora.AnyAsync(x => x.EntidadId == id.ToString() && x.Accion == AccionesDeBitacora.UsuarioCreado));
        Assert.True(await db.Bitacora.AnyAsync(x => x.EntidadId == id.ToString() && x.Accion == AccionesDeBitacora.UsuarioEliminado && x.UsuarioId == _titular));
    }

    [Fact]
    public async Task Operador_no_elimina_principal_a_si_mismo_ni_historial()
    {
        await using var db = Abrir();
        var principal = Operador(true);
        var nuevo = Operador();
        var historico = Operador();
        db.OperadoresPlataforma.AddRange(principal, nuevo, historico);
        await db.SaveChangesAsync();
        var bitacora = new ServicioDeBitacora(db, new ContextoEmpresaFijo(), new ContextoDeOperadorFijo(principal.Id), new HttpContextAccessor());
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var servicio = new ServicioDeOperadores(db, new PasswordHasher<OperadorPlataforma>(),
            new ServicioDeRefreshTokensDeOperador(db, bitacora, NullLogger<ServicioDeRefreshTokensDeOperador>.Instance),
            bitacora, new ControlDeIntentos(memoria));
        Assert.Equal("contrasena-incorrecta", (await servicio.EliminarAsync(principal.Id, nuevo.Id, "incorrecta", default)).Error?.Codigo);
        Assert.Equal("operador-protegido", (await servicio.EliminarAsync(principal.Id, principal.Id, Contrasena, default)).Error?.Codigo);
        Assert.Equal("operador-protegido", (await servicio.EliminarAsync(nuevo.Id, principal.Id, Contrasena, default)).Error?.Codigo);
        Assert.Equal("operador-protegido", (await servicio.EliminarAsync(nuevo.Id, nuevo.Id, Contrasena, default)).Error?.Codigo);
        bitacora.Registrar(EntidadesDeBitacora.OperadorPlataforma, nuevo.Id.ToString(), AccionesDeBitacora.OperadorActualizado, operadorId: historico.Id);
        await db.SaveChangesAsync();
        Assert.Equal("operador-con-historial", (await servicio.EliminarAsync(principal.Id, historico.Id, Contrasena, default)).Error?.Codigo);
        Assert.True((await servicio.EliminarAsync(principal.Id, nuevo.Id, Contrasena, default)).EsExito);
        Assert.False(await db.OperadoresPlataforma.AnyAsync(x => x.Id == nuevo.Id));
        Assert.True(await db.Bitacora.IgnoreQueryFilters().AnyAsync(x => x.EntidadId == nuevo.Id.ToString() && x.Accion == AccionesDeBitacora.OperadorEliminado));
    }

    [Fact]
    public async Task Cliente_bloquea_documentos_de_cualquier_empresa_traslados_y_otra_cuenta()
    {
        await using var db = Abrir();
        var contexto = new ContextoEmpresaFijo(_empresa, _cuenta, _titular);
        var bitacora = new ServicioDeBitacora(db, contexto, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());
        var servicio = new ServicioDeClientes(db, new ValidadorDeCliente(db, new ServicioCatalogosSat(db)), bitacora,
            contexto, new UsoDeCatalogos(db));
        var libre = Cliente(1);
        var conFactura = Cliente(2);
        var conTraslado = Cliente(3);
        var usadoEnOtraEmpresa = Cliente(4);
        db.Clientes.AddRange(libre, conFactura, conTraslado, usadoEnOtraEmpresa);
        db.Comprobantes.Add(Comprobante(conFactura.Id));
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Clave = "1", Descripcion = "Prueba", ConfiguracionAutotransporte = "C2",
            Placa = "ABC123", Aseguradora = "Prueba", Poliza = "1", TipoPermiso = "TPAF01", NumeroPermiso = "1" };
        var figura = new FiguraTransporte { Id = Guid.NewGuid(), Clave = "1", TipoFigura = "01", Rfc = "XAXX010101000",
            Nombre = "Prueba", Calle = "Prueba", NumeroExterior = "1", Estado = "HID", Municipio = "048", CodigoPostal = "42000" };
        db.Vehiculos.Add(vehiculo);
        db.FigurasTransporte.Add(figura);
        db.TrasladosCartaPorte.Add(new TrasladoCartaPorte { Id = Guid.NewGuid(), Comprobante = Comprobante(null),
            VehiculoId = vehiculo.Id, FiguraTransporteId = figura.Id, ClienteDestinoId = conTraslado.Id });
        bitacora.Registrar(EntidadesDeBitacora.Cliente, libre.Id.ToString(), AccionesDeBitacora.ClienteCreado);
        await db.SaveChangesAsync();
        Assert.Equal("catalogo-en-uso", (await servicio.EliminarAsync(conFactura.Id, default)).Error?.Codigo);
        Assert.Equal("catalogo-en-uso", (await servicio.EliminarAsync(conTraslado.Id, default)).Error?.Codigo);

        // Los clientes son de la cuenta: una factura de otra empresa de la misma cuenta también lo usa.
        var otraEmpresa = Guid.NewGuid();
        db.Empresas.Add(Empresa(otraEmpresa));
        await db.SaveChangesAsync();
        var deOtraEmpresa = new ContextoEmpresaFijo(otraEmpresa, _cuenta);
        await using (var otroDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Configurar(
            db.Database.GetConnectionString(), deOtraEmpresa).Options, deOtraEmpresa))
        {
            otroDb.Comprobantes.Add(Comprobante(usadoEnOtraEmpresa.Id));
            await otroDb.SaveChangesAsync();
        }
        Assert.Equal("catalogo-en-uso", (await servicio.EliminarAsync(usadoEnOtraEmpresa.Id, default)).Error?.Codigo);

        // Un cliente de otra cuenta ni siquiera se encuentra.
        var otraCuenta = Guid.NewGuid();
        var empresaAjena = Guid.NewGuid();
        db.Cuentas.Add(new Cuenta { Id = otraCuenta, Nombre = "Ajena", CorreoContacto = "ajena@example.com" });
        var empresa = Empresa(empresaAjena);
        empresa.CuentaId = otraCuenta;
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        var ajeno = Cliente(1);
        var deOtraCuenta = new ContextoEmpresaFijo(empresaAjena, otraCuenta);
        await using (var otroDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Configurar(
            db.Database.GetConnectionString(), deOtraCuenta).Options, deOtraCuenta))
        {
            otroDb.Clientes.Add(ajeno);
            await otroDb.SaveChangesAsync();
        }
        Assert.Equal("cliente-no-encontrado", (await servicio.EliminarAsync(ajeno.Id, default)).Error?.Codigo);
        Assert.True(await db.Clientes.IgnoreQueryFilters().AnyAsync(x => x.Id == ajeno.Id));
        Assert.True((await servicio.EliminarAsync(libre.Id, default)).EsExito);
        Assert.False(await db.Clientes.AnyAsync(x => x.Id == libre.Id));
        Assert.True(await db.Bitacora.AnyAsync(x => x.EntidadId == libre.Id.ToString() && x.Accion == AccionesDeBitacora.ClienteCreado));
        Assert.True(await db.Bitacora.AnyAsync(x => x.EntidadId == libre.Id.ToString() && x.Accion == AccionesDeBitacora.ClienteEliminado));
    }

    [Fact]
    public async Task Eliminar_cliente_espera_el_guardado_en_curso_y_no_deja_documento_huerfano()
    {
        await using var db = Abrir();
        var contexto = new ContextoEmpresaFijo(_empresa, _cuenta, _titular);
        var bitacora = new ServicioDeBitacora(db, contexto, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());
        var servicio = new ServicioDeClientes(db, new ValidadorDeCliente(db, new ServicioCatalogosSat(db)), bitacora,
            contexto, new UsoDeCatalogos(db));
        var cliente = Cliente(1);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        await using var guardando = Abrir();
        var servicioGuardando = new ServicioDeClientes(guardando, new ValidadorDeCliente(guardando, new ServicioCatalogosSat(guardando)),
            bitacora, contexto, new UsoDeCatalogos(guardando));
        await using var transaccion = await guardando.Database.BeginTransactionAsync();
        Assert.NotNull(await servicioGuardando.ObtenerParaTimbradoAsync(cliente.Id, default));
        var eliminacion = servicio.EliminarAsync(cliente.Id, default);
        try
        {
            Assert.NotSame(eliminacion, await Task.WhenAny(eliminacion, Task.Delay(200)));
            guardando.Comprobantes.Add(Comprobante(cliente.Id));
            await guardando.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        finally
        {
            await transaccion.DisposeAsync();
        }
        Assert.Equal("catalogo-en-uso", (await eliminacion).Error?.Codigo);
        Assert.True(await db.Clientes.AnyAsync(x => x.Id == cliente.Id));
    }

    private Cliente Cliente(int clave) => new()
    {
        Id = Guid.NewGuid(), ClaveInterna = clave, Rfc = "XAXX010101000", Nombre = "PUBLICO EN GENERAL",
        RegimenFiscal = "616", DomicilioFiscalCp = "42000"
    };

    private Comprobante Comprobante(Guid? cliente) => new()
    {
        Id = Guid.NewGuid(), Estatus = "borrador", TipoDeComprobante = "I", LugarExpedicion = "42000", Moneda = "MXN",
        Exportacion = "01", EmisorRfc = "XAXX010101000", EmisorNombre = "PRUEBA", EmisorRegimenFiscal = "601",
        ClienteId = cliente, ReceptorRfc = "XAXX010101000", ReceptorNombre = "PUBLICO EN GENERAL",
        ReceptorRegimenFiscal = "616", ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "S01", CreadoPorUsuarioId = _titular
    };

    private Empresa Empresa(Guid id) => new()
    {
        Id = id, CuentaId = _cuenta, Rfc = id == _empresa ? "XAXX010101000" : "XEXX010101000", NombreFiscal = "PRUEBA",
        RegimenFiscal = "601", CodigoPostalExpedicion = "42000", ZonaHoraria = "Central Standard Time (Mexico)"
    };

    private Usuario Usuario(Guid id, bool titular = false)
    {
        var usuario = new Usuario { Id = id, CuentaId = _cuenta, Nombre = "Prueba", Email = $"{id:N}@example.com",
            UserName = $"{id:N}@example.com", EsTitular = titular, CreadoPorAdministrador = !titular,
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(), LockoutEnabled = true };
        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, Contrasena);
        return usuario;
    }

    private static OperadorPlataforma Operador(bool principal = false)
    {
        var id = Guid.NewGuid();
        var operador = new OperadorPlataforma { Id = id, Nombre = "Prueba", Correo = $"{id:N}@example.com",
            CorreoNormalizado = $"{id:N}@example.com".ToUpperInvariant(), HashContrasena = "", EsPrincipal = principal };
        operador.HashContrasena = new PasswordHasher<OperadorPlataforma>().HashPassword(operador, Contrasena);
        return operador;
    }
}
