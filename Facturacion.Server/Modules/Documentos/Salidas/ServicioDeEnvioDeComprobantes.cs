using System.Net.Mail;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Envío de un CFDI timbrado por correo (B6, §1.3 del documento funcional).
///
/// <para><b>Quién envía</b></para>
/// El SMTP propio de la empresa si lo tiene habilitado; si no, el del SaaS. En los dos casos
/// <c>Reply-To</c> va al correo de la empresa emisora (AGENTS.md §6 y §11). Si el servidor de
/// la empresa falla, el envío falla: no se reintenta por el del SaaS.
///
/// <para><b>Qué se adjunta</b></para>
/// Los mismos archivos que la descarga, generados por <see cref="ServicioDeSalidasFiscales"/>:
/// el XML tal como lo selló el PAC y el PDF que sale de él. Así lo que recibe el cliente y lo
/// que descarga el contador no pueden diferir ni en el nombre del archivo.
///
/// <para><b>Sin transacción alrededor del SMTP</b></para>
/// El servidor de correo puede tardar; el registro del envío se guarda después, en su propia
/// escritura corta (ARQUITECTURA.md §5).
/// </summary>
public sealed class ServicioDeEnvioDeComprobantes(
    AppDbContext baseDeDatos,
    ServicioDeSalidasFiscales salidas,
    IServicioDeCorreoDeEmpresa correo,
    IServicioEmpresaEmisora empresaEmisora,
    IServicioClientes clientes,
    IContextoEmpresaInterno contexto,
    HusoDeEmpresa huso,
    ILogger<ServicioDeEnvioDeComprobantes> registro)
{
    /// <summary>El del sistema anterior; se le agrega el folio para que se distinga en la bandeja.</summary>
    private const string AsuntoPredeterminado = "Documento CFDI";

    private const int EnviosEnHistorial = 20;

    public async Task<Resultado<PropuestaDeEnvioDto>> ObtenerPropuestaAsync(Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await CargarAsync(comprobanteId, ct);

        if (comprobante.EsFallo) return comprobante.Error!;

        var c = comprobante.Valor;
        var emisor = await empresaEmisora.ObtenerParaTimbradoAsync(ct);
        var zona = await huso.ObtenerAsync(ct);

        return new PropuestaDeEnvioDto(
            await DestinatariosDelClienteAsync(c.ClienteId, ct),
            $"{AsuntoPredeterminado} {Folio(c)}".TrimEnd(),
            string.IsNullOrWhiteSpace(emisor.CorreoContacto) ? null : emisor.CorreoContacto,
            NombreDeArchivoFiscal.Construir(c, zona, "pdf"),
            NombreDeArchivoFiscal.Construir(c, zona, "xml"),
            await HistorialAsync(comprobanteId, ct));
    }

    public async Task<Resultado<EnvioDeCorreoDto>> EnviarAsync(
        Guid comprobanteId, PeticionDeEnvio peticion, CancellationToken ct)
    {
        var comprobante = await CargarAsync(comprobanteId, ct);

        if (comprobante.EsFallo) return comprobante.Error!;

        var c = comprobante.Valor;

        var destinatarios = ValidarDestinatarios(peticion.Destinatarios);
        if (destinatarios.EsFallo) return destinatarios.Error!;

        var asunto = (peticion.Asunto ?? string.Empty).Trim();

        if (asunto.Length == 0)
            return ErrorNegocio.Validacion("asunto-requerido", "Escribe el asunto del correo.");

        // Un salto de línea en el asunto es cómo se inyectan encabezados en un correo.
        if (asunto.Length > ReglasDeEnvio.LongitudMaximaAsunto || asunto.Contains('\r') || asunto.Contains('\n'))
            return ErrorNegocio.Validacion("asunto-invalido",
                $"El asunto admite hasta {ReglasDeEnvio.LongitudMaximaAsunto} caracteres en una sola línea.");

        if (peticion.Mensaje?.Length > ReglasDeEnvio.LongitudMaximaMensaje)
            return ErrorNegocio.Validacion("mensaje-demasiado-largo",
                $"El mensaje admite hasta {ReglasDeEnvio.LongitudMaximaMensaje} caracteres.");

        var emisor = await empresaEmisora.ObtenerParaTimbradoAsync(ct);
        var correoDeLaEmpresa = string.IsNullOrWhiteSpace(emisor.CorreoContacto) ? null : emisor.CorreoContacto.Trim();

        if (peticion.CopiaALaEmpresa && correoDeLaEmpresa is null)
            return ErrorNegocio.Validacion("empresa-sin-correo",
                "La empresa no tiene correo de contacto. Agrégalo en los datos de la empresa para mandarle copia.");

        var pdf = await salidas.GenerarPdfAsync(comprobanteId, ct);
        if (pdf.EsFallo) return pdf.Error!;

        var adjuntos = new List<AdjuntoDeCorreo> { AAdjunto(pdf.Valor) };

        if (peticion.IncluirXml)
        {
            var xml = await salidas.ObtenerXmlAsync(comprobanteId, ct);
            if (xml.EsFallo) return xml.Error!;

            adjuntos.Add(AAdjunto(xml.Valor));
        }

        var copiaOculta = peticion.CopiaALaEmpresa ? correoDeLaEmpresa : null;

        // La copia va siempre al correo de contacto; las respuestas, a donde la empresa las haya
        // dirigido en Configuración.
        var responderA = await correo.ResponderAAsync(correoDeLaEmpresa, ct);

        var mensaje = new MensajeDeCorreo(
            destinatarios.Valor,
            asunto,
            Cuerpo(c, peticion.Mensaje, responderA is not null),
            ResponderA: responderA,
            CopiaOculta: copiaOculta is null ? null : [copiaOculta],
            Adjuntos: adjuntos);

        string? error = null;
        FalloDeCorreoDeEmpresa? falloDeLaEmpresa = null;

        try
        {
            await correo.EnviarAsync(mensaje, ct);
        }
        catch (FalloDeCorreoDeEmpresa ex)
        {
            // Ya lo registró ServicioDeCorreoDeEmpresa; aquí solo se conserva para el usuario.
            falloDeLaEmpresa = ex;
            error = ex.Codigo;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Las direcciones no van al log: bastan el comprobante y el traceId de la petición.
            registro.LogError(ex, "No se pudo enviar por correo el comprobante {Comprobante}.", comprobanteId);

            error = ex is SmtpException smtp ? $"smtp-{(int)smtp.StatusCode}" : "error-inesperado";
        }

        var envio = new EnvioDeCorreo
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobanteId,
            Destinatarios = string.Join(';', destinatarios.Valor),
            CopiaOculta = copiaOculta,
            Asunto = asunto,
            IncluyoXml = peticion.IncluirXml,
            Exitoso = error is null,
            Error = error,
            EnviadoPorUsuarioId = contexto.UsuarioActual ?? Guid.Empty,
            EnviadoUtc = DateTime.UtcNow
        };

        baseDeDatos.EnviosDeCorreo.Add(envio);
        await baseDeDatos.SaveChangesAsync(ct);

        if (falloDeLaEmpresa is not null)
            return ErrorNegocio.Regla(falloDeLaEmpresa.Codigo,
                $"{falloDeLaEmpresa.Message} El intento quedó registrado; revisa la pestaña Correo en Configuración.");

        if (error is not null)
            return ErrorNegocio.Regla("correo-no-enviado",
                "No se pudo enviar el correo. El intento quedó registrado; vuelve a intentarlo en unos " +
                "minutos o avisa a soporte con el código de rastreo.");

        return ADto(envio);
    }

    // ── Validación ──────────────────────────────────────────────────────────────────────

    private async Task<Resultado<Comprobante>> CargarAsync(Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        // Un borrador no es un CFDI, y uno cancelado o en cancelación ya no se le entrega a nadie
        // como si estuviera vigente.
        if (comprobante.Estatus != EstatusComprobante.Timbrado.ACadena() || comprobante.Uuid is null)
            return ErrorNegocio.Conflicto("comprobante-no-enviable",
                "Solo se pueden enviar por correo comprobantes timbrados y vigentes.");

        return comprobante;
    }

    /// <summary>
    /// Direcciones limpias, sin repetir y sin nombre para mostrar: «Juan &lt;x@y.mx&gt;» se rechaza
    /// porque lo que se guarda y se muestra en el historial tiene que ser exactamente a dónde fue.
    /// </summary>
    private static Resultado<IReadOnlyList<string>> ValidarDestinatarios(IReadOnlyList<string>? destinatarios)
    {
        var limpias = (destinatarios ?? [])
            .Select(d => d?.Trim() ?? string.Empty)
            .Where(d => d.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (limpias.Count == 0)
            return ErrorNegocio.Validacion("destinatario-requerido", "Escribe al menos un correo destinatario.");

        if (limpias.Count > ReglasDeEnvio.MaximoDestinatarios)
            return ErrorNegocio.Validacion("demasiados-destinatarios",
                $"Se puede enviar a un máximo de {ReglasDeEnvio.MaximoDestinatarios} correos a la vez.");

        foreach (var direccion in limpias)
        {
            if (direccion.Length > 254 ||
                !MailAddress.TryCreate(direccion, out var valida) ||
                !string.Equals(valida.Address, direccion, StringComparison.OrdinalIgnoreCase))
                return ErrorNegocio.Validacion("destinatario-invalido", $"«{direccion}» no es un correo válido.");
        }

        return limpias;
    }

    // ── Apoyo ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El correo principal del cliente. Se admite que traiga varios separados por coma o punto
    /// y coma, que es como suelen capturarlos quienes vienen del sistema anterior.
    /// </summary>
    private async Task<IReadOnlyList<string>> DestinatariosDelClienteAsync(Guid? clienteId, CancellationToken ct)
    {
        if (clienteId is not { } id) return [];

        var receptor = await clientes.ObtenerParaTimbradoAsync(id, ct);

        return receptor?.CorreoPrincipal is { } correos
            ? correos.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
    }

    private async Task<IReadOnlyList<EnvioDeCorreoDto>> HistorialAsync(Guid comprobanteId, CancellationToken ct)
    {
        var envios = await baseDeDatos.EnviosDeCorreo
            .AsNoTracking()
            .Where(e => e.ComprobanteId == comprobanteId)
            .OrderByDescending(e => e.EnviadoUtc)
            .Take(EnviosEnHistorial)
            .ToListAsync(ct);

        return [.. envios.Select(ADto)];
    }

    private static string Cuerpo(Comprobante c, string? mensajeDelUsuario, bool respondeALaEmpresa)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(mensajeDelUsuario))
            partes.Add(PlantillaDeCorreo.Renderizar(mensajeDelUsuario));

        var documento = c.TipoDeComprobante == "P" ? "el complemento de pago" : "el comprobante fiscal";
        var folio = Folio(c);

        partes.Add(
            $"<p>Te enviamos {documento}{(folio.Length > 0 ? $" {E(folio)}" : string.Empty)} " +
            $"emitido por {E(c.EmisorNombre)} (RFC {E(c.EmisorRfc)}) a {E(c.ReceptorNombre)}.</p>");

        partes.Add($"<p>Folio fiscal: {E(c.Uuid!.Value.ToString().ToUpperInvariant())}</p>");

        if (respondeALaEmpresa)
            partes.Add($"<p>Si tienes alguna duda, responde a este correo: la respuesta le llega a {E(c.EmisorNombre)}.</p>");

        return string.Join("\n", partes);
    }

    private static string E(string texto) => PlantillaDeCorreo.Escapar(texto);

    private static string Folio(Comprobante c)
        => c.Folio is null ? string.Empty : string.IsNullOrEmpty(c.Serie) ? c.Folio.Value.ToString() : $"{c.Serie}-{c.Folio}";

    private static AdjuntoDeCorreo AAdjunto(ArchivoFiscal archivo)
        => new(archivo.Nombre, archivo.Contenido, archivo.TipoContenido);

    private static EnvioDeCorreoDto ADto(EnvioDeCorreo e) => new(
        e.Id,
        e.EnviadoUtc,
        e.Destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries),
        e.CopiaOculta,
        e.Asunto,
        e.IncluyoXml,
        e.Exitoso,
        e.Error);
}
