using Facturacion.Server.Infra.Tenencia;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Data;

/// <summary>
/// Única forma de armar las opciones del contexto.
/// <para>
/// Existe porque el sellado de <c>EmpresaId</c> al escribir se conecta como interceptor, y
/// un interceptor que hay que acordarse de agregar es un interceptor que algún día no se
/// agrega. Con esto, cualquier <see cref="AppDbContext"/> —el de la aplicación, el de las
/// herramientas de migración, el de las pruebas— nace con la protección de escritura puesta.
/// </para>
/// </summary>
public static class OpcionesDeAppDbContext
{
    public static DbContextOptionsBuilder Configurar(
        this DbContextOptionsBuilder constructor,
        string? cadenaDeConexion,
        IContextoEmpresaInterno contexto)
        => constructor
            .UseSqlServer(cadenaDeConexion)
            .AddInterceptors(new SelladoDeEmpresaInterceptor(contexto));

    /// <summary>Misma configuración, para quien necesita el constructor tipado y sus <c>Options</c>.</summary>
    public static DbContextOptionsBuilder<AppDbContext> Configurar(
        this DbContextOptionsBuilder<AppDbContext> constructor,
        string? cadenaDeConexion,
        IContextoEmpresaInterno contexto)
    {
        ((DbContextOptionsBuilder)constructor).Configurar(cadenaDeConexion, contexto);
        return constructor;
    }
}
