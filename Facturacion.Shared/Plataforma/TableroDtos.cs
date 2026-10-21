namespace Facturacion.Shared.Plataforma;

/// <summary>
/// Estado del certificado de sello digital activo, para el aviso del tablero.
/// </summary>
/// <param name="HayActivo">
/// Con <c>false</c>, la empresa no tiene ningún CSD cargado y eso mismo es el aviso: sin
/// certificado no se puede timbrar nada.
/// </param>
/// <param name="DiasParaCaducar">Negativo si ya caducó. Lo calcula el servidor.</param>
/// <param name="PorCaducar">
/// Contra el plazo de aviso configurado en la empresa, no contra un número fijo del Client.
/// </param>
public sealed record AvisoCertificadoDto(
    bool HayActivo,
    DateTime? VigenciaHastaUtc,
    int DiasParaCaducar,
    bool PorCaducar);

/// <summary>Datos del tablero filtrados por el servidor según los permisos del usuario.</summary>
/// <param name="UmbralAvisoTimbres">Saldo a partir del cual se muestra el aviso.</param>
/// <param name="Certificado">Nulo si el usuario no tiene permiso para consultar los datos de empresa.</param>
/// <param name="Membresia">Nula si la cuenta no tiene una membresía registrada.</param>
/// <param name="MembresiaEnAviso">Aviso de vencimiento calculado por el servidor.</param>
/// <param name="Documentos">Resumen del mes; nulo si el servicio de resumen no está disponible.</param>
/// <param name="SerieMensual">Importes y comprobantes de los últimos meses; vacía si el servicio no está disponible.</param>
public sealed record TableroDto(
    SaldoTimbresDto Timbres,
    int UmbralAvisoTimbres,
    AvisoCertificadoDto? Certificado,
    MembresiaDto? Membresia,
    bool MembresiaEnAviso,
    ResumenDelMesDto? Documentos,
    IReadOnlyList<PuntoDeFacturacionDto> SerieMensual);

/// <summary>Un mes de la serie de facturacion del tablero.</summary>
public sealed record PuntoDeFacturacionDto(
    string Etiqueta,
    int Timbrados,
    decimal Importe);

/// <summary>
/// Conteo de comprobantes del periodo, aplanado para el tablero.
/// </summary>
/// <param name="ConteoPorEstatus">
/// Clave y cifra ya listas para pintar, en el orden en que conviene leerlas. Se aplana aquí
/// y no se manda el diccionario del contrato porque el Client no tiene por qué conocer el
/// enum de estatus ni decidir su orden de presentación.
/// </param>
public sealed record ResumenDelMesDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ConteoPorEstatusDto> ConteoPorEstatus,
    decimal ImporteTimbrado,
    decimal ImporteCancelado);

/// <param name="Estatus">Clave del estatus, tal como viaja en el resto del sistema.</param>
public sealed record ConteoPorEstatusDto(string Estatus, string Etiqueta, int Cuenta);
