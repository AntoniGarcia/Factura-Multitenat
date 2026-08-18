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

/// <summary>
/// El tablero ya compuesto por el servidor.
///
/// <para><b>Por qué se compone en el servidor y no con cuatro llamadas del Client</b></para>
/// Cada pieza vive detrás de un permiso distinto. Si el Client pidiera las cuatro por su
/// cuenta, a un capturista sin <c>configurar_empresa</c> el tablero le respondería 403 en
/// una de ellas y tendría que decidir en el navegador qué ignorar. Aquí el servidor decide
/// qué puede ver quien pregunta, y de paso la pantalla se pinta con una sola petición, que
/// en WebAssembly no es un detalle.
/// </summary>
/// <param name="UmbralAvisoTimbres">
/// A partir de cuántos timbres restantes se avisa. Viaja desde el servidor para que el
/// Client no lo lleve compilado: cambiarlo no debe exigir recompilar el wasm.
/// </param>
/// <param name="Certificado">
/// <c>null</c> cuando el usuario no tiene <c>configurar_empresa</c>. El endpoint de
/// certificados exige ese permiso, así que el tablero no enseña por la puerta de atrás lo
/// que esa política niega por la de enfrente.
/// </param>
/// <param name="Membresia">
/// <c>null</c> si la cuenta no tiene ninguna membresía registrada.
/// </param>
/// <param name="MembresiaEnAviso">
/// Si hay que avisar del vencimiento. Lo decide el servidor con su propio plazo, en vez de
/// que el Client repita el número: ya está duplicado entre <c>ServicioDeCompras</c> y la
/// pantalla de la bolsa, y una tercera copia acabaría avisando en un plazo distinto que las
/// otras dos.
/// </param>
/// <param name="Documentos">
/// Comprobantes del mes en curso. <c>null</c> si la mitad B todavía no tiene registrada su
/// implementación de <c>IResumenDocumentos</c>: entonces el recuadro no se pinta, en vez de
/// enseñar ceros que se leerían como «este mes no facturaste».
/// </param>
public sealed record TableroDto(
    SaldoTimbresDto Timbres,
    int UmbralAvisoTimbres,
    AvisoCertificadoDto? Certificado,
    MembresiaDto? Membresia,
    bool MembresiaEnAviso,
    ResumenDelMesDto? Documentos);

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
