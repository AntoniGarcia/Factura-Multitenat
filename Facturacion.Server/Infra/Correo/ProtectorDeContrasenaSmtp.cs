using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Cifra la contraseña del SMTP propio de una empresa (AGENTS.md §11).
/// <para>
/// El propósito es distinto al de la contraseña del CSD y lleva la empresa: una contraseña de
/// correo no se descifra con el protector del sello ni en el contexto de otra empresa.
/// </para>
/// </summary>
public interface IProtectorDeContrasenaSmtp
{
    string Cifrar(Guid empresaId, string contrasena);

    string Descifrar(Guid empresaId, string cifrada);
}

public sealed class ProtectorDeContrasenaSmtp(IDataProtectionProvider protecciones) : IProtectorDeContrasenaSmtp
{
    public string Cifrar(Guid empresaId, string contrasena) => Protector(empresaId).Protect(contrasena);

    public string Descifrar(Guid empresaId, string cifrada) => Protector(empresaId).Unprotect(cifrada);

    private IDataProtector Protector(Guid empresaId)
        => protecciones.CreateProtector("Facturacion.CorreoDeEmpresa.v1", empresaId.ToString());
}
