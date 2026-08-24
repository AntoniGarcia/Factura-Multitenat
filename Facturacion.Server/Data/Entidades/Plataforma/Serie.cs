namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Serie de folios de una empresa. <see cref="FolioActual"/> es el último folio entregado;
/// el siguiente sale de sumarle uno <b>dentro del procedimiento almacenado</b>, con bloqueo
/// de renglón. Nunca con <c>SELECT MAX(Folio)+1</c> (ARQUITECTURA.md §5).
/// <para>
/// Ninguna parte del código de la aplicación debe escribir <see cref="FolioActual"/>: si se
/// pudiera actualizar desde C#, dos peticiones simultáneas entregarían el mismo folio y el
/// SAT rechazaría el segundo comprobante por duplicado.
/// </para>
/// </summary>
public sealed class Serie : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>Prefijo que va en el atributo <c>Serie</c> del comprobante; hasta 25 caracteres.</summary>
    public required string Prefijo { get; set; }

    /// <summary>Desde dónde empieza a contar. Se conserva para poder explicar la numeración después.</summary>
    public int FolioInicial { get; set; }

    /// <summary>Último folio entregado. Lo mueve solo el procedimiento almacenado de reserva.</summary>
    public int FolioActual { get; set; }

    /// <summary>Clave de <c>c_TipoDeComprobante</c> a la que aplica la serie (I, E, T, N, P).</summary>
    public required string TipoComprobante { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }
}
