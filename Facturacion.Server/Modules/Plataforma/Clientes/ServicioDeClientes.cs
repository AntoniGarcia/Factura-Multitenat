using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Clientes;

/// <summary>
/// Catálogo de clientes de la empresa activa. Implementa además
/// <see cref="IServicioClientes"/> del contrato congelado, que es lo que la mitad B consume
/// al timbrar (REPARTO-EQUIPO.md §5).
/// <para>
/// Ninguna consulta filtra por empresa a mano: lo hace el filtro global de EF Core desde el
/// claim (CLAUDE.md §5). Que aquí no se vea un <c>WHERE EmpresaId</c> es la señal de que
/// está bien hecho, no de que falte.
/// </para>
/// </summary>
public sealed class ServicioDeClientes(
    AppDbContext baseDeDatos,
    ValidadorDeCliente validador,
    IServicioDeBitacora bitacora) : IServicioClientes
{
    /// <summary>
    /// La foto fiscal para congelar en el comprobante. Devuelve el DTO del contrato, nunca
    /// la entidad: un comprobante timbrado no puede depender de un catálogo que cambia
    /// (CLAUDE.md §5, inmutabilidad).
    /// </summary>
    public async Task<ReceptorFiscalDto?> ObtenerParaTimbradoAsync(Guid clienteId, CancellationToken ct)
    {
        var cliente = await baseDeDatos.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId, ct);

        // Un cliente dado de baja no debe poder recibir comprobantes nuevos; los viejos
        // siguen siendo legibles porque llevan la foto dentro.
        if (cliente is null || !cliente.Activo) return null;

        return new ReceptorFiscalDto(
            cliente.Id,
            cliente.Rfc,
            cliente.Nombre,
            cliente.RegimenFiscal,
            cliente.DomicilioFiscalCp,
            cliente.UsoCfdiPreferido,
            cliente.MetodoPagoPreferido,
            cliente.FormaPagoPreferida,
            cliente.CorreoPrincipal,
            cliente.ResidenciaFiscal,
            cliente.NumRegIdTrib);
    }

    public async Task<PaginaDeClientes> ListarAsync(
        string? texto, bool soloActivos, int pagina, int tamano, string? orden, bool descendente, CancellationToken ct)
    {
        var consulta = baseDeDatos.Clientes.AsNoTracking();

        if (soloActivos)
            consulta = consulta.Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var buscado = texto.Trim();
            consulta = consulta.Where(c => c.Nombre.Contains(buscado) || c.Rfc.Contains(buscado));
        }

        var total = await consulta.CountAsync(ct);

        consulta = (orden, descendente) switch
        {
            ("clave", false) => consulta.OrderBy(c => c.ClaveInterna),
            ("clave", true) => consulta.OrderByDescending(c => c.ClaveInterna),
            ("rfc", false) => consulta.OrderBy(c => c.Rfc),
            ("rfc", true) => consulta.OrderByDescending(c => c.Rfc),
            (_, true) => consulta.OrderByDescending(c => c.Nombre),
            _ => consulta.OrderBy(c => c.Nombre)
        };

        var elementos = await consulta
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(c => new ClienteEnListaDto(
                c.Id, c.ClaveInterna, c.Rfc, c.Nombre, c.RegimenFiscal,
                c.DomicilioFiscalCp, c.CorreoPrincipal, c.Activo))
            .ToListAsync(ct);

        return new PaginaDeClientes(elementos, total);
    }

    public async Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var cliente = await baseDeDatos.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return cliente is null ? null : ADto(cliente);
    }

    /// <summary>Todos los clientes para exportar. Sin paginar: el CSV es del catálogo completo.</summary>
    public Task<List<Cliente>> ListarParaExportarAsync(bool soloActivos, CancellationToken ct)
    {
        var consulta = baseDeDatos.Clientes.AsNoTracking();

        if (soloActivos) consulta = consulta.Where(c => c.Activo);

        return consulta.OrderBy(c => c.ClaveInterna).ToListAsync(ct);
    }

    public async Task<Resultado<RespuestaGuardarCliente>> CrearAsync(
        PeticionGuardarCliente peticion, CancellationToken ct)
    {
        var rfc = (peticion.Rfc ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = NombreFiscal.Normalizar(peticion.Nombre);

        var error = await validador.ValidarAsync(peticion, rfc, nombre.Normalizado, ct);
        if (error is not null) return error;

        var repetido = await baseDeDatos.Clientes
            .AsNoTracking()
            .AnyAsync(c => c.Rfc == rfc, ct);

        if (repetido && !Shared.Comun.Rfc.EsGenerico(rfc))
            return ErrorNegocio.Conflicto("rfc-repetido",
                $"Ya existe un cliente con el RFC {rfc} en esta empresa. " +
                "Si está dado de baja, reactívalo en vez de crear otro.");

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            ClaveInterna = await SiguienteClaveAsync(ct),
            Rfc = rfc,
            Nombre = nombre.Normalizado,
            RegimenFiscal = peticion.RegimenFiscal,
            DomicilioFiscalCp = peticion.DomicilioFiscalCp,
            FechaAltaUtc = DateTime.UtcNow
        };

        Volcar(peticion, cliente);

        baseDeDatos.Clientes.Add(cliente);

        bitacora.Registrar(
            EntidadesDeBitacora.Cliente, cliente.Id.ToString(), AccionesDeBitacora.ClienteCreado,
            despues: ADto(cliente));

        await baseDeDatos.SaveChangesAsync(ct);

        return new RespuestaGuardarCliente(ADto(cliente), nombre);
    }

    public async Task<Resultado<RespuestaGuardarCliente>> ActualizarAsync(
        Guid id, PeticionGuardarCliente peticion, CancellationToken ct)
    {
        var cliente = await baseDeDatos.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
            return ErrorNegocio.NoEncontrado("cliente-no-encontrado", "No se encontró ese cliente.");

        var rfc = (peticion.Rfc ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = NombreFiscal.Normalizar(peticion.Nombre);

        var error = await validador.ValidarAsync(peticion, rfc, nombre.Normalizado, ct);
        if (error is not null) return error;

        var repetido = await baseDeDatos.Clientes
            .AsNoTracking()
            .AnyAsync(c => c.Rfc == rfc && c.Id != id, ct);

        if (repetido && !Shared.Comun.Rfc.EsGenerico(rfc))
            return ErrorNegocio.Conflicto("rfc-repetido",
                $"Ya existe otro cliente con el RFC {rfc} en esta empresa.");

        var antes = ADto(cliente);

        cliente.Rfc = rfc;
        cliente.Nombre = nombre.Normalizado;
        cliente.RegimenFiscal = peticion.RegimenFiscal;
        cliente.DomicilioFiscalCp = peticion.DomicilioFiscalCp;
        cliente.FechaModificacionUtc = DateTime.UtcNow;

        Volcar(peticion, cliente);

        bitacora.Registrar(
            EntidadesDeBitacora.Cliente, cliente.Id.ToString(), AccionesDeBitacora.ClienteActualizado,
            antes, ADto(cliente));

        await baseDeDatos.SaveChangesAsync(ct);

        return new RespuestaGuardarCliente(ADto(cliente), nombre);
    }

    /// <summary>Baja lógica. Nunca borrado físico (CLAUDE.md §5).</summary>
    public async Task<Resultado<ClienteDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct)
    {
        var cliente = await baseDeDatos.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
            return ErrorNegocio.NoEncontrado("cliente-no-encontrado", "No se encontró ese cliente.");

        if (cliente.Activo == activo) return ADto(cliente);

        cliente.Activo = activo;
        cliente.FechaModificacionUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.Cliente, cliente.Id.ToString(),
            activo ? AccionesDeBitacora.ClienteReactivado : AccionesDeBitacora.ClienteDesactivado,
            antes: new { Activo = !activo }, despues: new { Activo = activo });

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(cliente);
    }

    /// <summary>
    /// Siguiente consecutivo de la empresa.
    ///
    /// <para><b>Por qué aquí sí basta con MAX + 1, y en folios no</b></para>
    /// Dos altas simultáneas pueden calcular la misma clave. La diferencia está en la
    /// consecuencia: un folio repetido es un comprobante que el SAT rechaza, y por eso vive
    /// en un procedimiento almacenado con bloqueo de renglón. Una clave interna repetida la
    /// ataja el índice único <c>IX_Clientes_ClaveInternaPorEmpresa</c>, la segunda alta
    /// falla, el usuario reintenta y ya. No tiene efecto fiscal: es una etiqueta para buscar.
    /// </para>
    /// </summary>
    private async Task<int> SiguienteClaveAsync(CancellationToken ct)
        => await baseDeDatos.Clientes.MaxAsync(c => (int?)c.ClaveInterna, ct) + 1 ?? 1;

    private static void Volcar(PeticionGuardarCliente p, Cliente c)
    {
        c.ResidenciaFiscal = Recortar(p.ResidenciaFiscal);
        c.NumRegIdTrib = Recortar(p.NumRegIdTrib);
        c.UsoCfdiPreferido = Recortar(p.UsoCfdiPreferido);
        c.MetodoPagoPreferido = Recortar(p.MetodoPagoPreferido);
        c.FormaPagoPreferida = Recortar(p.FormaPagoPreferida);
        c.Telefono = Recortar(p.Telefono);
        c.CorreoPrincipal = Recortar(p.CorreoPrincipal);
        c.Calle = Recortar(p.Calle);
        c.NumeroExterior = Recortar(p.NumeroExterior);
        c.NumeroInterior = Recortar(p.NumeroInterior);
        c.Colonia = Recortar(p.Colonia);
        c.Localidad = Recortar(p.Localidad);
        c.Referencia = Recortar(p.Referencia);
        c.Municipio = Recortar(p.Municipio);
        c.Estado = Recortar(p.Estado);
        c.Pais = Recortar(p.Pais);
        c.CodigoPostal = Recortar(p.CodigoPostal);
        c.Activo = p.Activo;
    }

    private static string? Recortar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ClienteDto ADto(Cliente c) => new(
        c.Id, c.ClaveInterna, c.Rfc, c.Nombre, c.RegimenFiscal, c.DomicilioFiscalCp,
        c.ResidenciaFiscal, c.NumRegIdTrib, c.UsoCfdiPreferido, c.MetodoPagoPreferido,
        c.FormaPagoPreferida, c.Telefono, c.CorreoPrincipal, c.Calle, c.NumeroExterior,
        c.NumeroInterior, c.Colonia, c.Localidad, c.Referencia, c.Municipio, c.Estado,
        c.Pais, c.CodigoPostal, c.Activo);
}
