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
}
