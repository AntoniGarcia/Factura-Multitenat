using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Tablero;

/// <summary>
/// Arma el tablero de inicio.
///
/// <para><b>El conteo de comprobantes es opcional a propósito</b></para>
/// Lo aporta <c>IResumenDocumentos</c>, el único contrato que va de la mitad B hacia la A.
/// Se resuelve como <c>IResumenDocumentos?</c> y no como dependencia obligatoria: mientras
/// la mitad B no lo registre, el recuadro simplemente no se pinta. Rellenarlo con ceros
/// sería peor que omitirlo — un cero se lee como «este mes no facturaste», que es una
/// afirmación distinta de «esto aún no existe».
/// </summary>
public sealed class ServicioDeTablero(
    ServicioDeCompras compras,
    ServicioDeCsd csd,
    IContextoEmpresaInterno contexto,
    HusoDeEmpresa huso,
    IResumenDocumentos? documentos = null)
{
    /// <summary>
    /// Timbres restantes a partir de los cuales se avisa. Lo fija la fase 9 del prompt.
    /// Viaja al Client dentro del DTO en vez de estar compilado allá.
    /// </summary>
    private const int UmbralAvisoTimbres = 50;

    public async Task<Resultado<TableroDto>> ObtenerAsync(CancellationToken ct)
    {
        // El layout del Client ya manda a elegir empresa antes de pintar esto, pero la API
        // se puede llamar directo. Sin esta guarda, EmpresaId lanza y el usuario recibe un
        // 500 en vez de un error que explique qué le falta.
        if (!contexto.HayEmpresa)
            return ErrorNegocio.Regla(
                "sin-empresa-activa", "Elige una empresa antes de consultar el tablero.");

        var saldo = await compras.SaldoAsync(ct);
        var membresia = await compras.MembresiaAsync(ct);

        // Se avisa tanto de la que está por vencer como de la ya vencida. EstaPorVencer solo
        // cubre la primera —exige que siga activa—, así que la vencida se suma aparte; sin
        // eso, la membresía dejaría de avisar justo el día en que empieza a estorbar.
        var enAviso = membresia is not null
                      && (ServicioDeCompras.EstaPorVencer(membresia)
                          || membresia.Estado == EstadosDeMembresia.Vencida);

        return new TableroDto(
            saldo, UmbralAvisoTimbres, await CertificadoAsync(ct), membresia, enAviso,
            await DocumentosAsync(ct), await SerieAsync(ct));
    }

    /// <summary>
    /// Los ultimos seis meses de facturacion. Vacia si la mitad B no esta registrada, igual
    /// que el resumen del mes: mejor no pintar la grafica que pintarla plana en cero.
    /// </summary>
    private async Task<IReadOnlyList<PuntoDeFacturacionDto>> SerieAsync(CancellationToken ct)
    {
        if (documentos is null) return [];

        var serie = await documentos.SerieMensualAsync(6, ct);

        return serie
            .Select(p => new PuntoDeFacturacionDto(p.Etiqueta, p.Timbrados, p.Importe))
            .ToList();
    }

    /// <summary>
    /// Comprobantes del mes en curso, en el huso de la empresa: «agosto» tiene que ser el
    /// agosto del contador, no el de UTC (ver <see cref="HusoDeEmpresa"/>).
    /// </summary>
    private async Task<ResumenDelMesDto?> DocumentosAsync(CancellationToken ct)
    {
        if (documentos is null) return null;

        var (desde, hasta) = await huso.MesEnCursoAsync(ct);
        var resumen = await documentos.ObtenerAsync(desde, hasta, ct);

        // Se enumeran los seis estatus y no solo los que trajeron comprobantes: el contrato
        // permite omitir los que van en cero, y una rejilla que cambia de columnas según el
        // mes es ilegible. Un cero aquí no miente —el periodo se está midiendo de verdad—,
        // que es justo lo contrario del recuadro que la fase 9 se negó a pintar.
        var conteo = Enum.GetValues<EstatusComprobante>()
            .Select(e => new ConteoPorEstatusDto(
                e.ACadena(),
                Etiqueta(e),
                resumen.ConteoPorEstatus.TryGetValue(e, out var cuenta) ? cuenta : 0))
            .ToArray();

        return new ResumenDelMesDto(
            desde, hasta, conteo, resumen.ImporteTimbrado, resumen.ImporteCancelado);
    }

    private static string Etiqueta(EstatusComprobante estatus) => estatus switch
    {
        EstatusComprobante.Borrador => "Borradores",
        EstatusComprobante.Timbrando => "Timbrando",
        EstatusComprobante.Timbrado => "Timbrados",
        EstatusComprobante.Error => "Con error",
        EstatusComprobante.Cancelado => "Cancelados",
        EstatusComprobante.EnCancelacion => "En cancelación",
        _ => estatus.ACadena()
    };

    /// <summary>
    /// Solo para quien tiene <c>configurar_empresa</c>. Es el mismo permiso que exige
    /// <c>/api/empresa/certificados</c>: si el tablero devolviera la vigencia a quien no
    /// puede consultar los certificados, el permiso dejaría de significar algo.
    /// </summary>
    private async Task<AvisoCertificadoDto?> CertificadoAsync(CancellationToken ct)
    {
        if (!contexto.Tiene(Permisos.ConfigurarEmpresa)) return null;

        var activo = (await csd.ListarAsync(ct)).FirstOrDefault(c => c.Activo);

        return activo is null
            ? new AvisoCertificadoDto(HayActivo: false, VigenciaHastaUtc: null, DiasParaCaducar: 0, PorCaducar: false)
            : new AvisoCertificadoDto(true, activo.VigenciaHastaUtc, activo.DiasParaCaducar, activo.PorCaducar);
    }
}
