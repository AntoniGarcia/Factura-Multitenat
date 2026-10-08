using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Microsoft.Data.SqlClient;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.EntityFrameworkCore;
using Facturacion.Server.Modules.Plataforma.Folios;
using Facturacion.Server.Infra.Bitacora;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

if (args.Length == 2 && args[0] == "preparar-cuenta-qa")
{
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    var destino = new SqlConnectionStringBuilder(conexion);
    if (destino.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Esta preparación no admite Azure SQL.");
    const string correoQa = "qa-aislamiento@example.invalid";
    var servicios = new ServiceCollection();
    servicios.AddLogging();
    servicios.AddDbContext<AppDbContext>(o => o.Configurar(conexion, ContextoEmpresaFijo.SinEmpresa));
    servicios.AddSingleton<IContextoEmpresaInterno>(ContextoEmpresaFijo.SinEmpresa);
    servicios.AddIdentityCore<Usuario>().AddEntityFrameworkStores<AppDbContext>();
    await using var proveedor = servicios.BuildServiceProvider();
    await using var ambito = proveedor.CreateAsyncScope();
    var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
    var usuarios = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    var existente = await usuarios.FindByEmailAsync(correoQa);
    if (existente is not null)
    {
        var cuentaExistente = await db.Cuentas.SingleAsync(c => c.Id == existente.CuentaId);
        if (cuentaExistente.Nombre != "QA - aislamiento entre cuentas")
            throw new InvalidOperationException("El correo QA ya pertenece a otra cuenta; no se modificó.");
        Console.WriteLine("La cuenta QA ya existe. No se cambiaron credenciales, permisos ni datos.");
        return 0;
    }
    await using var transaccion = await db.Database.BeginTransactionAsync();
    var ahora = DateTime.UtcNow;
    var cuenta = new Cuenta
    {
        Id = Guid.NewGuid(), Nombre = "QA - aislamiento entre cuentas",
        CorreoContacto = correoQa, FechaAltaUtc = ahora
    };
    var empresa = new Empresa
    {
        Id = Guid.NewGuid(), CuentaId = cuenta.Id, Rfc = "XAXX010101000",
        NombreFiscal = "QA AISLAMIENTO NO EMITIR CFDI", RegimenFiscal = "616",
        CodigoPostalExpedicion = "42000", ZonaHoraria = "Central Standard Time (Mexico)",
        FechaAltaUtc = ahora
    };
    db.Cuentas.Add(cuenta);
    db.Empresas.Add(empresa);
    await db.SaveChangesAsync();
    var usuario = new Usuario
    {
        Id = Guid.NewGuid(), CuentaId = cuenta.Id, Nombre = "QA aislamiento",
        UserName = correoQa, Email = correoQa, EmailConfirmed = true,
        FechaAltaUtc = ahora, CreadoPorAdministrador = true, LockoutEnabled = true
    };
    // No se inventa una contraseña compartida ni se guarda una credencial en archivos.
    // El operador debe asignarla antes del inicio de sesión interactivo.
    var alta = await usuarios.CreateAsync(usuario);
    if (!alta.Succeeded) throw new InvalidOperationException(string.Join("; ", alta.Errors.Select(e => e.Description)));
    db.UsuariosEmpresas.Add(new UsuarioEmpresa { UsuarioId = usuario.Id, EmpresaId = empresa.Id, FechaAltaUtc = ahora });
    db.UsuariosEmpresasPermisos.Add(new UsuarioEmpresaPermiso
    {
        UsuarioId = usuario.Id, EmpresaId = empresa.Id, PermisoClave = Facturacion.Shared.Comun.Permisos.Timbrar,
        OtorgadoUtc = ahora
    });
    var contextoQa = new ContextoEmpresaFijo(empresa.Id, cuenta.Id, usuario.Id);
    new ServicioDeBitacora(db, contextoQa, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor())
        .Registrar("PreparacionQA", cuenta.Id.ToString(), "cuenta-qa-creada",
            despues: new { cuenta.Nombre, Empresa = empresa.NombreFiscal, Correo = correoQa, Permiso = "timbrar", SinContrasena = true });
    await db.SaveChangesAsync();
    await transaccion.CommitAsync();
    Console.WriteLine($"Cuenta y empresa QA creadas. Usuario: {correoQa}. Permiso único: timbrar.");
    Console.WriteLine("Sin contraseña de acceso: debe asignarla el operador. Sin CSD, timbres, documentos ni configuración SMTP.");
    return 0;
}

if (args.Length == 2 && args[0] == "relaciones-empresa")
{
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .Configurar(conexion, ContextoEmpresaFijo.SinEmpresa).Options, ContextoEmpresaFijo.SinEmpresa);
    await using var sql = new SqlConnection(conexion);
    await sql.OpenAsync();
    static string NombreSql(string nombre) => "[" + nombre.Replace("]", "]]", StringComparison.Ordinal) + "]";
    var comprobadas = 0;
    long cruces = 0;
    foreach (var entidad in db.Model.GetEntityTypes().Where(e => typeof(IEntidadDeEmpresa).IsAssignableFrom(e.ClrType)))
    {
        if (!entidad.GetDeclaredQueryFilters().Any()) throw new InvalidOperationException("Entidad de empresa sin filtro global.");
        foreach (var fk in entidad.GetForeignKeys().Where(f => typeof(IEntidadDeEmpresa).IsAssignableFrom(f.PrincipalEntityType.ClrType)))
        {
            var tabla = StoreObjectIdentifier.Table(entidad.GetTableName()!, entidad.GetSchema());
            var padre = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema());
            var condiciones = fk.Properties.Zip(fk.PrincipalKey.Properties,
                (h, p) => $"h.{NombreSql(h.GetColumnName(tabla)!)}=p.{NombreSql(p.GetColumnName(padre)!)}");
            using var comando = sql.CreateCommand();
            comando.CommandText = $"SELECT COUNT_BIG(*) FROM {NombreSql(tabla.Schema ?? "dbo")}.{NombreSql(tabla.Name)} h JOIN {NombreSql(padre.Schema ?? "dbo")}.{NombreSql(padre.Name)} p ON {string.Join(" AND ", condiciones)} WHERE h.EmpresaId<>p.EmpresaId";
            var cantidad = Convert.ToInt64(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            cruces += cantidad;
            comprobadas++;
            if (cantidad > 0) Console.WriteLine($"Cruce: {tabla.Name} -> {padre.Name}: {cantidad}");
        }
    }
    // Referencias de snapshots que no están declaradas como FK en el modelo.
    foreach (var referencia in new[]
    {
        ("Comprobantes", "ClienteId", "Clientes"),
        ("Comprobantes", "SerieId", "Series"),
        ("Conceptos", "ProductoId", "Productos"),
        ("TrasladosCartaPorte", "ClienteDestinoId", "Clientes")
    })
    {
        using var comando = sql.CreateCommand();
        comando.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.{NombreSql(referencia.Item1)} h JOIN dbo.{NombreSql(referencia.Item3)} p ON h.{NombreSql(referencia.Item2)}=p.Id WHERE h.EmpresaId<>p.EmpresaId";
        var cantidad = Convert.ToInt64(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        cruces += cantidad;
        comprobadas++;
        if (cantidad > 0) Console.WriteLine($"Cruce: {referencia.Item1}.{referencia.Item2}: {cantidad}");
    }
    Console.WriteLine($"Relaciones comprobadas: {comprobadas}. Cruces de empresa: {cruces}. Solo lectura.");
    return cruces == 0 ? 0 : 1;
}

if (args.Length == 2 && args[0] == "aislamiento-documentos")
{
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    await using var lectura = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .Configurar(conexion, ContextoEmpresaFijo.SinEmpresa).Options, ContextoEmpresaFijo.SinEmpresa);
    var empresas = await lectura.Empresas.AsNoTracking().Select(e => new { e.Id, e.CuentaId }).ToListAsync();
    async Task Revisar<T>(AppDbContext db, Guid? empresa) where T : class, IEntidadDeEmpresa
    {
        var tenencias = await db.Set<T>().AsNoTracking().Select(e => e.EmpresaId).ToListAsync();
        if (tenencias.Any(e => e != empresa))
            throw new InvalidOperationException($"El filtro de {typeof(T).Name} devolvió otra empresa.");
    }
    var verificadas = 0;
    foreach (var empresa in empresas.Select(e => (Guid?)e.Id).Append(null).Append(Guid.NewGuid()))
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(conexion, tenencia).Options, tenencia);
        await Revisar<Comprobante>(db, empresa);
        await Revisar<Concepto>(db, empresa);
        await Revisar<ImpuestoConcepto>(db, empresa);
        await Revisar<Pago>(db, empresa);
        await Revisar<DocumentoPagado>(db, empresa);
        await Revisar<Cliente>(db, empresa);
        await Revisar<Producto>(db, empresa);
        await Revisar<Serie>(db, empresa);
        verificadas += 8;
    }
    Console.WriteLine($"Consultas verificadas: {verificadas}; empresas: {empresas.Count}; cuentas: {empresas.Select(e => e.CuentaId).Distinct().Count()}. Sin empresa y empresa inexistente: sin datos.");
    Console.WriteLine("Solo lectura EF/SQL; no sustituye la autorización HTTP ni las pruebas de envío.");
    return 0;
}

if (args.Length == 2 && args[0] == "validar-series")
{
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    await using var lectura = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .Configurar(conexion, ContextoEmpresaFijo.SinEmpresa).Options, ContextoEmpresaFijo.SinEmpresa);
    var series = await lectura.Series.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    var activa = series.FirstOrDefault(s => s.Activa)
        ?? throw new InvalidOperationException("No hay series activas para comprobar.");
    async Task Comprobar(Guid empresa, Guid id, string tipo, bool valido)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(conexion, tenencia).Options, tenencia);
        var bitacora = new ServicioDeBitacora(db, tenencia, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());
        var resultado = await new ServicioDeFolios(db, tenencia, bitacora).ValidarSerieAsync(id, tipo, default);
        if (resultado.EsExito != valido) throw new InvalidOperationException("Resultado incorrecto de validación de serie.");
    }
    await Comprobar(activa.EmpresaId, activa.Id, activa.TipoComprobante, true);
    await Comprobar(Guid.NewGuid(), activa.Id, activa.TipoComprobante, false);
    await Comprobar(activa.EmpresaId, activa.Id, "tipo-incompatible", false);
    await Comprobar(activa.EmpresaId, Guid.NewGuid(), activa.TipoComprobante, false);
    var inactiva = series.FirstOrDefault(s => !s.Activa);
    if (inactiva is not null) await Comprobar(inactiva.EmpresaId, inactiva.Id, inactiva.TipoComprobante, false);
    Console.WriteLine("Serie propia válida, ajena, inexistente y tipo incompatible: correctos. Sin reservar folios ni modificar datos.");
    Console.WriteLine(inactiva is null ? "No hay serie inactiva para verificar ese caso con datos reales." : "Serie inactiva rechazada.");
    return 0;
}

if (args.Length == 2 && args[0] == "sellado-empresa")
{
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    var activa = Guid.NewGuid();
    foreach (var estado in new[] { EntityState.Modified, EntityState.Deleted })
    {
        var tenencia = new ContextoEmpresaFijo(activa);
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(conexion, tenencia).Options;
        await using var db = new AppDbContext(opciones, tenencia);
        var ajena = new ClaveIdempotencia
        {
            Id = Guid.NewGuid(), EmpresaId = Guid.NewGuid(), UsuarioId = Guid.NewGuid(),
            Clave = "prueba-no-persistida", Endpoint = "prueba", HashPeticion = "prueba",
            RespuestaJson = "{}", CreadoUtc = DateTime.UtcNow, ExpiraUtc = DateTime.UtcNow.AddHours(1)
        };
        db.Attach(ajena);
        if (estado == EntityState.Modified)
        {
            ajena.RespuestaJson = "cambio-no-persistido";
            db.Entry(ajena).Property(x => x.RespuestaJson).IsModified = true;
        }
        else db.Remove(ajena);
        try
        {
            await db.SaveChangesAsync();
            throw new InvalidOperationException("No se rechazó la escritura de empresa ajena.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("empresa distinta de la activa", StringComparison.Ordinal))
        {
            Console.WriteLine($"{estado}: escritura ajena rechazada antes de enviar SQL.");
        }
    }
    return 0;
}

if (args.Length == 2 && args[0] == "bloqueo-pagos")
{
    // Solo locks transaccionales: no inserta, modifica ni borra datos fiscales.
    using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
    var conexion = config.RootElement.GetProperty("ConnectionStrings").GetProperty("BaseDeDatos").GetString();
    await using var primera = new SqlConnection(conexion);
    await using var segunda = new SqlConnection(conexion);
    await primera.OpenAsync();
    await segunda.OpenAsync();
    using var t1 = primera.BeginTransaction();
    using var t2 = segunda.BeginTransaction();
    var recurso = $"documentos-pagos:{Guid.NewGuid():D}";
    async Task<int> Tomar(SqlConnection c, SqlTransaction t, string nombre)
    {
        using var comando = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@nombre, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0; SELECT @r;", c, t);
        comando.Parameters.AddWithValue("@nombre", nombre);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
    if (await Tomar(primera, t1, recurso) < 0 || await Tomar(segunda, t2, recurso) != -1)
        throw new InvalidOperationException("No se bloqueó la segunda sesión.");
    if (await Tomar(segunda, t2, recurso + "-otra-empresa") < 0)
        throw new InvalidOperationException("Se bloqueó indebidamente otro recurso.");
    t1.Rollback();
    if (await Tomar(segunda, t2, recurso) < 0)
        throw new InvalidOperationException("El bloqueo no se liberó al terminar.");
    t2.Rollback();
    Console.WriteLine("Bloqueo entre sesiones, independencia por recurso y liberación: correctos. Sin cambios en datos.");
    return 0;
}

if (args.Length == 3 && args[0] == "pdf-pagos")
{
    var prueba = JsonSerializer.Deserialize<Comprobante>(File.ReadAllText(args[1]))
        ?? throw new InvalidOperationException("No se pudo leer el borrador de prueba.");
    if (prueba.TipoDeComprobante != "P" || prueba.Estatus != "borrador")
        throw new InvalidOperationException("Solo se admiten borradores de pago de prueba.");
    if (GeneradorDeXmlPago.Validar(prueba) is { } error)
        throw new InvalidOperationException(error.ToString());
    var huso = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
    var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(prueba.FechaEmisionUtc, DateTimeKind.Utc), huso);
    QuestPDF.Settings.License = LicenseType.Community;
    File.WriteAllBytes(args[2], new GeneradorDePdfCfdi().Generar(prueba,
        new DatosDelPdf(local, 2, null, EsBorrador: true) { ZonaHoraria = huso }));
    Console.WriteLine("PDF de prueba sin validez fiscal generado; sin base de datos ni PAC.");
    return 0;
}

if (args.Length == 4 && args[0] == "repartir-pago")
{
    var impuestos = JsonSerializer.Deserialize<ImpuestoDeFactura[]>(File.ReadAllText(args[1]))
        ?? throw new InvalidOperationException("No se pudieron leer los impuestos de prueba.");
    var reparto = CalculoDeImpuestosDePago.Repartir(impuestos,
        decimal.Parse(args[2], CultureInfo.InvariantCulture), decimal.Parse(args[3], CultureInfo.InvariantCulture), 6);
    Console.WriteLine(JsonSerializer.Serialize(reparto, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (args.Length != 4 || args[0] != "xml-pagos")
{
    Console.WriteLine("Uso: xml-pagos <comprobante-de-prueba.json> <carpeta-SAT> <salida.xml>");
    Console.WriteLine("O: repartir-pago <impuestos-de-prueba.json> <total-factura> <abono>");
    Console.WriteLine("Solo verifica estructura local; no firma, no consulta la base y no llama al PAC.");
    return 2;
}

var comprobante = JsonSerializer.Deserialize<Comprobante>(File.ReadAllText(args[1]))
    ?? throw new InvalidOperationException("No se pudo leer el comprobante de prueba.");
if (comprobante.TipoDeComprobante != "P" || comprobante.Estatus != "borrador")
    throw new InvalidOperationException("La herramienta solo admite borradores de prueba de tipo P.");

var zona = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
var fecha = TimeZoneInfo.ConvertTimeFromUtc(
    DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona);
var datos = new DatosDeEmision(fecha, 2, "30001000000400002434",
    Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })) { ZonaHoraria = zona };
var documento = new GeneradorDeXmlCfdi().Generar(comprobante, datos);
// Marcadores sintéticos solo para comprobar el XSD: no son una firma ni un certificado real.
documento.Root!.SetAttributeValue("Sello", Convert.ToBase64String(new byte[256]));
documento.AddFirst(new XComment("SOLO PRUEBA DE ESTRUCTURA. SIN FIRMA REAL NI VALIDEZ FISCAL."));
documento.Save(args[3]);

var entorno = Host.CreateApplicationBuilder().Environment;
var esquemas = new EsquemasSat(entorno, Options.Create(new OpcionesDeEsquemasSat { Ruta = Path.GetFullPath(args[2]) }));
var problemas = new List<string>();
documento.Validate(esquemas.EsquemaCfdiPagos, (_, e) => problemas.Add(e.Message));
if (problemas.Count > 0)
{
    foreach (var problema in problemas) Console.WriteLine(problema);
    return 1;
}

var cadena = new StringBuilder();
using (var lector = documento.CreateReader())
using (var escritor = XmlWriter.Create(cadena, new XmlWriterSettings
    { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
{
    esquemas.CadenaOriginal.Transform(lector, escritor);
}
Console.WriteLine($"Estructura XSD válida. Cadena original calculada: {cadena.Length} caracteres.");
Console.WriteLine("No se verificó una firma real ni se realizó un timbrado.");
return 0;
