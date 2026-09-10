using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>
/// Cifra y descifra secretos cortos que sí viven en la base, como la contraseña de la llave
/// privada del CSD (ARQUITECTURA.md §4). Igual que <see cref="IAlmacenDeArchivos"/>, el protector
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

/// <summary>
/// Protege secretos globales del SaaS. Usa un propósito distinto al de los CSD para que una
/// credencial SMTP no pueda descifrarse con el protector ligado a una empresa.
/// </summary>
public interface IProtectorDeSecretosDelSistema
{
    string Cifrar(string secreto);

    string Descifrar(string cifrado);
}

public sealed class ProtectorDeSecretos(IDataProtectionProvider protecciones) : IProtectorDeSecretos
{
    public string Cifrar(Guid empresaId, string secreto) => Protector(empresaId).Protect(secreto);

    public string Descifrar(Guid empresaId, string cifrado) => Protector(empresaId).Unprotect(cifrado);

    private IDataProtector Protector(Guid empresaId)
        => protecciones.CreateProtector("Facturacion.Secretos.v1", empresaId.ToString());
}

public sealed class ProtectorDeSecretosDelSistema(IDataProtectionProvider protecciones)
    : IProtectorDeSecretosDelSistema
{
    private readonly IDataProtector _protector =
        protecciones.CreateProtector("Facturacion.SecretosDelSistema.v1");

    public string Cifrar(string secreto) => _protector.Protect(secreto);

    public string Descifrar(string cifrado) => _protector.Unprotect(cifrado);
}
