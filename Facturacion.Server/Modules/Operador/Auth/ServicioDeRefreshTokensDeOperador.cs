using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>Refresh token de operador recién creado; el valor en claro solo existe aquí y en la cookie.</summary>
public sealed record RefreshDeOperadorEmitido(string EnClaro, RefreshTokenOperador Registro);

/// <summary>
/// Rotación de refresh tokens del panel de operador, con la misma mecánica que la del
/// inquilino: familia por sesión, un solo uso por token, y ante un token ya consumido se
/// invalida la familia entera (ARQUITECTURA.md §4).
///
/// <para><b>Por qué es una clase aparte y no un parámetro de la del inquilino</b></para>
/// Las dos hacen lo mismo sobre tablas distintas, y ahí está el punto: si compartieran
/// consulta, un identificador de operador podría resolver una sesión de inquilino o al revés.
/// Lo que sí se comparte es lo que no distingue identidades —el hash y el generador
/// aleatorio—, reutilizados de <see cref="ServicioDeRefreshTokens"/> para no tener dos
/// definiciones de lo que es un token seguro.
/// </summary>
public sealed class ServicioDeRefreshTokensDeOperador(
    AppDbContext baseDeDatos,
    IServicioDeBitacora bitacora,
    ILogger<ServicioDeRefreshTokensDeOperador> registro)
{
    /// <summary>Abre una familia nueva. Se llama al iniciar sesión en el panel.</summary>
    public RefreshDeOperadorEmitido CrearFamilia(Guid operadorId, TimeSpan duracion, string? ip, string? agente)
        => Crear(operadorId, Guid.NewGuid(), DateTime.UtcNow.Add(duracion), ip, agente);

    /// <summary>
    /// Canjea un token por otro de la misma familia. Devuelve error de negocio y no excepción:
    /// que una cookie caduque es lo normal.
    /// </summary>
    public async Task<Resultado<RefreshDeOperadorEmitido>> RotarAsync(
        string enClaro, string? ip, string? agente, CancellationToken ct)
    {
        var hash = ServicioDeRefreshTokens.Hash(enClaro);

        var actual = await baseDeDatos.RefreshTokensOperador
            .Include(t => t.Operador)
            .SingleOrDefaultAsync(t => t.HashToken == hash, ct);

        if (actual is null)
            return SesionNoValida();

        if (actual.ConsumidoUtc is not null)
            return await ReutilizacionDetectada(actual, ct);

        if (actual.RevocadoUtc is not null || actual.ExpiraUtc <= DateTime.UtcNow)
            return SesionNoValida();

        if (!actual.Operador.Activo)
        {
            await InvalidarFamiliaAsync(actual.FamiliaId, "operador desactivado", ct);
            await baseDeDatos.SaveChangesAsync(ct);
            return SesionNoValida();
        }

        // La expiración no se mueve: la familia caduca cuando dijo que caducaría.
        var nuevo = Crear(actual.OperadorId, actual.FamiliaId, actual.ExpiraUtc, ip, agente);

        actual.ConsumidoUtc = DateTime.UtcNow;
        actual.ReemplazadoPorId = nuevo.Registro.Id;

        bitacora.Registrar(
            EntidadesDeBitacora.RefreshTokenOperador,
            actual.Id.ToString(),
            AccionesDeBitacora.TokenRotado,
            despues: new { actual.FamiliaId, Nuevo = nuevo.Registro.Id },
            operadorId: actual.OperadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return nuevo;
    }

    /// <summary>Revoca todos los tokens vivos de una familia. Devuelve cuántos revocó.</summary>
    public async Task<int> InvalidarFamiliaAsync(Guid familiaId, string motivo, CancellationToken ct)
    {
        var vivos = await baseDeDatos.RefreshTokensOperador
            .Where(t => t.FamiliaId == familiaId && t.RevocadoUtc == null)
            .ToListAsync(ct);

        foreach (var token in vivos)
        {
            token.RevocadoUtc = DateTime.UtcNow;
            token.MotivoRevocacion = motivo;
        }

        if (vivos.Count > 0)
            bitacora.Registrar(
                EntidadesDeBitacora.RefreshTokenOperador,
                familiaId.ToString(),
                AccionesDeBitacora.FamiliaInvalidada,
                despues: new { FamiliaId = familiaId, Motivo = motivo, Revocados = vivos.Count },
                operadorId: vivos[0].OperadorId);

        return vivos.Count;
    }

    private RefreshDeOperadorEmitido Crear(
        Guid operadorId, Guid familiaId, DateTime expiraUtc, string? ip, string? agente)
    {
        var enClaro = ServicioDeRefreshTokens.GenerarToken();

        var token = new RefreshTokenOperador
        {
            Id = Guid.NewGuid(),
            FamiliaId = familiaId,
            OperadorId = operadorId,
            HashToken = ServicioDeRefreshTokens.Hash(enClaro),
            CreadoUtc = DateTime.UtcNow,
            ExpiraUtc = expiraUtc,
            IpCreacion = ip,
            AgenteUsuario = Recortar(agente, 256)
        };

        baseDeDatos.RefreshTokensOperador.Add(token);

        return new RefreshDeOperadorEmitido(enClaro, token);
    }

    private async Task<Resultado<RefreshDeOperadorEmitido>> ReutilizacionDetectada(
        RefreshTokenOperador token, CancellationToken ct)
    {
        // Que alguien reutilice la cookie del panel es más grave que en el lado del inquilino:
        // desde aquí se acreditan pagos. Se registra para poder alertar.
        registro.LogWarning(
            "Reutilización de refresh token de operador. Familia {FamiliaId}, operador {OperadorId}",
            token.FamiliaId, token.OperadorId);

        bitacora.Registrar(
            EntidadesDeBitacora.RefreshTokenOperador,
            token.Id.ToString(),
            AccionesDeBitacora.ReutilizacionDeToken,
            despues: new { token.FamiliaId, token.ConsumidoUtc },
            operadorId: token.OperadorId);

        await InvalidarFamiliaAsync(token.FamiliaId, "reutilización de token consumido", ct);
        await baseDeDatos.SaveChangesAsync(ct);

        return SesionNoValida();
    }

    // Mismo error para toda causa: ni al cliente legítimo ni a quien robó la cookie les sirve
    // saber cuál de todas fue.
    private static ErrorNegocio SesionNoValida()
        => ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.");

    private static string? Recortar(string? valor, int tope)
        => valor is null || valor.Length <= tope ? valor : valor[..tope];
}
