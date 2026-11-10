namespace Facturacion.Shared.Mantenimiento;

public sealed record RevisionBaseDto(string Servidor, string Base, string VersionSql, bool PuedeActualizar,
    IReadOnlyList<string> ScriptsDisponibles, EstadoActualizacionBaseDto? UltimoIntento)
{
    public IReadOnlyList<string> RequisitosFaltantes { get; init; } = [];
}

public sealed record EstadoActualizacionBaseDto(Guid Id, string Nombre, string Sha256, string Estado,
    string Mensaje, string Base, Guid UsuarioId, DateTime InicioUtc, DateTime? FinUtc = null,
    int LoteActual = 0, int TotalLotes = 0, string Servidor = "");

public sealed record PeticionEjecutarActualizacionBase(Guid Id, string Contrasena, bool RespaldoConfirmado);
