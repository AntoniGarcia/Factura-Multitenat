namespace Facturacion.Shared.Plataforma;

/// <summary>Saldo de la empresa activa, tal como lo pinta la tarjeta de la barra superior.</summary>
/// <param name="Disponibles">Utilizables ahora mismo.</param>
/// <param name="Reservados">Apartados por un timbrado en curso.</param>
public sealed record SaldoTimbresDto(int Disponibles, int Reservados);

/// <summary>
/// Un paquete a la venta. No lleva nada que el cliente pueda alterar para cambiar lo que
/// paga: el precio viaja solo para mostrarse, y al comprar se vuelve a leer del servidor.
/// </summary>
public sealed record PaqueteDto(
    Guid Id,
    string Nombre,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal PrecioTotal,
    int VigenciaMeses);

/// <summary>Lo único que manda el cliente para comprar: cuál paquete.</summary>
public sealed record PeticionDeCompra(Guid PaqueteId);

/// <summary>Una compra, con los datos del paquete ya copiados.</summary>
public sealed record CompraDto(
    Guid Id,
    string NombrePaquete,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal PrecioTotal,
    string Estado,
    DateTime CreadaUtc,
    DateTime? AcreditadaUtc,
    DateTime? VenceUtc);

/// <summary>Un renglón del historial de movimientos.</summary>
/// <param name="DeltaDisponible">Cuánto cambió el saldo disponible; negativo cuando baja.</param>
public sealed record MovimientoTimbreDto(
    Guid Id,
    string Tipo,
    int DeltaDisponible,
    int DeltaReservado,
    int DisponiblesDespues,
    string? Motivo,
    DateTime MomentoUtc);

/// <summary>
/// Estado de la suscripción de la cuenta.
/// </summary>
/// <param name="DiasParaVencer">
/// Negativo si ya venció. Se calcula en el servidor: el reloj del navegador puede estar mal
/// y de este número depende el aviso que decide si el usuario renueva a tiempo.
/// </param>
public sealed record MembresiaDto(
    DateTime InicioUtc,
    DateTime FinUtc,
    string Estado,
    int DiasParaVencer);
