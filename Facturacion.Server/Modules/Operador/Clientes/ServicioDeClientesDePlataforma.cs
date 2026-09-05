using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
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
public sealed class ServicioDeClientesDePlataforma(
    AppDbContext baseDeDatos,
    IPasswordHasher<OperadorPlataforma> hasher,
    IServicioDeBitacora bitacora)
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

                // IgnoreQueryFilters justificado: ComprasTimbres es de empresa y se cuentan
                // las de todas las empresas de la cuenta que se está listando. Acotado por
                // CuentaId, nunca abierto.
                ComprasPendientes = baseDeDatos.ComprasTimbres
                    .IgnoreQueryFilters()
                    .Count(cmp => cmp.Estado == EstadosDeCompra.PendienteDePago &&
                                  baseDeDatos.Empresas.Any(e => e.Id == cmp.EmpresaId && e.CuentaId == c.Id)),

                Membresia = baseDeDatos.Membresias
                    .Where(m => m.CuentaId == c.Id)
                    .OrderByDescending(m => m.FinUtc)
                    .Select(m => new { m.Estado, m.FinUtc })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var ahora = DateTime.UtcNow;

        var elementos = cuentas
            .Select(c => new CuentaEnListaDto(
                c.Id, c.Nombre, c.CorreoContacto, c.Activa, c.FechaAltaUtc,
                c.Empresas, c.Usuarios, c.Timbres,
                c.Membresia?.Estado, c.Membresia?.FinUtc,
                // El total de pendientes suma las compras por acreditar y el aviso de la
                // membresía: vencida o por vencer en los próximos treinta días.
                c.ComprasPendientes + (TieneMembresiaPendiente(c.Membresia?.Estado, c.Membresia?.FinUtc, ahora) ? 1 : 0)))
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
                    .Where(b => b.EmpresaId == e.Id).Select(b => (int?)b.Reservados).FirstOrDefault() ?? 0,
                // IgnoreQueryFilters justificado: ComprasTimbres es de empresa; se cuentan las
                // pendientes de acreditar de esta empresa concreta, no las de las demás.
                baseDeDatos.ComprasTimbres.IgnoreQueryFilters()
                    .Count(c => c.EmpresaId == e.Id && c.Estado == EstadosDeCompra.PendienteDePago)))
            .ToListAsync(ct);

        return new DetalleDeCuentaDto(cuenta, empresas);
    }

    /// <summary>
    /// La ficha de una sola empresa emisora de una cuenta. Acredita que la empresa pertenezca
    /// a la cuenta antes de devolver nada: el operador trabaja con identificadores de dominio,
    /// y una empresa ajena a esa cuenta no debe resolverse.
    /// </summary>
    public async Task<FichaDeEmpresaDto?> ObtenerEmpresaAsync(
        Guid cuentaId, Guid empresaId, string? texto, CancellationToken ct)
    {
        var empresa = await baseDeDatos.Empresas
            .AsNoTracking()
            .Where(e => e.Id == empresaId && e.CuentaId == cuentaId)
            .Select(e => new
            {
                e.Id,
                e.CuentaId,
                e.Rfc,
                e.NombreFiscal,
                e.RegimenFiscal,
                e.Activa,
                e.FechaAltaUtc,
                // IgnoreQueryFilters justificado: BolsaTimbres y ComprasTimbres son de empresa
                // y se consultan acotadas a esta empresa concreta, nunca abiertas.
                TimbresDisponibles = baseDeDatos.BolsasTimbres.IgnoreQueryFilters()
                    .Where(b => b.EmpresaId == e.Id).Select(b => (int?)b.Disponibles).FirstOrDefault() ?? 0,
                TimbresReservados = baseDeDatos.BolsasTimbres.IgnoreQueryFilters()
                    .Where(b => b.EmpresaId == e.Id).Select(b => (int?)b.Reservados).FirstOrDefault() ?? 0
            })
            .FirstOrDefaultAsync(ct);

        if (empresa is null) return null;

        // Búsqueda opcional de compras por nombre de paquete. Vacía o nula conserva el
        // comportamiento por defecto: todas las pendientes y las diez pagadas recientes.
        string? patron = string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim()}%";

        var comprasPendientes = await (
            from compra in baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AsNoTracking()
            join cuenta in baseDeDatos.Cuentas.AsNoTracking() on empresa.CuentaId equals cuenta.Id
            where compra.EmpresaId == empresaId && compra.Estado == EstadosDeCompra.PendienteDePago
                  && (patron == null || EF.Functions.Like(compra.NombrePaquete, patron))
            orderby compra.CreadaUtc ascending
            select new CompraDeOperadorDto(
                compra.Id, empresa.NombreFiscal, empresa.Rfc, cuenta.Nombre,
                compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioPorTimbre,
                compra.PrecioTotal, compra.Estado, compra.CreadaUtc, compra.AcreditadaUtc,
                compra.VenceUtc, compra.CanceladaUtc, compra.MotivoCancelacion))
            .ToListAsync(ct);

        // Compras pagadas de esta empresa: el historial de lo que ya entró a la bolsa.
        // IgnoreQueryFilters justificado como arriba, acotado a esta empresa.
        // Al buscar se amplía el tope: la búsqueda sirve para encontrar compras antiguas, no
        // solo las diez más recientes.
        var topeDePagadas = patron is null ? 10 : 100;

        var ultimasCompras = await (
            from compra in baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AsNoTracking()
            join cuenta in baseDeDatos.Cuentas.AsNoTracking() on empresa.CuentaId equals cuenta.Id
            where compra.EmpresaId == empresaId && compra.Estado == EstadosDeCompra.Pagada
                  && (patron == null || EF.Functions.Like(compra.NombrePaquete, patron))
            orderby compra.CreadaUtc descending
            select new CompraDeOperadorDto(
                compra.Id, empresa.NombreFiscal, empresa.Rfc, cuenta.Nombre,
                compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioPorTimbre,
                compra.PrecioTotal, compra.Estado, compra.CreadaUtc, compra.AcreditadaUtc,
                compra.VenceUtc, compra.CanceladaUtc, compra.MotivoCancelacion))
            .Take(topeDePagadas)
            .ToListAsync(ct);

        // Usuarios con acceso vigente a esta empresa. Se leen a través del cableado de tenencia,
        // que es la única forma fiable de saber dónde puede entrar cada usuario. Se filtra por
        // <c>ue.Activo</c>: un acceso eliminado deja de aparecer aquí.
        var usuarios = await (
            from ue in baseDeDatos.UsuariosEmpresas.AsNoTracking()
            join u in baseDeDatos.Users.AsNoTracking() on ue.UsuarioId equals u.Id
            where ue.EmpresaId == empresaId && ue.Activo
            orderby u.Nombre
            select new UsuarioDeCuentaDto(
                u.Id, u.Nombre, u.Email ?? string.Empty, u.Activo, u.FechaAltaUtc))
            .ToListAsync(ct);

        return new FichaDeEmpresaDto(
            empresa.Id, empresa.Rfc, empresa.NombreFiscal, empresa.RegimenFiscal,
            empresa.Activa, empresa.FechaAltaUtc,
            empresa.TimbresDisponibles, empresa.TimbresReservados,
            comprasPendientes, ultimasCompras, usuarios);
    }

    /// <summary>
    /// Activa o desactiva una empresa de una cuenta. Baja lógica (<c>Activo = false</c>): no se
    /// borra nada, los comprobantes ya timbrados siguen inmutables y el cambio es reversible.
    /// <para>
    /// Es una operación grave —deja a la empresa sin poder facturar—, así que exige la
    /// contraseña del propio operador, igual que el resto de las operaciones de soporte.
    /// </para>
    /// </summary>
    public async Task<Resultado<bool>> CambiarActivoEmpresaAsync(
        Guid operadorId, Guid cuentaId, Guid empresaId, PeticionCambiarActivoDeEmpresa peticion, CancellationToken ct)
    {
        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        if (!peticion.Activo && string.IsNullOrWhiteSpace(peticion.Motivo))
            return ErrorNegocio.Validacion("motivo-requerido", "Escribe por qué se da de baja la empresa.");

        var empresa = await baseDeDatos.Empresas
            .SingleOrDefaultAsync(e => e.Id == empresaId && e.CuentaId == cuentaId, ct);

        if (empresa is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "Esa empresa no existe en esta cuenta.");

        if (empresa.Activa == peticion.Activo) return true;

        var antes = new { empresa.Activa };

        empresa.Activa = peticion.Activo;

        var motivo = peticion.Motivo?.Trim();

        bitacora.Registrar(
            EntidadesDeBitacora.Empresa,
            empresa.Id.ToString(),
            peticion.Activo ? AccionesDeBitacora.EmpresaReactivadaPorOperador : AccionesDeBitacora.EmpresaDesactivadaPorOperador,
            antes: antes,
            despues: new { empresa.Activa, Motivo = motivo },
            empresaId: empresaId,
            cuentaId: cuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Cambia el correo de contacto de una cuenta. Es una operación de soporte del SaaS, así
    /// que exige la contraseña del operador. Cambiar este correo no toca las sesiones ni las
    /// credenciales de la cuenta.
    /// </summary>
    public async Task<Resultado<bool>> CambiarCorreoDeContactoDeCuentaAsync(
        Guid operadorId, Guid cuentaId, PeticionCambiarCorreoDeContactoDeCuenta peticion, CancellationToken ct)
    {
        if (await ContrasenaDelOperadorEsIncorrecta(operadorId, peticion.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        if (!EsCorreoValido(peticion.CorreoNuevo))
            return ErrorNegocio.Validacion("correo-invalido", "Escribe un correo válido.");

        var correo = peticion.CorreoNuevo.Trim().ToLowerInvariant();

        var cuenta = await baseDeDatos.Cuentas
            .SingleOrDefaultAsync(c => c.Id == cuentaId, ct);

        if (cuenta is null)
            return ErrorNegocio.NoEncontrado("cuenta-no-encontrada", "Esa cuenta no existe.");

        var antes = new { cuenta.CorreoContacto };

        cuenta.CorreoContacto = correo;

        bitacora.Registrar(
            EntidadesDeBitacora.Cuenta,
            cuenta.Id.ToString(),
            AccionesDeBitacora.CorreoDeCuentaCambiadoPorOperador,
            antes: antes,
            despues: new { correo },
            cuentaId: cuentaId,
            operadorId: operadorId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    private static bool EsCorreoValido(string? correo)
        => correo is not null
           && correo.Length <= 254
           && new System.Net.Mail.MailAddress(correo.Trim()).Address == correo.Trim();

    private async Task<bool> ContrasenaDelOperadorEsIncorrecta(
        Guid operadorId, string contrasena, CancellationToken ct)
    {
        var operador = await baseDeDatos.OperadoresPlataforma
            .SingleOrDefaultAsync(o => o.Id == operadorId && o.Activo, ct);

        if (operador is null) return true;

        return hasher.VerifyHashedPassword(operador, operador.HashContrasena, contrasena)
            is PasswordVerificationResult.Failed;
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

                ComprasPendientes = baseDeDatos.ComprasTimbres
                    .IgnoreQueryFilters()
                    .Count(cmp => cmp.Estado == EstadosDeCompra.PendienteDePago &&
                                  baseDeDatos.Empresas.Any(e => e.Id == cmp.EmpresaId && e.CuentaId == c.Id)),

                Membresia = baseDeDatos.Membresias
                    .Where(m => m.CuentaId == c.Id)
                    .OrderByDescending(m => m.FinUtc)
                    .Select(m => new { m.Estado, m.FinUtc })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null) return null;

        var ahora = DateTime.UtcNow;

        return new CuentaEnListaDto(
            cuenta.Id, cuenta.Nombre, cuenta.CorreoContacto, cuenta.Activa, cuenta.FechaAltaUtc,
            cuenta.Empresas, cuenta.Usuarios, cuenta.Timbres,
            cuenta.Membresia?.Estado, cuenta.Membresia?.FinUtc,
            cuenta.ComprasPendientes + (TieneMembresiaPendiente(cuenta.Membresia?.Estado, cuenta.Membresia?.FinUtc, ahora) ? 1 : 0));
    }

    /// <summary>
    /// Cuenta para el badge de pendientes si la membresía está vencida o por vencer en los
    /// próximos treinta días. El mismo horizonte que usa el tablero del operador.
    /// </summary>
    private static bool TieneMembresiaPendiente(string? estado, DateTime? finUtc, DateTime ahora)
    {
        if (estado is null || finUtc is null) return false;

        if (estado == EstadosDeMembresia.Vencida) return true;

        var limiteDeAviso = ahora.AddDays(DiasDeAvisoDeVencimiento);

        return estado == EstadosDeMembresia.Activa && finUtc <= limiteDeAviso;
    }

    private const int DiasDeAvisoDeVencimiento = 30;
}
