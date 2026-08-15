using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>
/// Transporta un <see cref="ErrorNegocio"/> desde un servicio cuyo contrato no tiene dónde
/// devolverlo.
///
/// <para><b>Por qué existe si el sistema evita las excepciones para el negocio</b></para>
/// En casi todo el sistema un error de negocio viaja dentro de un <see cref="Resultado{T}"/>,
/// que es lo correcto: quedarse sin timbres no es una condición excepcional. Pero
/// <see cref="Facturacion.Shared.Contratos.IServicioTimbres.ReservarAsync"/> está en el
/// contrato congelado de la fase 0 y devuelve el DTO directo, sin canal de error. Cambiar esa
/// firma rompería a la mitad B, que ya la tiene escrita.
///
/// <para>
/// Así que el error se lanza, pero <b>no</b> se comporta como una falla: el middleware lo
/// convierte exactamente en el mismo Problem Details que produciría
/// <see cref="ResultadosDeError.AResultado"/>, con su código estable y su estado HTTP, y lo
/// registra como advertencia y no como error. Para el cliente, y para el log, es
/// indistinguible de un error de negocio devuelto por cualquier otro camino.
/// </para>
/// </summary>
public sealed class ErrorDeNegocioExcepcion(ErrorNegocio error)
    : Exception(error.Mensaje)
{
    public ErrorNegocio Error { get; } = error;
}
