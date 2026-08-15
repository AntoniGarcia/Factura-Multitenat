using Facturacion.Server.Data;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Entrega el CSD descifrado. <b>Uso exclusivo del módulo de timbrado.</b>
///
/// <para><b>Hasta dónde llega la restricción, con honestidad</b></para>
/// El prompt de la fase pedía hacer cumplir esa restricción «como se pueda». Dentro de un
/// mismo proceso <b>no se puede impedir de verdad</b>: cualquier código que resuelva
/// <see cref="IProveedorCsdParaTimbrado"/> del contenedor obtiene la llave. Fingir lo
/// contrario —por ejemplo inspeccionando la pila de llamadas— daría una sensación de
/// seguridad falsa, y con <c>async</c> además sería poco fiable.
///
/// <para>Lo que sí se hace, y es lo que de verdad sirve:</para>
/// <list type="number">
///   <item><description>
///     <b>Toda</b> llamada queda en la bitácora con usuario, empresa y momento. No impide el
///     acceso indebido; garantiza que no pase inadvertido, que es la propiedad que se puede
///     sostener en una revisión.
///   </description></item>
///   <item><description>
///     El material solo existe en memoria, en el <see cref="CsdDescifradoDto"/> que devuelve.
///     No se registra en el log, no se serializa y no hay ningún endpoint que lo exponga.
///   </description></item>
///   <item><description>
///     El descifrado va atado a la empresa del claim: el CSD de una empresa no se puede
///     descifrar desde la sesión de otra, aunque alguien construya la consulta a mano.
///   </description></item>
/// </list>
/// <para>
/// La frontera dura de verdad sería sacar el sellado a un proceso aparte que solo reciba la
/// cadena original y devuelva el sello. Está fuera del alcance del MVP y queda anotado.
/// </para>
/// </summary>
public sealed class ProveedorCsdParaTimbrado(
    AppDbContext baseDeDatos,
    IAlmacenDeArchivos almacen,
    IProtectorDeSecretos secretos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora) : IProveedorCsdParaTimbrado
{
    public async Task<CsdDescifradoDto> ObtenerAsync(CancellationToken ct)
    {
        var empresaId = contexto.EmpresaId;

        var certificado = await baseDeDatos.CertificadosCsd
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Activo, ct)
            ?? throw new InvalidOperationException(
                "La empresa no tiene un certificado de sello digital activo.");

        if (DateTime.UtcNow > certificado.VigenciaHastaUtc)
            throw new InvalidOperationException(
                $"El certificado de sello digital caducó el {certificado.VigenciaHastaUtc:dd/MM/yyyy}.");

        var cer = await almacen.LeerAsync(empresaId, CategoriasDeArchivo.CertificadoCer, certificado.RutaCer, ct);
        var key = await almacen.LeerAsync(empresaId, CategoriasDeArchivo.LlavePrivadaKey, certificado.RutaKey, ct);

        // Solo el número de serie: la bitácora dice quién pidió el sello y con qué
        // certificado, nunca el material.
        bitacora.Registrar(
            EntidadesDeBitacora.CertificadoCsd, certificado.Id.ToString(),
            AccionesDeBitacora.CsdEntregadoParaTimbrar,
            despues: new { certificado.NumeroSerie });

        await baseDeDatos.SaveChangesAsync(ct);

        return new CsdDescifradoDto(
            certificado.NumeroSerie,
            cer,
            key,
            secretos.Descifrar(empresaId, certificado.ContrasenaCifrada),
            certificado.VigenciaDesdeUtc,
            certificado.VigenciaHastaUtc);
    }
}
