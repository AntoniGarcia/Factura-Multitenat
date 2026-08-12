using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Extiende el contrato compartido con lo que la mitad A necesita y la mitad B no.
/// <para>
/// <see cref="IContextoEmpresa.EmpresaId"/> es un <see cref="Guid"/> no nulo porque la mitad B
/// siempre corre con empresa activa. La mitad A, en cambio, atiende peticiones que ocurren
/// antes de elegir empresa —iniciar sesión, listar empresas, cambiar de empresa— y necesita
/// poder preguntar por la empresa sin que consultarla reviente.
/// </para>
/// <para>
/// Esta interfaz vive en el servidor y no en <c>Shared/Contratos</c>: el contrato congelado
/// no cambia.
/// </para>
/// </summary>
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
