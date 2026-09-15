using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

public sealed record ArchivoDeComprobanteDeCompra(byte[] Contenido, string Nombre);

/// <summary>
/// Lee una compra pagada y genera su comprobante. Tiene dos entradas explícitas porque el
/// inquilino depende del filtro global de empresa y el operador necesita cruzarlo.
/// </summary>
public sealed class ServicioDeComprobantesDeCompra(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IProveedorDeConfiguracionDelSistema configuracion,
    GeneradorDeComprobanteDeCompraPdf generador)
{
    public Task<Resultado<ArchivoDeComprobanteDeCompra>> GenerarParaEmpresaAsync(
        Guid compraId, CancellationToken ct)
        => GenerarAsync(
            baseDeDatos.ComprasTimbres.AsNoTracking().Where(c => c.EmpresaId == contexto.EmpresaId),
            compraId,
            ct);

    public Task<Resultado<ArchivoDeComprobanteDeCompra>> GenerarParaOperadorAsync(
        Guid compraId, CancellationToken ct)
        // IgnoreQueryFilters justificado: solo lo llama el endpoint protegido del operador.
        => GenerarAsync(baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AsNoTracking(), compraId, ct);

    private async Task<Resultado<ArchivoDeComprobanteDeCompra>> GenerarAsync(
        IQueryable<CompraTimbres> compras, Guid compraId, CancellationToken ct)
    {
        var compra = await compras.SingleOrDefaultAsync(c => c.Id == compraId, ct);

        if (compra is null)
            return ErrorNegocio.NoEncontrado("compra-no-encontrada", "Esa compra no existe.");

        if (compra.Estado != EstadosDeCompra.Pagada || compra.AcreditadaUtc is null)
            return ErrorNegocio.Conflicto(
                "compra-sin-pago", "El comprobante está disponible cuando el pago queda acreditado.");

        var sistema = await configuracion.ObtenerAsync(ct);
        var datos = new DatosDeComprobanteDeCompra(
            compra.Id,
            sistema.NombreDelSistema,
            compra.EmpresaNombreAlComprar,
            compra.EmpresaRfcAlComprar,
            compra.NombrePaquete,
            compra.CantidadTimbres,
            compra.PrecioPorTimbre,
            compra.Subtotal,
            compra.Iva,
            compra.TasaIva,
            compra.PrecioTotal,
            compra.CreadaUtc,
            compra.AcreditadaUtc.Value,
            compra.VenceUtc);

        var contenido = generador.Generar(datos);
        var nombre = $"comprobante-compra-{compra.Id:N}.pdf";
        return new ArchivoDeComprobanteDeCompra(contenido, nombre);
    }
}
