using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>Permite devolver errores de negocio desde contratos que retornan un DTO directo. El middleware los transforma en Problem Details y los registra como advertencias.</summary>
public sealed class ErrorDeNegocioExcepcion(ErrorNegocio error)
    : Exception(error.Mensaje)
{
    public ErrorNegocio Error { get; } = error;
}
