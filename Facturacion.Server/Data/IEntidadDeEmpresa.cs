namespace Facturacion.Server.Data;

/// <summary>
/// Marca una entidad cuyos renglones pertenecen a una empresa. Toda entidad que la
/// implemente queda cubierta automáticamente por el filtro global de consulta y por el
/// sellado de <c>EmpresaId</c> al guardar. No hay que hacer nada más, y no se debe filtrar
/// a mano en un servicio ni en un endpoint (ARQUITECTURA.md §5).
/// <para>
/// <b>No</b> la implementan las tablas de Identity, <c>Cuenta</c>, <c>Empresa</c>,
/// <c>UsuarioEmpresa</c>, <c>UsuarioEmpresaPermiso</c>, <c>Permiso</c>, <c>RefreshToken</c>
/// ni los catálogos del SAT. El motivo de cada exclusión está en
/// <see cref="FiltroDeEmpresa"/>.
/// </para>
/// </summary>
public interface IEntidadDeEmpresa
{
    Guid EmpresaId { get; set; }
}

/// <summary>
/// Igual que <see cref="IEntidadDeEmpresa"/>, pero para entidades donde la empresa puede
/// no existir todavía. Hoy solo la bitácora: un inicio de sesión ocurre antes de que haya
/// empresa activa y aun así tiene que quedar registrado.
/// <para>
/// El filtro exige explícitamente que la columna no sea nula, así que esos renglones de
/// alcance de cuenta quedan invisibles mientras haya una empresa activa. Leerlos requiere
/// <c>IgnoreQueryFilters</c> con un filtro explícito por cuenta.
/// </para>
/// </summary>
public interface IEntidadDeEmpresaOpcional
{
    Guid? EmpresaId { get; set; }
}
