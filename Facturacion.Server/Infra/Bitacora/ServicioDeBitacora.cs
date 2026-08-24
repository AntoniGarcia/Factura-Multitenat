using System.Diagnostics;
using System.Text.Json;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;

namespace Facturacion.Server.Infra.Bitacora;

/// <summary>
/// Escribe la bitácora. Toda operación que cambie datos fiscales o de facturación, y todo
/// evento de identidad, deja aquí quién, en qué empresa, cuándo y con qué valores
/// (ARQUITECTURA.md §5).
/// </summary>
public interface IServicioDeBitacora
{
    /// <summary>
    /// Agrega un registro al contexto. <b>No guarda</b>: se persiste con el
    /// <c>SaveChanges</c> de la operación que lo generó, para que el registro y el cambio
    /// entren o no entren juntos.
    /// </summary>
    /// <param name="antes">Estado anterior; se serializa a JSON. Nulo en un alta.</param>
    /// <param name="despues">Estado nuevo; se serializa a JSON.</param>
    /// <param name="empresaId">
    /// Empresa del evento. Si se omite se toma la activa, que puede no existir: un inicio
    /// de sesión ocurre antes de que haya empresa.
    /// </param>
    void Registrar(
        string entidad,
        string? entidadId,
        string accion,
        object? antes = null,
        object? despues = null,
        Guid? empresaId = null,
        Guid? usuarioId = null,
        Guid? cuentaId = null);
}

public sealed class ServicioDeBitacora(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IHttpContextAccessor accesor) : IServicioDeBitacora
{
    public void Registrar(
        string entidad,
        string? entidadId,
        string accion,
        object? antes = null,
        object? despues = null,
        Guid? empresaId = null,
        Guid? usuarioId = null,
        Guid? cuentaId = null)
    {
        baseDeDatos.Bitacora.Add(new RegistroBitacora
        {
            EmpresaId = empresaId ?? contexto.EmpresaActual,
            CuentaId = cuentaId ?? contexto.CuentaActual,
            UsuarioId = usuarioId ?? contexto.UsuarioActual,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            ValorAnterior = ASerie(antes),
            ValorNuevo = ASerie(despues),
            MomentoUtc = DateTime.UtcNow,
            TraceId = Activity.Current?.Id ?? accesor.HttpContext?.TraceIdentifier,
            IpOrigen = accesor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
    }

    private static string? ASerie(object? valor) => valor is null ? null : JsonSerializer.Serialize(valor);
}
