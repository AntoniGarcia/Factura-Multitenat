using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Seguridad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Infra.ActualizacionBase;

public sealed class AccesoTemporalDeBase(IConfiguration configuracion, IHostEnvironment entorno,
    IControlDeIntentos intentos, IOptions<PasswordHasherOptions> opciones)
{
    private readonly PasswordHasher<Usuario> _hasher = new(opciones);
    public const string Esquema = "mantenimiento-base";
    public const string ClaimUsuario = "mantenimiento_uid";
    private sealed record Sesion(Guid Usuario, string Sello, DateTime Hasta);
    private readonly ConcurrentDictionary<string, Sesion> _sesiones = new();
    private string Correo => configuracion["MantenimientoBase:Correo"]?.Trim().ToUpperInvariant() ?? "";

    public bool Habilitado => !entorno.IsProduction()
        && bool.TryParse(configuracion["MantenimientoBase:Habilitado"], out var habilitado) && habilitado
        && Correo.Length > 0 && DateTimeOffset.TryParse(configuracion["MantenimientoBase:HastaUtc"], out var hasta)
        && hasta > DateTimeOffset.UtcNow;

    private async Task<Usuario?> LeerAsync(Guid? id, CancellationToken ct)
    {
        if (!Habilitado) return null;
        await using var conexion = new SqlConnection(configuracion.GetConnectionString("BaseDeDatos"));
        await conexion.OpenAsync(ct);
        await using var comando = conexion.CreateCommand();
        // Solo columnas que ya existían en el primer esquema, sin cargar el modelo actual de EF.
        comando.CommandText = """
            SELECT u.Id,u.PasswordHash,u.SecurityStamp,u.LockoutEnd,u.Activo,u.EmailConfirmed
            FROM dbo.AspNetUsers u JOIN dbo.Cuentas c ON c.Id=u.CuentaId
            WHERE u.NormalizedEmail=@correo AND c.Activa=1 AND (@id IS NULL OR u.Id=@id);
            """;
        comando.Parameters.AddWithValue("@correo", Correo);
        comando.Parameters.Add("@id", System.Data.SqlDbType.UniqueIdentifier).Value = (object?)id ?? DBNull.Value;
        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct) || !lector.GetBoolean(4) || !lector.GetBoolean(5)
            || (!lector.IsDBNull(3) && lector.GetFieldValue<DateTimeOffset>(3) > DateTimeOffset.UtcNow)) return null;
        var usuario = new Usuario { Id = lector.GetGuid(0), Nombre = "Mantenimiento", Email = Correo,
            PasswordHash = lector.IsDBNull(1) ? null : lector.GetString(1), SecurityStamp = lector.IsDBNull(2) ? "" : lector.GetString(2) };
        // No se admite un correo duplicado en esquemas antiguos.
        return await lector.ReadAsync(ct) ? null : usuario;
    }

    public async Task<bool> DisponibleAsync(Guid id, CancellationToken ct) => await LeerAsync(id, ct) is not null;

    public async Task<string?> IniciarAsync(string correo, string contrasena, string ip, CancellationToken ct)
    {
        var clave = "mantenimiento-login:" + ip;
        if (intentos.BloqueoRestante(clave) is not null) return null;
        var usuario = Habilitado && string.Equals(correo.Trim(), Correo, StringComparison.OrdinalIgnoreCase)
            ? await LeerAsync(null, ct) : null;
        if (!Verificar(usuario, contrasena)) { intentos.RegistrarFallo(clave); return null; }
        intentos.Limpiar(clave);
        foreach (var vencida in _sesiones.Where(x => x.Value.Hasta < DateTime.UtcNow)) _sesiones.TryRemove(vencida.Key, out _);
        if (_sesiones.Count >= 10) _sesiones.Clear();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sesiones[Huella(token)] = new(usuario!.Id, usuario.SecurityStamp ?? "", DateTime.UtcNow.AddMinutes(10));
        return token;
    }

    public async Task<Guid?> ValidarAsync(string token, CancellationToken ct)
    {
        if (!Habilitado || token.Length != 64 || !_sesiones.TryGetValue(Huella(token), out var sesion)
            || sesion.Hasta < DateTime.UtcNow) return null;
        var usuario = await LeerAsync(sesion.Usuario, ct);
        return usuario is not null && usuario.SecurityStamp == sesion.Sello ? usuario.Id : null;
    }

    public async Task<bool> ConfirmarAsync(Guid id, string contrasena, CancellationToken ct)
    {
        var clave = "mantenimiento-confirmar:" + id;
        if (intentos.BloqueoRestante(clave) is not null) return false;
        if (!Verificar(await LeerAsync(id, ct), contrasena)) { intentos.RegistrarFallo(clave); return false; }
        intentos.Limpiar(clave);
        return true;
    }

    private bool Verificar(Usuario? usuario, string contrasena) => usuario is not null && usuario.PasswordHash is { } hash
        && contrasena.Length is > 0 and <= 128 && _hasher.VerifyHashedPassword(usuario, hash, contrasena) != PasswordVerificationResult.Failed;
    private static string Huella(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class AutenticacionTemporalDeBase(IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory registro, UrlEncoder codificador, AccesoTemporalDeBase acceso)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, registro, codificador)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cabecera = Request.Headers.Authorization.ToString();
        if (!cabecera.StartsWith("Mantenimiento ", StringComparison.Ordinal)) return AuthenticateResult.NoResult();
        var id = await acceso.ValidarAsync(cabecera[14..], Context.RequestAborted);
        if (id is null) return AuthenticateResult.Fail("Acceso temporal no válido.");
        var identidad = new ClaimsIdentity([new Claim(AccesoTemporalDeBase.ClaimUsuario, id.Value.ToString()),
            new Claim("mantenimiento_permiso", "actualizar_base")], AccesoTemporalDeBase.Esquema);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name));
    }
}
