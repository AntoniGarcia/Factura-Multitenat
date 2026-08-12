namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Bitácora: toda operación que cambie datos fiscales o de facturación deja aquí quién,
/// en qué empresa, cuándo, y los valores anterior y nuevo (CLAUDE.md §5).
/// <para>
/// La empresa es opcional porque hay eventos de alcance de cuenta —inicio de sesión,
/// rotación de token, alta de empresa— que ocurren antes de que exista empresa activa.
/// Esos renglones quedan invisibles bajo el filtro global; leerlos exige
/// <c>IgnoreQueryFilters</c> con filtro explícito por cuenta.
/// </para>
/// <para>
/// Un renglón de bitácora no se modifica ni se borra nunca.
/// </para>
/// </summary>
public sealed class RegistroBitacora : IEntidadDeEmpresaOpcional
{
    /// <summary>Entero autoincremental: esta tabla crece mucho y se lee por rangos de tiempo.</summary>
    public long Id { get; set; }

    public Guid? EmpresaId { get; set; }

    public Guid? CuentaId { get; set; }

    /// <summary>Nulo solo cuando el evento lo genera el sistema, no una persona.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Nombre de la entidad afectada, por ejemplo <c>Cliente</c>.</summary>
    public required string Entidad { get; set; }

    public string? EntidadId { get; set; }

    /// <summary>Qué se hizo: <c>alta</c>, <c>cambio</c>, <c>baja</c>, <c>inicio_sesion</c>…</summary>
    public required string Accion { get; set; }

    /// <summary>Estado anterior en JSON. Nulo en un alta.</summary>
    public string? ValorAnterior { get; set; }

    /// <summary>Estado nuevo en JSON. Nulo en una baja física, que solo aplica a borradores.</summary>
    public string? ValorNuevo { get; set; }

    public DateTime MomentoUtc { get; set; }

    /// <summary>Traza de la petición que lo originó; empata con el <c>traceId</c> del error mostrado.</summary>
    public string? TraceId { get; set; }

    public string? IpOrigen { get; set; }
}
