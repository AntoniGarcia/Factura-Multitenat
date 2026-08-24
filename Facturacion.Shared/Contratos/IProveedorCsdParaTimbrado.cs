namespace Facturacion.Shared.Contratos;

/// <summary>
/// Entrega el certificado de sello digital descifrado. <b>Uso exclusivo del módulo de
/// timbrado.</b>
/// <para>
/// Está separada de <see cref="IServicioEmpresaEmisora"/> a propósito: mientras el CSD
/// viviera en la misma interfaz que el RFC del emisor, cualquier código que necesitara un
/// dato inocente obtenía de paso la llave privada. Al estar sola, se puede registrar y
/// auditar su resolución.
/// </para>
/// <para>
/// Todo llamado queda en la bitácora. El resultado nunca se registra en el log, nunca se
/// serializa y nunca vuelve al cliente (ARQUITECTURA.md §4).
/// </para>
/// </summary>
public interface IProveedorCsdParaTimbrado
{
    /// <summary>Devuelve el CSD vigente de la empresa activa, ya descifrado en memoria.</summary>
    Task<CsdDescifradoDto> ObtenerAsync(CancellationToken ct);
}

/// <summary>
/// Material del certificado de sello digital en claro. Solo existe en memoria y solo
/// durante el sellado.
/// </summary>
/// <param name="NumeroSerie">Número de serie del certificado; viaja en el XML.</param>
/// <param name="CertificadoCer">Contenido del archivo .cer.</param>
/// <param name="LlavePrivadaKey">Contenido del archivo .key, todavía protegido por su contraseña.</param>
/// <param name="ContrasenaLlave">Contraseña que abre la llave privada.</param>
/// <param name="VigenciaDesdeUtc">Inicio de vigencia del certificado, en UTC.</param>
/// <param name="VigenciaHastaUtc">Fin de vigencia del certificado, en UTC.</param>
public sealed record CsdDescifradoDto(
    string NumeroSerie,
    byte[] CertificadoCer,
    byte[] LlavePrivadaKey,
    string ContrasenaLlave,
    DateTime VigenciaDesdeUtc,
    DateTime VigenciaHastaUtc);
