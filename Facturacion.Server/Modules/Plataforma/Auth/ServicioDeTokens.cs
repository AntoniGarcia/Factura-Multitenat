using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>Access token recién emitido.</summary>
public sealed record TokenEmitido(string Token, DateTime ExpiraUtc);

/// <summary>
/// Emite el access token. Los claims son exactamente los cinco de la fase 1: usuario,
/// cuenta, empresa activa, permisos en esa empresa y familia de sesión.
/// <para>
/// La empresa activa va <b>dentro del token</b> y no como parámetro de las peticiones: es
/// lo que impide que el Client pida datos de una empresa a la que no tiene acceso con solo
/// cambiar un número (ARQUITECTURA.md §4).
/// </para>
/// </summary>
public sealed class ServicioDeTokens(IOptions<OpcionesDeJwt> opciones)
{
    private readonly OpcionesDeJwt _opciones = opciones.Value;

    public TokenEmitido Emitir(
        Usuario usuario,
        Guid cuentaId,
        Guid? empresaId,
        IReadOnlyList<string> permisos,
        Guid familiaId)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.MinutosDeVida);

        var claims = new List<Claim>
        {
            new(ClavesDeClaim.Usuario, usuario.Id.ToString()),
            new(ClavesDeClaim.Cuenta, cuentaId.ToString()),
            new(ClavesDeClaim.Familia, familiaId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (empresaId is { } empresa)
            claims.Add(new Claim(ClavesDeClaim.Empresa, empresa.ToString()));

        // Un claim por permiso: así la política de ASP.NET Core se resuelve con una
        // comprobación de claim y no interpretando una cadena separada por comas.
        claims.AddRange(permisos.Select(p => new Claim(ClavesDeClaim.Permiso, p)));

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.ClaveDeFirma)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            notBefore: ahora,
            expires: expira,
            signingCredentials: credenciales);

        return new TokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    /// <summary>
    /// Emite el access token del operador del SaaS. Lleva exactamente tres claims —operador,
    /// familia y jti— y ninguno de tenencia.
    ///
    /// <para><b>Por qué un método aparte y no un parámetro de <see cref="Emitir"/></b></para>
    /// Con una sola ruta de emisión, cualquier cambio futuro podría añadirle un claim de
    /// empresa al token del operador sin que nadie lo note. Separados, el token del operador
    /// se arma en un sitio donde la empresa ni siquiera está disponible.
    ///
    /// <para><b>Audiencia distinta</b></para>
    /// El sufijo hace que los dos tipos de token no sean intercambiables ni aunque compartan
    /// la clave de firma: presentar uno del inquilino al panel falla en la validación de la
    /// audiencia, o sea un 401 antes de llegar a mirar ningún claim.
    /// </summary>
    public TokenEmitido EmitirDeOperador(OperadorPlataforma operador, Guid familiaId)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.MinutosDeVida);

        var claims = new List<Claim>
        {
            new(ClavesDeClaim.Operador, operador.Id.ToString()),
            new(ClavesDeClaim.Familia, familiaId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.ClaveDeFirma)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: AudienciaDeOperador(_opciones.Audiencia),
            claims: claims,
            notBefore: ahora,
            expires: expira,
            signingCredentials: credenciales);

        return new TokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    /// <summary>
    /// La audiencia del panel de operador. Se calcula aquí para que el emisor y el validador
    /// no puedan desincronizarse.
    /// </summary>
    public static string AudienciaDeOperador(string audienciaBase) => $"{audienciaBase}/operador";
}
