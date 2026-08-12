using System.Security.Cryptography;
using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>Refresh token recién creado: el valor en claro solo existe aquí y en la cookie.</summary>
public sealed record RefreshEmitido(string EnClaro, RefreshToken Registro);

/// <summary>
/// Rotación de refresh tokens con detección de reutilización (CLAUDE.md §4).
///
/// <para><b>Cómo funciona</b></para>
/// Cada sesión es una familia. Cada canje invalida el token usado y emite otro de la misma
/// familia, con la <b>misma fecha de expiración</b>: "12 horas" significa doce horas de
/// sesión, no doce horas renovables para siempre.
///
/// <para><b>Por qué importa la reutilización</b></para>
/// Un token ya consumido que vuelve a llegar significa que dos partes tienen la misma
/// cookie: la legítima y quien la copió. Como no hay forma de distinguirlas, se invalida la
/// familia completa y las dos quedan fuera. Es la respuesta correcta: preferimos que el
/// usuario vuelva a iniciar sesión a que el ladrón siga dentro.
///
/// <para>
/// Esta clase persiste sus propios cambios. La invalidación de una familia no puede depender
/// de que quien la llamó se acuerde de guardar.
/// </para>
/// </summary>
public sealed class ServicioDeRefreshTokens(
    AppDbContext baseDeDatos,
    IServicioDeBitacora bitacora,
    ILogger<ServicioDeRefreshTokens> registro)
{
    /// <summary>Abre una familia nueva. Se llama al iniciar sesión.</summary>
    public RefreshEmitido CrearFamilia(Guid usuarioId, TimeSpan duracion, string? ip, string? agente)
        => Crear(usuarioId, Guid.NewGuid(), DateTime.UtcNow.Add(duracion), ip, agente, empresaActivaId: null);

    /// <summary>
    /// Recuerda en la familia qué empresa quedó activa, para que el próximo refresh emita el
    /// token ya con ella. Se llama desde el cambio de empresa; no consume ningún token ni
    /// toca la cookie.
    /// </summary>
    public async Task FijarEmpresaActivaAsync(Guid familiaId, Guid empresaId, CancellationToken ct)
    {
        var vivo = await baseDeDatos.RefreshTokens
            .Where(t => t.FamiliaId == familiaId && t.ConsumidoUtc == null && t.RevocadoUtc == null)
            .OrderByDescending(t => t.CreadoUtc)
            .FirstOrDefaultAsync(ct);

        if (vivo is not null)
            vivo.EmpresaActivaId = empresaId;
    }

    /// <summary>
    /// Canjea un refresh token por otro de la misma familia. Devuelve error de negocio —no
    /// excepción— cuando el token no sirve: que una cookie caduque es lo normal, no algo
    /// excepcional.
    /// </summary>
    public async Task<Resultado<RefreshEmitido>> RotarAsync(
        string enClaro, string? ip, string? agente, CancellationToken ct)
    {
        var hash = Hash(enClaro);

        var actual = await baseDeDatos.RefreshTokens
            .Include(t => t.Usuario)
            .SingleOrDefaultAsync(t => t.HashToken == hash, ct);

        if (actual is null)
            return SesionNoValida();

        if (actual.ConsumidoUtc is not null)
            return await ReutilizacionDetectada(actual, ct);

        if (actual.RevocadoUtc is not null || actual.ExpiraUtc <= DateTime.UtcNow)
            return SesionNoValida();

        if (!actual.Usuario.Activo)
        {
            await InvalidarFamiliaAsync(actual.FamiliaId, "usuario desactivado", ct);
            return SesionNoValida();
        }

        // La expiración no se mueve: la familia caduca cuando dijo que caducaría. La empresa
        // activa sí se arrastra, que es lo que evita volver al selector en cada renovación.
        var nuevo = Crear(actual.UsuarioId, actual.FamiliaId, actual.ExpiraUtc, ip, agente, actual.EmpresaActivaId);

        actual.ConsumidoUtc = DateTime.UtcNow;
        actual.ReemplazadoPorId = nuevo.Registro.Id;

        bitacora.Registrar(
            EntidadesDeBitacora.RefreshToken,
            actual.Id.ToString(),
            AccionesDeBitacora.TokenRotado,
            despues: new { actual.FamiliaId, Nuevo = nuevo.Registro.Id },
            usuarioId: actual.UsuarioId);

        await baseDeDatos.SaveChangesAsync(ct);

        return nuevo;
    }

    /// <summary>Revoca todos los tokens vivos de una familia. Devuelve cuántos revocó.</summary>
    public async Task<int> InvalidarFamiliaAsync(Guid familiaId, string motivo, CancellationToken ct)
    {
        var vivos = await baseDeDatos.RefreshTokens
            .Where(t => t.FamiliaId == familiaId && t.RevocadoUtc == null)
            .ToListAsync(ct);

        foreach (var token in vivos)
        {
            token.RevocadoUtc = DateTime.UtcNow;
            token.MotivoRevocacion = motivo;
        }

        if (vivos.Count > 0)
            bitacora.Registrar(
                EntidadesDeBitacora.RefreshToken,
                familiaId.ToString(),
                AccionesDeBitacora.FamiliaInvalidada,
                despues: new { FamiliaId = familiaId, Motivo = motivo, Revocados = vivos.Count },
                usuarioId: vivos[0].UsuarioId);

        return vivos.Count;
    }

    /// <summary>SHA-256 en base64. El token en claro nunca toca la base.</summary>
    public static string Hash(string enClaro)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(enClaro)));

    private RefreshEmitido Crear(
        Guid usuarioId, Guid familiaId, DateTime expiraUtc, string? ip, string? agente, Guid? empresaActivaId)
    {
        var enClaro = Base64UrlEncoder(RandomNumberGenerator.GetBytes(32));

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamiliaId = familiaId,
            UsuarioId = usuarioId,
            HashToken = Hash(enClaro),
            EmpresaActivaId = empresaActivaId,
            CreadoUtc = DateTime.UtcNow,
            ExpiraUtc = expiraUtc,
            IpCreacion = ip,
            AgenteUsuario = Recortar(agente, 256)
        };

        baseDeDatos.RefreshTokens.Add(token);

        return new RefreshEmitido(enClaro, token);
    }

    private async Task<Resultado<RefreshEmitido>> ReutilizacionDetectada(RefreshToken token, CancellationToken ct)
    {
        // Incidente de seguridad: se registra con nivel de advertencia para que se pueda
        // alertar sobre él, sin el valor del token, que el enriquecedor omitiría de todos modos.
        registro.LogWarning(
            "Reutilización de refresh token consumido. Familia {FamiliaId}, usuario {UsuarioId}",
            token.FamiliaId, token.UsuarioId);

        bitacora.Registrar(
            EntidadesDeBitacora.RefreshToken,
            token.Id.ToString(),
            AccionesDeBitacora.ReutilizacionDeToken,
            despues: new { token.FamiliaId, token.ConsumidoUtc },
            usuarioId: token.UsuarioId);

        await InvalidarFamiliaAsync(token.FamiliaId, "reutilización de token consumido", ct);
        await baseDeDatos.SaveChangesAsync(ct);

        return SesionNoValida();
    }

    // Mismo error para toda causa: al cliente no le sirve saber si la cookie caducó, si se
    // revocó o si nunca existió, y a quien la robó tampoco.
    private static ErrorNegocio SesionNoValida()
        => ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

    private static string Base64UrlEncoder(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Recortar(string? valor, int tope)
        => valor is null || valor.Length <= tope ? valor : valor[..tope];
}
