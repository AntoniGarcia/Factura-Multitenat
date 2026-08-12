namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Punto de entrada de la mitad B: emisión, impuestos, timbrado, PAC, XML, PDF, correo,
/// cancelación y complemento de pagos.
/// <para>
/// <b>Este archivo pertenece a la mitad B.</b> Lo creó la fase 0 vacío, solo porque
/// <c>Program.cs</c> tiene que poder llamarlo. A partir de aquí lo escribe su dueño; la
/// mitad A no vuelve a tocarlo (REPARTO-EQUIPO.md §4).
/// </para>
/// </summary>
public static class DocumentosModule
{
    public static IServiceCollection AddDocumentos(
        this IServiceCollection servicios, IConfiguration configuracion) => servicios;

    public static WebApplication MapDocumentos(this WebApplication aplicacion) => aplicacion;
}
