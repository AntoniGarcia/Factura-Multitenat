using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>
/// <c>dotnet run -- --compras-pendientes</c> y <c>dotnet run -- --acreditar-compra &lt;id&gt;</c>.
/// Es como el operador del SaaS confirma un pago recibido y suelta los timbres a la bolsa de
/// la empresa que compró.
///
/// <para><b>Por qué consola y no un endpoint</b></para>
/// Acreditar es crear dinero: mete timbres a una bolsa sin que nadie haya pagado desde el
/// sistema. Quien puede hacerlo es el dueño del SaaS, no un usuario del inquilino —permitir
/// que un cliente acredite su propia compra sería regalarle timbres—. Y una identidad de
/// operador de plataforma no existe todavía; inventarla aquí sería adelantarse a la fase 8,
/// que es la que define usuarios y permisos. Mientras tanto, el permiso es tener acceso al
/// servidor, que es exactamente quien debe tenerlo.
/// </summary>
public static class AcreditacionDeCompraCli
{
    public static async Task<int> ListarPendientes(IServiceProvider servicios)
    {
        using var ambito = servicios.CreateScope();
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters justificado: la consola no tiene empresa activa y el operador
        // necesita ver las compras de todas las empresas.
        var pendientes = await baseDeDatos.ComprasTimbres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Estado == EstadosDeCompra.PendienteDePago)
            .OrderBy(c => c.CreadaUtc)
            .Join(baseDeDatos.Empresas.IgnoreQueryFilters(),
                compra => compra.EmpresaId,
                empresa => empresa.Id,
                (compra, empresa) => new
                {
                    compra.Id,
                    compra.NombrePaquete,
                    compra.CantidadTimbres,
                    compra.PrecioTotal,
                    compra.CreadaUtc,
                    Empresa = empresa.NombreFiscal
                })
            .ToListAsync();

        if (pendientes.Count == 0)
        {
            Console.WriteLine("No hay compras pendientes de pago.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"{pendientes.Count} compra(s) pendiente(s) de pago:");
        Console.WriteLine();

        foreach (var compra in pendientes)
        {
            Console.WriteLine($"  {compra.Id}");
            Console.WriteLine($"      empresa   {compra.Empresa}");
            Console.WriteLine($"      paquete   {compra.NombrePaquete} ({compra.CantidadTimbres} timbres)");
            Console.WriteLine($"      total     {compra.PrecioTotal:N2}");
            Console.WriteLine($"      solicitada {compra.CreadaUtc:yyyy-MM-dd HH:mm} UTC");
            Console.WriteLine();
        }

        Console.WriteLine("Para acreditar una: dotnet run -- --acreditar-compra <id>");
        Console.WriteLine();

        return 0;
    }

    public static async Task<int> Acreditar(IServiceProvider servicios, string argumento)
    {
        if (!Guid.TryParse(argumento, out var compraId))
        {
            Console.Error.WriteLine($"«{argumento}» no es un identificador de compra válido.");
            return 1;
        }

        using var ambito = servicios.CreateScope();
        var compras = ambito.ServiceProvider.GetRequiredService<ServicioDeCompras>();

        var resultado = await compras.AcreditarAsync(compraId, CancellationToken.None);

        if (resultado.EsFallo)
        {
            Console.Error.WriteLine(resultado.Error!.Mensaje);
            return 1;
        }

        var compra = resultado.Valor;

        Console.WriteLine();
        Console.WriteLine($"Acreditada: {compra.NombrePaquete}, {compra.CantidadTimbres} timbres.");
        Console.WriteLine($"Los timbres ya están en la bolsa. Vencen el {compra.VenceUtc:yyyy-MM-dd}.");
        Console.WriteLine();

        return 0;
    }
}
