using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Contratos;

/// <summary>
/// Empresa y usuario de la petición en curso, leídos de los claims del access token.
/// Es la única fuente de la empresa activa: ningún endpoint recibe un identificador de
/// empresa por ruta, query o cuerpo (ARQUITECTURA.md §4).
/// </summary>
public interface IContextoEmpresa
{
    /// <summary>
    /// Empresa activa. Consultarla en una petición que todavía no tiene empresa
    /// —inicio de sesión, selección de empresa— lanza excepción: es un error de programación,
    /// no un caso de negocio.
    /// </summary>
    Guid EmpresaId { get; }

    /// <summary>Usuario autenticado.</summary>
    Guid UsuarioId { get; }

    /// <summary>
    /// Indica si el usuario tiene el permiso en la empresa activa.
    /// Usa las claves de <see cref="Permisos"/>; no escribas la cadena a mano.
    /// </summary>
    bool Tiene(string permiso);
}

/// <summary>Comodidad tipada sobre <see cref="IContextoEmpresa.Tiene(string)"/>.</summary>
public static class ContextoEmpresaExtensiones
{
    public static bool Tiene(this IContextoEmpresa contexto, Permiso permiso)
        => contexto.Tiene(permiso.ACadena());
}
