namespace Facturacion.Shared.Comun;

/// <summary>
/// Clase de error de negocio. El servidor la traduce a un código HTTP; el cliente la usa
/// para decidir si muestra el error junto a un campo o como aviso general.
/// </summary>
public enum TipoErrorNegocio
{
    /// <summary>Los datos recibidos no pasan validación. Se traduce a 400.</summary>
    Validacion,

    /// <summary>El recurso no existe o no pertenece a la empresa activa. Se traduce a 404.</summary>
    NoEncontrado,

    /// <summary>Choca con el estado actual: duplicado, concurrencia. Se traduce a 409.</summary>
    Conflicto,

    /// <summary>El usuario está autenticado pero no tiene el permiso. Se traduce a 403.</summary>
    SinPermiso,

    /// <summary>Los datos son válidos pero una regla del negocio lo impide. Se traduce a 422.</summary>
    ReglaDeNegocio,

    /// <summary>Se agotó un cupo: timbres, auxiliares, intentos. Se traduce a 429.</summary>
    LimiteExcedido
}
