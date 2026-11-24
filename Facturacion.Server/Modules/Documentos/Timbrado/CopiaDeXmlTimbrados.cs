using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.ActualizacionBase;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// Copia a XmlsDeComprobante los XML timbrados que antes se guardaban como archivo cifrado.
/// El archivo se conserva como respaldo. Corre al arrancar; si la base no está lista (sin la
/// migración o en pausa), reintenta cada 15 minutos. Lo que falle se vuelve a intentar al reiniciar.
public sealed class CopiaDeXmlTimbrados(
    IServiceScopeFactory fabricaDeAmbitos,
    IConfiguration configuracion,
    IAlmacenDeArchivos almacen,
    ILogger<CopiaDeXmlTimbrados> registro) : BackgroundService
{
    private static readonly TimeSpan Reintento = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await CopiarAsync(ct)) return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                registro.LogError(ex, "No se pudieron copiar los XML timbrados a la base; se reintentará.");
            }

            await Task.Delay(Reintento, ct);
        }
    }

    // Verdadero cuando ya no queda nada que copiar.
    private async Task<bool> CopiarAsync(CancellationToken ct)
    {
        using var ambito = fabricaDeAmbitos.CreateScope();
        using var usoBase = ambito.ServiceProvider.GetRequiredService<PausaDeBase>().Tomar();
        if (usoBase is null) return false;
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        // Sin petición no hay empresa activa: se recorren todas y cada una se guarda en su contexto.
        var pendientes = await baseDeDatos.Comprobantes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Uuid != null && c.RutaXml != null &&
                        !baseDeDatos.XmlsDeComprobante.Any(x => x.ComprobanteId == c.Id))
            .Select(c => new { c.Id, c.EmpresaId, Ruta = c.RutaXml! })
            .ToListAsync(ct);

        if (pendientes.Count == 0) return true;

        var fallidos = 0;
        foreach (var empresa in pendientes.GroupBy(p => p.EmpresaId))
        {
            var tenencia = new ContextoEmpresaFijo(empresa.Key);
            var opciones = new DbContextOptionsBuilder<AppDbContext>()
                .Configurar(configuracion.GetConnectionString("BaseDeDatos"), tenencia)
                .Options;
            await using var deLaEmpresa = new AppDbContext(opciones, tenencia);

            foreach (var pendiente in empresa)
            {
                try
                {
                    var contenido = await almacen.LeerAsync(
                        empresa.Key, CategoriasDeArchivo.XmlTimbrado, pendiente.Ruta, ct);

                    deLaEmpresa.XmlsDeComprobante.Add(new XmlDeComprobante
                    {
                        ComprobanteId = pendiente.Id,
                        Xml = Encoding.UTF8.GetString(contenido).TrimStart('﻿'),
                        GuardadoUtc = DateTime.UtcNow
                    });
                    await deLaEmpresa.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fallidos++;
                    deLaEmpresa.ChangeTracker.Clear();
                    registro.LogError(ex, "No se pudo copiar a la base el XML del comprobante {Comprobante}.", pendiente.Id);
                }
            }
        }

        registro.LogInformation("XML timbrados copiados a la base: {Copiados} de {Total}.",
            pendientes.Count - fallidos, pendientes.Count);

        // Un archivo dañado no se arregla reintentando: se deja en el registro y no se insiste.
        return true;
    }
}
