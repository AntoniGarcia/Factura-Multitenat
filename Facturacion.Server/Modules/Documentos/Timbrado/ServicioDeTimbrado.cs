using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Egresos;
using Facturacion.Server.Modules.Documentos.Emision;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Server.Modules.Documentos.Obras;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>
/// Timbra un comprobante en tres pasos.
///
/// <para><b>Por qué tres y no uno</b></para>
/// El PAC puede tardar treinta segundos. Una transacción de base de datos abierta ese tiempo
/// bloquea renglones y tumba el sistema entero bajo carga, así que la llamada al PAC ocurre
/// <b>fuera de toda transacción</b>. Eso obliga a partir la operación:
///
/// <list type="number">
/// <item><description>
/// <b>Apartar</b> — transacción corta: se toma folio y timbre, el comprobante pasa a
/// <c>timbrando</c> y se abre un intento con su clave de idempotencia.
/// </description></item>
/// <item><description>
/// <b>Enviar</b> — sin transacción: se arma el XML, se sella y se manda al PAC, con
/// reintentos de espera creciente.
/// </description></item>
/// <item><description>
/// <b>Resolver</b> — transacción corta: se confirma o se revierte.
/// </description></item>
/// </list>
///
/// <para><b>El estado que no se resuelve es el importante</b></para>
/// Si el paso 2 no sabe qué pasó —red caída, tiempo agotado—, el comprobante <b>se queda</b>
/// en <c>timbrando</c> con su intento en vuelo. No se marca error: el PAC pudo haberlo
/// sellado y marcarlo como fallido llevaría a emitirlo dos veces. De ahí lo saca
/// <see cref="ConciliacionDeTimbrados"/>, preguntando con la misma clave.
/// </summary>
public sealed class ServicioDeTimbrado(
    AppDbContext baseDeDatos,
    ServicioDeXmlCfdi xml,
    IServicioFolios folios,
    IServicioTimbres timbres,
    CierreDeTimbrado cierre,
    IServicioDeBitacora bitacora,
    IContextoEmpresaInterno contexto,
    ServicioDePagos pagos,
    ServicioDeNotasCredito notasCredito,
    ILogger<ServicioDeTimbrado> registro,
    IProveedorPac? pac = null)
{
    /// <summary>Esperas entre reintentos. Crecientes: si el PAC va lento, insistir rápido lo empeora.</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)];

    public async Task<Resultado<Comprobante>> TimbrarAsync(Guid comprobanteId, CancellationToken ct)
    {
        // Antes de apartar nada. Si no hay PAC, fallar aquí no cuesta un folio ni un timbre;
        // fallar más adelante sí, y encima dejaría el comprobante en 'timbrando' esperando
        // una conciliación que tampoco tendría a quién preguntar.
        if (pac is null)
        {
            registro.LogError("Se intentó timbrar sin un PAC registrado.");

            return ErrorNegocio.Regla(
                "pac-no-configurado",
                "No hay un proveedor de timbrado configurado. Avisa a soporte: no se consumió " +
                "ningún timbre ni folio.");
        }

        // Antes de apartar: sin permiso para el tipo de documento no se gasta folio ni timbre.
        var clase = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Where(c => c.Id == comprobanteId)
            .Select(c => new { c.TipoDeComprobante, c.Variante })
            .FirstOrDefaultAsync(ct);

        if (clase is not null &&
            PermisoDeEmision.Exigir(contexto, PermisoDeEmision.Para(clase.TipoDeComprobante, clase.Variante, Permisos.Acciones.Timbrar)) is { } sinPermiso)
            return sinPermiso;

        var apartado = await ApartarAsync(comprobanteId, ct);

        if (apartado.EsFallo) return apartado.Error!;

        var (comprobante, intento) = apartado.Valor;

        // ── Paso 2: fuera de toda transacción ───────────────────────────────────────────
        string xmlSellado;

        try
        {
            var sellado = await xml.GenerarAsync(comprobante, ct);

            if (sellado.EsFallo)
            {
                await cierre.RevertirAsync(comprobante, intento, sellado.Error!.Codigo, sellado.Error.Mensaje, ct);
                return sellado.Error;
            }

            // El sello y el XML se guardan ANTES de enviar, no al confirmar. Si el proceso
            // muere a mitad de la llamada, la conciliación recupera el comprobante horas
            // después y para entonces ya no hay forma de recalcularlos: el XML se selló con la
            // fecha y el folio de este momento, y con el CSD que estuviera activo entonces.
            // Sin el sello el PDF no puede armar el QR —lleva sus últimos ocho caracteres— y
            // sin el XML la conciliación no tiene qué reenviar para preguntar.
            comprobante.SelloCfd = sellado.Valor.Sello;
            comprobante.NoCertificadoEmisor = sellado.Valor.NoCertificado;
            intento.XmlEnviado = sellado.Valor.Xml;
            await baseDeDatos.SaveChangesAsync(ct);

            xmlSellado = sellado.Valor.Xml;
        }
        catch (Exception ex)
        {
            // Cualquier excepción antes de enviar es nuestra, no del PAC: se puede revertir
            // sin riesgo de duplicar, porque el comprobante nunca salió.
            registro.LogError(ex, "Falló la preparación del timbrado de {Comprobante}.", comprobanteId);
            await cierre.RevertirAsync(comprobante, intento, "error-al-preparar", ex.Message, CancellationToken.None);

            return ErrorNegocio.Regla("error-al-preparar", "No se pudo preparar el comprobante para timbrar.");
        }

        // Una vez que se inicia el envío al PAC, nunca se revierte por excepción de red o
        // cancelación del cliente: el comprobante queda en 'timbrando' para conciliación.
        var respuesta = await EnviarConReintentosAsync(pac, xmlSellado, intento.ClaveIdempotencia!, ct);

        return await ResolverAsync(comprobante, intento, respuesta, ct);
    }

    // ── Paso 1 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<(Comprobante, IntentoTimbrado)>> ApartarAsync(
        Guid comprobanteId, CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        await BloqueoDePagos.TomarAsync(baseDeDatos, contexto.EmpresaActual
            ?? throw new InvalidOperationException("Falta empresa activa."), ct);
        var comprobante = await baseDeDatos.Comprobantes
            .Include(c => c.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(c => c.Relacionados)
            // Vacío salvo en los CFDI de pago, donde es todo el contenido del comprobante.
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        // Solo desde borrador o desde un error ya resuelto. Nunca desde timbrando: eso
        // significaría que hay otro intento en vuelo y timbrar de nuevo lo duplicaría.
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-timbrable",
                $"El comprobante está en '{comprobante.Estatus}' y no se puede timbrar desde ahí.");

        if (comprobante.Folio is null && comprobante.SerieId is { } seriePorValidar)
        {
            var serie = await folios.ValidarSerieAsync(seriePorValidar, comprobante.TipoDeComprobante, ct);
            if (serie.EsFallo) return serie.Error!;
        }

        if (ReglasDeObjetoImpuesto.ValidarCombinacion(comprobante.Conceptos.Select(c => c.ObjetoImp)) is { } mezcla)
            return ErrorNegocio.Validacion("objeto-05-no-combinable", mezcla);

        foreach (var concepto in comprobante.Conceptos)
            if (ReglasDeObjetoImpuesto.Validar(concepto.ObjetoImp,
                    concepto.Impuestos.Select(i => (i.Impuesto, i.EsRetencion))) is { } errorObjeto)
                return ErrorNegocio.Validacion("objeto-impuesto-invalido", $"Renglón {concepto.Orden}: {errorObjeto}");

        // Repite las reglas de guardado para borradores guardados antes de que existieran.
        if (comprobante.TipoDeComprobante is TiposDeComprobante.Ingreso or TiposDeComprobante.Egreso &&
            comprobante.Conceptos.OrderBy(c => c.Orden).FirstOrDefault(c => c.ValorUnitario <= 0) is { } sinPrecio)
            return ErrorNegocio.Validacion("precio-unitario-invalido",
                $"El precio unitario del renglón {sinPrecio.Orden} debe ser mayor que cero. Corrígelo y guarda el borrador.");

        // El borrador se guarda sin información global; el SAT la exige al timbrar.
        if (comprobante.TipoDeComprobante == TiposDeComprobante.Ingreso &&
            ReglaDeInformacionGlobal.Validar(comprobante.ReceptorRfc, comprobante.ReceptorNombre, comprobante.GlobalPeriodicidad) is { } sinGlobal)
            return sinGlobal;

        // Refresca la fecha de emisión al momento de timbrar para evitar rechazos CFDI40101 (>72 h)
        // en borradores creados días antes.
        comprobante.FechaEmisionUtc = DateTime.UtcNow;

        var obra = await baseDeDatos.DatosObra.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Ingreso)
        {
            var decimalesMoneda = await baseDeDatos.SatMonedas.AsNoTracking()
                .Where(x => x.Clave == comprobante.Moneda)
                .Select(x => (int?)x.Decimales)
                .FirstOrDefaultAsync(ct);
            if (decimalesMoneda is null)
                return ErrorNegocio.Validacion("moneda-desconocida",
                    "La moneda de la factura no está en el catálogo del SAT.");
            if (!ProyeccionMonetariaCfdi.Calcular(comprobante, decimalesMoneda.Value, obra)
                    .CoincideCon(comprobante))
                return ErrorNegocio.Regla("totales-del-borrador-desactualizados",
                    "Los totales guardados no coinciden con el XML en los decimales de la moneda. Guarda de nuevo el borrador antes de timbrarlo; no se consumió folio ni timbre.");
        }

        if (comprobante.Variante == VariantesDeFactura.Obra && obra is null)

            return ErrorNegocio.Regla("obra-sin-datos",
                "Guarda la estimación de obra antes de timbrar. No se reservó folio ni timbre.");
        if (obra is not null)
        {
            if (!await baseDeDatos.Empresas.AsNoTracking()
                .AnyAsync(x => x.Id == comprobante.EmpresaId && x.LicObras, ct))
                return ErrorNegocio.Regla("modulo-obras-no-contratado",
                    "Esta empresa no tiene activo el módulo de Constructoras.");
            if (ValidadorFiscalDeObra.Validar(comprobante, obra) is { } errorObra)
                return errorObra;
            if (await AnticipoDeObra.ValidarAsync(baseDeDatos, comprobante, obra.PorcentajeAmortizacion, ct) is { } errorAnticipo)
                return errorAnticipo;

            var validacionXmlObra = await xml.GenerarAsync(comprobante, ct);
            if (validacionXmlObra.EsFallo) return validacionXmlObra.Error!;
        }

        var tieneComercioExterior = await baseDeDatos.DatosComercioExterior.AsNoTracking()
            .AnyAsync(x => x.ComprobanteId == comprobanteId, ct);
        if (comprobante.Exportacion == "02" || tieneComercioExterior)
        {
            if (comprobante.Exportacion != "02" || !tieneComercioExterior)
                return ErrorNegocio.Validacion("comercio-datos-inconsistentes",
                    "La exportación definitiva necesita los datos del complemento de Comercio Exterior 2.0.");

            if (!await baseDeDatos.Empresas.AsNoTracking()
                .AnyAsync(x => x.Id == comprobante.EmpresaId && x.LicComercio, ct))
                return ErrorNegocio.Regla("modulo-comercio-no-contratado",
                    "Esta empresa no tiene activo el módulo de Comercio Exterior.");

            // Validar el CFDI completo antes de reservar folio o timbre evita consumirlos
            // por un complemento incompleto o por archivos SAT ausentes.
            var validacionComercio = await xml.GenerarAsync(comprobante, ct);
            if (validacionComercio.EsFallo) return validacionComercio.Error!;
        }

        var datosNotaria = await baseDeDatos.DatosNotaria
            .AsNoTracking()
            .Include(x => x.Inmuebles)
            .Include(x => x.Partes)
            .FirstOrDefaultAsync(x => x.ComprobanteId == comprobanteId, ct);

        if (datosNotaria is not null)
        {
            var licencia = await baseDeDatos.Empresas.AsNoTracking()
                .AnyAsync(x => x.Id == comprobante.EmpresaId && x.LicNotarios, ct);
            if (!licencia)
                return ErrorNegocio.Regla("modulo-notaria-no-contratado",
                    "Esta empresa no tiene activo el módulo de Notaría.");

            var notario = await baseDeDatos.ConfiguracionesNotario.AsNoTracking().FirstOrDefaultAsync(ct);
            if (GeneradorDeXmlNotaria.Validar(datosNotaria, notario) is { } errorNotaria)
                return errorNotaria;

            var validacionNotaria = await xml.GenerarAsync(comprobante, ct);
            if (validacionNotaria.EsFallo) return validacionNotaria.Error!;
        }

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Ingreso &&
            obra is null && datosNotaria is null && !tieneComercioExterior &&
            comprobante.Exportacion != "02")
        {
            var validacionFactura = await xml.GenerarAsync(comprobante, ct);
            if (validacionFactura.EsFallo) return validacionFactura.Error!;
        }

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Egreso)
        {
            var notaValida = await notasCredito.ValidarParaEmisionAsync(comprobante, ct);
            if (notaValida.EsFallo) return notaValida.Error!;
            var xmlValido = await xml.GenerarAsync(comprobante, ct);
            if (xmlValido.EsFallo) return xmlValido.Error!;
        }

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Pago)
        {
            var saldos = await pagos.ValidarSaldosParaEmisionAsync(comprobante, ct);
            if (saldos.EsFallo) return saldos.Error!;
            var validacionPago = await xml.GenerarAsync(comprobante, ct);
            if (validacionPago.EsFallo) return validacionPago.Error!;
        }

        if (comprobante.TipoDeComprobante == TiposDeComprobante.Traslado)
        {
            var validacionTraslado = await xml.GenerarAsync(comprobante, ct);
            if (validacionTraslado.EsFallo) return validacionTraslado.Error!;
        }

        var intento = new IntentoTimbrado
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobanteId,
            Numero = await baseDeDatos.IntentosTimbrado.CountAsync(i => i.ComprobanteId == comprobanteId, ct) + 1,
            IniciadoUtc = DateTime.UtcNow,
            Resultado = ResultadosDeIntento.EnVuelo,
            // Una clave nueva por intento de timbrado, no por comprobante: si el PAC rechazó
            // y el usuario corrigió, es otro documento y merece otra clave. Dentro de este
            // intento se reutiliza en cada reenvío y en la conciliación.
            ClaveIdempotencia = Guid.NewGuid().ToString("N")
        };

        baseDeDatos.IntentosTimbrado.Add(intento);

        var estatusAnterior = comprobante.Estatus;

        comprobante.Estatus = EstatusComprobante.Timbrando.ACadena();
        comprobante.ModificadoUtc = DateTime.UtcNow;

        // Se registra al apartar: si el PAC nunca responde, es lo único que dice quién mandó a timbrar.
        bitacora.Registrar(
            EntidadesDeBitacora.Comprobante, comprobante.Id.ToString(), AccionesDeBitacora.TimbradoIniciado,
            new { Estatus = estatusAnterior },
            new { comprobante.Estatus, Intento = intento.Numero, comprobante.Serie, comprobante.Folio, comprobante.Total });

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        await transaccion.DisposeAsync();

        // Los procedimientos dbo.ReservarTimbre y dbo.ReservarFolio manejan su propia
        // transacción con ROLLBACK interno; se invocan con @@TRANCOUNT = 0 para evitar el
        // error 266 de SQL Server. El comprobante ya está en 'timbrando', así que ninguna
        // petición concurrente puede volver a apartarlo.
        ReservaTimbreDto timbre;

        try
        {
            timbre = await timbres.ReservarAsync(comprobanteId, ct);
        }
        catch (ErrorDeNegocioExcepcion negocio)
        {
            await CompensarApartadoAsync(comprobante, intento, estatusAnterior, negocio.Error.Codigo, negocio.Error.Mensaje);
            registro.LogWarning("No se pudo apartar timbre para {Comprobante}: {Codigo}", comprobanteId, negocio.Error.Codigo);
            return negocio.Error;
        }
        catch (Exception ex)
        {
            await CompensarApartadoAsync(comprobante, intento, estatusAnterior, "error-al-reservar-timbre", ex.Message);
            registro.LogError(ex, "Error inesperado al apartar timbre para {Comprobante}.", comprobanteId);
            return ErrorNegocio.Regla("error-al-reservar-timbre", "No se pudo apartar el timbre. Inténtalo de nuevo.");
        }

        // Un reintento conserva el folio del intento anterior; no reserva otro.
        if (comprobante.Folio is null && comprobante.SerieId is { } serieId)
        {
            try
            {
                var folio = await folios.ReservarAsync(serieId, ct);
                comprobante.SerieId = folio.SerieId;
                comprobante.Serie = folio.Serie;
                comprobante.Folio = folio.Folio;
                comprobante.ModificadoUtc = DateTime.UtcNow;
                await baseDeDatos.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                try
                {
                    await timbres.DevolverAsync(timbre.ReservaId, "error-al-reservar-folio", CancellationToken.None);
                }
                catch (Exception exDevolver)
                {
                    registro.LogError(exDevolver, "No se pudo devolver el timbre {Reserva} tras fallar el folio.", timbre.ReservaId);
                }

                await CompensarApartadoAsync(comprobante, intento, estatusAnterior, "error-al-reservar-folio", ex.Message);
                registro.LogError(ex, "No se pudo apartar folio para {Comprobante}.", comprobanteId);
                return ErrorNegocio.Regla("error-al-reservar-folio", "No se pudo apartar el folio para el comprobante. No se consumió ningún timbre.");
            }
        }

        registro.LogInformation(
            "Comprobante {Comprobante} apartado para timbrar. Intento {Numero}, timbres restantes {Restantes}.",
            comprobanteId, intento.Numero, timbre.DisponiblesDespues);

        return (comprobante, intento);
    }

    private async Task CompensarApartadoAsync(
        Comprobante comprobante, IntentoTimbrado intento, string estatusAnterior, string codigo, string mensaje)
    {
        comprobante.Estatus = estatusAnterior;
        comprobante.ModificadoUtc = DateTime.UtcNow;
        intento.Resultado = ResultadosDeIntento.Rechazado;
        intento.TerminadoUtc = DateTime.UtcNow;
        intento.DuracionMs = (int)(intento.TerminadoUtc.Value - intento.IniciadoUtc).TotalMilliseconds;
        intento.CodigoError = codigo;
        intento.MensajeError = mensaje;
        await baseDeDatos.SaveChangesAsync(CancellationToken.None);
    }

    // ── Paso 2 ──────────────────────────────────────────────────────────────────────────

    // El PAC entra por parámetro y no por el campo: TimbrarAsync ya comprobó que existe, y
    // pasarlo deja esa garantía a la vista en vez de confiarla a un comentario.
    private async Task<RespuestaDePac> EnviarConReintentosAsync(
        IProveedorPac proveedor, string documento, string clave, CancellationToken ct)
    {
        RespuestaDePac respuesta = new(ResultadoDePac.ErrorDeComunicacion, Mensaje: "No se intentó.");

        for (var intento = 0; intento <= Esperas.Length; intento++)
        {
            if (intento > 0)
            {
                try
                {
                    await Task.Delay(Esperas[intento - 1], ct);
                }
                catch (OperationCanceledException)
                {
                    // El primer envío ya salió al PAC: cancelar la espera no permite dar el
                    // timbrado por fallido; queda en comunicación incierta para conciliación.
                    return respuesta;
                }
            }

            try
            {
                respuesta = await proveedor.TimbrarAsync(documento, clave, ct);
            }
            catch (Exception ex)
            {
                registro.LogWarning(ex, "Error de comunicación con el PAC, envío {Numero}.", intento + 1);
                respuesta = new RespuestaDePac(ResultadoDePac.ErrorDeComunicacion, Mensaje: ex.Message);
            }

            // Solo se reintenta lo que puede cambiar de resultado. Un rechazo es del
            // comprobante: insistir da el mismo rechazo y gasta tiempo del usuario.
            if (respuesta.Resultado != ResultadoDePac.ErrorDeComunicacion) return respuesta;
        }

        return respuesta;
    }

    // ── Paso 3 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<Comprobante>> ResolverAsync(
        Comprobante comprobante, IntentoTimbrado intento, RespuestaDePac respuesta, CancellationToken ct)
    {
        switch (respuesta.Resultado)
        {
            case ResultadoDePac.Timbrado:
                await cierre.ConfirmarAsync(comprobante, intento, respuesta, ct);
                return comprobante;

            case ResultadoDePac.Rechazado:
                await cierre.RevertirAsync(comprobante, intento, respuesta.CodigoError, respuesta.Mensaje, ct);
                return ErrorNegocio.Regla(
                    "pac-rechazo",
                    $"El PAC rechazó el comprobante: {respuesta.Mensaje ?? "sin detalle"}");

            default:
                // Ni se confirma ni se revierte: no sabemos si el PAC lo selló. Se queda en
                // 'timbrando' con el intento en vuelo, y la conciliación lo resuelve.
                registro.LogError(
                    "Timbrado de {Comprobante} sin resolver tras {Envios} envíos. Queda para conciliación.",
                    comprobante.Id, Esperas.Length + 1);

                return ErrorNegocio.Regla(
                    "timbrado-sin-confirmar",
                    "No se pudo confirmar el timbrado con el PAC. El comprobante quedó en proceso y " +
                    "el sistema lo resolverá automáticamente; no lo vuelvas a timbrar.");
        }
    }
}
