using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Correo;

// Propósito distinto al del CSD y con la empresa: no se descifra con el protector del sello
// ni en el contexto de otra empresa.
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
