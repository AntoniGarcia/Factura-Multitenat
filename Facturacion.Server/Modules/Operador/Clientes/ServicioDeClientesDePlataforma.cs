using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Las cuentas contratantes del SaaS y sus empresas, vistas desde el panel del proveedor.
///
/// <para><b>Regla de privacidad de este archivo</b></para>
/// Aquí solo se consultan <b>metadatos de negocio</b>: cuántas empresas tiene una cuenta,
/// cuántos usuarios, cuánto saldo de timbres le queda y qué ha comprado. Ninguna consulta de
/// este módulo toca <c>Comprobantes</c>, <c>Clientes</c>, <c>Productos</c>,
/// <c>CertificadosCsd</c> ni <c>Series</c>: el proveedor administra el servicio, no mira
/// dentro del negocio de su cliente.
/// <para>
/// Nada en el código impide escribir esa consulta; lo que lo impide es esta decisión. Por eso
/// queda escrita aquí y no solo en un documento.
/// </para>
///
/// <para><b>Qué está y qué no está bajo el filtro de empresa</b></para>
/// <c>Cuenta</c>, <c>Empresa</c> y <c>Usuario</c> quedan fuera del filtro global por diseño,
/// así que se consultan directo. <c>BolsaTimbres</c> sí lo lleva, y por eso sus consultas van
/// con <c>IgnoreQueryFilters</c> anotado y siempre acotadas a las empresas de la cuenta que
/// se está mirando.
/// </summary>
public sealed class ServicioDeClientesDePlataforma(AppDbContext baseDeDatos)
{
    public async Task<PaginaDeCuentas> ListarAsync(
        string? texto, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = baseDeDatos.Cuentas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";

            // Se busca también por los datos de sus empresas: al proveedor le llega el RFC
            // del cliente antes que el nombre de la cuenta con que se dio de alta.
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Nombre, patron) ||
                EF.Functions.Like(c.CorreoContacto, patron) ||
                baseDeDatos.Empresas.Any(e => e.CuentaId == c.Id &&
                    (EF.Functions.Like(e.NombreFiscal, patron) || EF.Functions.Like(e.Rfc, patron))));
        }

        var total = await consulta.CountAsync(ct);

        var cuentas = await consulta
            .OrderBy(c => c.Nombre)
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.CorreoContacto,
                c.Activa,
                c.FechaAltaUtc,
                Empresas = baseDeDatos.Empresas.Count(e => e.CuentaId == c.Id),
                Usuarios = baseDeDatos.Users.Count(u => u.CuentaId == c.Id),

                // IgnoreQueryFilters justificado: BolsaTimbres es de empresa y el panel suma
                // las de la cuenta que está listando. Acotado por CuentaId, nunca abierto.
                Timbres = baseDeDatos.BolsasTimbres
                    .IgnoreQueryFilters()
                    .Where(b => baseDeDatos.Empresas.Any(e => e.Id == b.EmpresaId && e.CuentaId == c.Id))
                    .Sum(b => (int?)b.Disponibles) ?? 0,

                Membresia = baseDeDatos.Membresias
                    .Where(m => m.CuentaId == c.Id)
                    .OrderByDescending(m => m.FinUtc)
                    .Select(m => new { m.Estado, m.FinUtc })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var elementos = cuentas
            .Select(c => new CuentaEnListaDto(
                c.Id, c.Nombre, c.CorreoContacto, c.Activa, c.FechaAltaUtc,
                c.Empresas, c.Usuarios, c.Timbres,
                c.Membresia?.Estado, c.Membresia?.FinUtc))
            .ToList();

        return new PaginaDeCuentas(elementos, total);
    }

    public async Task<DetalleDeCuentaDto?> ObtenerAsync(Guid cuentaId, CancellationToken ct)
    {
        var pagina = await ListarPorIdAsync(cuentaId, ct);

        if (pagina is not { } cuenta) return null;

        var empresas = await baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.CuentaId == cuentaId)
            .OrderBy(e => e.NombreFiscal)
            .Select(e => new EmpresaDeCuentaDto(
                e.Id, e.Rfc, e.NombreFiscal, e.RegimenFiscal, e.Activa, e.FechaAltaUtc,
                // IgnoreQueryFilters justificado: igual que arriba, acotado a esta empresa.
                baseDeDatos.BolsasTimbres.IgnoreQueryFilters()
                    .Where(b => b.EmpresaId == e.Id).Select(b => (int?)b.Disponibles).FirstOrDefault() ?? 0,
                baseDeDatos.BolsasTimbres.IgnoreQueryFilters()
                    .Where(b => b.EmpresaId == e.Id).Select(b => (int?)b.Reservados).FirstOrDefault() ?? 0))
            .ToListAsync(ct);

        var usuarios = await baseDeDatos.Users
            .AsNoTracking()
            .Where(u => u.CuentaId == cuentaId)
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioDeCuentaDto(
                u.Id, u.Nombre, u.Email ?? string.Empty, u.Activo, u.FechaAltaUtc))
            .ToListAsync(ct);

        // Las últimas compras de todas sus empresas: es el historial comercial de la cuenta.
        // IgnoreQueryFilters justificado por lo mismo, acotado por las empresas de la cuenta.
        var compras = await (
            from compra in baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AsNoTracking()
            join empresa in baseDeDatos.Empresas.AsNoTracking() on compra.EmpresaId equals empresa.Id
            where empresa.CuentaId == cuentaId
            orderby compra.CreadaUtc descending
            select new CompraDeOperadorDto(
                compra.Id, empresa.NombreFiscal, empresa.Rfc, cuenta.Nombre,
                compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioPorTimbre,
                compra.PrecioTotal, compra.Estado, compra.CreadaUtc, compra.AcreditadaUtc,
                compra.VenceUtc, compra.CanceladaUtc, compra.MotivoCancelacion))
            .Take(10)
            .ToListAsync(ct);

        return new DetalleDeCuentaDto(cuenta, empresas, usuarios, compras);
    }

    /// <summary>
    /// La cabecera de una sola cuenta, con los mismos totales que el listado. Se consulta por
    /// identificador y no reutilizando el listado completo: traer todas las cuentas para
    /// quedarse con una escala fatal en cuanto haya clientes de verdad.
    /// </summary>
    private async Task<CuentaEnListaDto?> ListarPorIdAsync(Guid cuentaId, CancellationToken ct)
    {
        var cuenta = await baseDeDatos.Cuentas
            .AsNoTracking()
            .Where(c => c.Id == cuentaId)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.CorreoContacto,
                c.Activa,
                c.FechaAltaUtc,
                Empresas = baseDeDatos.Empresas.Count(e => e.CuentaId == c.Id),
                Usuarios = baseDeDatos.Users.Count(u => u.CuentaId == c.Id),

                // IgnoreQueryFilters justificado: mismo motivo que en el listado, acotado a
                // las empresas de esta cuenta.
                Timbres = baseDeDatos.BolsasTimbres
                    .IgnoreQueryFilters()
                    .Where(b => baseDeDatos.Empresas.Any(e => e.Id == b.EmpresaId && e.CuentaId == c.Id))
                    .Sum(b => (int?)b.Disponibles) ?? 0,

                Membresia = baseDeDatos.Membresias
                    .Where(m => m.CuentaId == c.Id)
                    .OrderByDescending(m => m.FinUtc)
                    .Select(m => new { m.Estado, m.FinUtc })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        return cuenta is null
            ? null
            : new CuentaEnListaDto(
                cuenta.Id, cuenta.Nombre, cuenta.CorreoContacto, cuenta.Activa, cuenta.FechaAltaUtc,
                cuenta.Empresas, cuenta.Usuarios, cuenta.Timbres,
                cuenta.Membresia?.Estado, cuenta.Membresia?.FinUtc);
    }
}
