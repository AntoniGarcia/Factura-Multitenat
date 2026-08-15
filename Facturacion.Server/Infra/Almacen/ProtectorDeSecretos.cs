using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>
/// Cifra y descifra secretos cortos que sí viven en la base, como la contraseña de la llave
/// privada del CSD (CLAUDE.md §4). Igual que <see cref="IAlmacenDeArchivos"/>, el protector
/// va atado a la empresa: la contraseña de una empresa no se descifra en el contexto de otra.
/// <para>
/// El texto en claro solo existe en memoria y solo durante el sellado. Nunca se registra en
/// el log, nunca se serializa y nunca vuelve al <c>Client</c>.
/// </para>
/// </summary>
public interface IProtectorDeSecretos
{
    string Cifrar(Guid empresaId, string secreto);

    string Descifrar(Guid empresaId, string cifrado);
}

public sealed class ProtectorDeSecretos(IDataProtectionProvider protecciones) : IProtectorDeSecretos
{
    public string Cifrar(Guid empresaId, string secreto) => Protector(empresaId).Protect(secreto);

    public string Descifrar(Guid empresaId, string cifrado) => Protector(empresaId).Unprotect(cifrado);

    private IDataProtector Protector(Guid empresaId)
        => protecciones.CreateProtector("Facturacion.Secretos.v1", empresaId.ToString());
}
