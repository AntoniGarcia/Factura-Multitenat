using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>Permite consultar la empresa activa antes de seleccionarla, sin modificar el contrato compartido.</summary>
public interface IContextoEmpresaInterno : IContextoEmpresa
{
    /// <summary>Empresa activa, o <c>null</c> si la petición todavía no tiene una.</summary>
    Guid? EmpresaActual { get; }

    /// <summary>Cuenta contratante del usuario, o <c>null</c> si no hay usuario autenticado.</summary>
    Guid? CuentaActual { get; }

    /// <summary>Usuario autenticado, o <c>null</c> si la petición es anónima.</summary>
    Guid? UsuarioActual { get; }

    bool HayEmpresa { get; }
}
