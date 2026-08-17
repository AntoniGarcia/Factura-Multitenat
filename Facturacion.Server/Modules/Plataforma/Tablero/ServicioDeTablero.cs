using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Tablero;

/// <summary>
/// Arma el tablero de inicio a partir de lo que ya existe en la mitad A.
///
/// <para><b>Lo que este tablero no trae</b></para>
/// El conteo de comprobantes por estatus es de la mitad B y se lee por
/// <c>IResumenDocumentos</c>, que todavía no tiene implementación. No se inventa ni se
/// rellena con ceros: un cero se lee como «hoy no facturaste», que es una afirmación
/// distinta de «esto aún no existe». Cuando la mitad B registre su implementación, el
/// enganche es este servicio.
/// </summary>
public sealed class ServicioDeTablero(
    ServicioDeCompras compras,
    ServicioDeCsd csd,
    IContextoEmpresaInterno contexto)
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

        return new TableroDto(saldo, UmbralAvisoTimbres, await CertificadoAsync(ct), membresia, enAviso);
    }

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
