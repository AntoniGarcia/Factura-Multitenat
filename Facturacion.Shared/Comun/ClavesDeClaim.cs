namespace Facturacion.Shared.Comun;

/// <summary>
/// Nombres de los claims del access token. Se dejan cortos porque el token viaja en cada
/// petición. Los emite la fase 1 y los lee <c>IContextoEmpresa</c>, de los dos lados.
/// </summary>
public static class ClavesDeClaim
{
    /// <summary>Id del usuario autenticado.</summary>
    public const string Usuario = "uid";

    /// <summary>Id de la cuenta contratante a la que pertenece el usuario.</summary>
    public const string Cuenta = "cta";

    /// <summary>Id de la empresa activa. Es un claim, nunca un parámetro de la petición.</summary>
    public const string Empresa = "emp";

    /// <summary>Permiso en la empresa activa. Se repite una vez por permiso concedido.</summary>
    public const string Permiso = "perm";

    /// <summary>Id de la familia de refresh tokens de esta sesión, para poder invalidarla entera.</summary>
    public const string Familia = "fam";

    /// <summary>
    /// Id del operador del SaaS. Es la identidad del proveedor, no la de un inquilino.
    /// <para>
    /// <b>Nunca coexiste</b> con <see cref="Usuario"/>, <see cref="Cuenta"/>,
    /// <see cref="Empresa"/> ni <see cref="Permiso"/>: un token lleva una identidad o la otra.
    /// Las políticas lo comprueban en los dos sentidos, y además cada lado usa su propia
    /// audiencia, así que un token del inquilino ni siquiera autentica contra el panel.
    /// </para>
    /// </summary>
    public const string Operador = "opr";
}
