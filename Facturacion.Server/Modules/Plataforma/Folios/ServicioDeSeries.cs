using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Folios;

/// <summary>
/// Administración de las series de folios de la empresa activa. La reserva vive aparte, en
/// <see cref="ServicioDeFolios"/>: aquí solo se define la serie, nunca se mueve su contador.
/// </summary>
public sealed class ServicioDeSeries(AppDbContext baseDeDatos, IServicioDeBitacora bitacora)
{
    public async Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken ct)
        => await baseDeDatos.Series
            .AsNoTracking()
            .OrderBy(s => s.Prefijo)
            .Select(s => new SerieDto(s.Id, s.Prefijo, s.FolioInicial, s.FolioActual, s.TipoComprobante, s.Activa))
            .ToListAsync(ct);

    public async Task<Resultado<SerieDto>> CrearAsync(PeticionGuardarSerie peticion, CancellationToken ct)
    {
        var error = await ValidarAsync(peticion, serieExistente: null, ct);
        if (error is not null) return error;

        var serie = new Serie
        {
            Id = Guid.NewGuid(),
            Prefijo = peticion.Prefijo.Trim(),
            FolioInicial = peticion.FolioInicial,
            // Arranca justo antes del inicial: el primer folio entregado será el inicial.
            FolioActual = peticion.FolioInicial - 1,
            TipoComprobante = peticion.TipoComprobante,
            Activa = peticion.Activa,
            FechaAltaUtc = DateTime.UtcNow
        };

        baseDeDatos.Series.Add(serie);

        bitacora.Registrar(
            EntidadesDeBitacora.Serie, serie.Id.ToString(), AccionesDeBitacora.SerieCreada,
            despues: new { serie.Prefijo, serie.FolioInicial, serie.TipoComprobante, serie.Activa });

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(serie);
    }

    public async Task<Resultado<SerieDto>> ActualizarAsync(
        Guid id, PeticionGuardarSerie peticion, CancellationToken ct)
    {
        var serie = await baseDeDatos.Series.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (serie is null)
            return ErrorNegocio.NoEncontrado("serie-no-encontrada", "No se encontró esa serie.");

        var error = await ValidarAsync(peticion, serie, ct);
        if (error is not null) return error;

        var antes = ADto(serie);

        serie.Prefijo = peticion.Prefijo.Trim();
        serie.TipoComprobante = peticion.TipoComprobante;
        serie.Activa = peticion.Activa;

        // FolioInicial solo se puede mover mientras la serie no haya entregado nada: cambiarlo
        // después reescribiría el sentido de la numeración ya emitida.
        if (serie.FolioActual < serie.FolioInicial)
        {
            serie.FolioInicial = peticion.FolioInicial;
            serie.FolioActual = peticion.FolioInicial - 1;
        }

        bitacora.Registrar(
            EntidadesDeBitacora.Serie, serie.Id.ToString(), AccionesDeBitacora.SerieActualizada,
            antes, ADto(serie));

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(serie);
    }

    private async Task<ErrorNegocio?> ValidarAsync(
        PeticionGuardarSerie peticion, Serie? serieExistente, CancellationToken ct)
    {
        var prefijo = peticion.Prefijo?.Trim() ?? string.Empty;

        if (prefijo.Length == 0)
            return ErrorNegocio.Validacion("prefijo-vacio", "Escribe el prefijo de la serie.");

        if (prefijo.Length > 25)
            return ErrorNegocio.Validacion("prefijo-largo", "El prefijo de la serie admite hasta 25 caracteres.");

        // El SAT solo acepta letras y dígitos en el atributo Serie del comprobante.
        if (!prefijo.All(char.IsAsciiLetterOrDigit))
            return ErrorNegocio.Validacion("prefijo-invalido",
                "El prefijo solo admite letras y números, sin espacios ni signos.");

        if (peticion.FolioInicial < 1)
            return ErrorNegocio.Validacion("folio-inicial-invalido", "El folio inicial empieza en 1.");

        var tipoExiste = await baseDeDatos.SatTiposDeComprobante
            .AsNoTracking()
            .AnyAsync(t => t.Clave == peticion.TipoComprobante, ct);

        if (!tipoExiste)
            return ErrorNegocio.Validacion("tipo-desconocido",
                $"El tipo de comprobante {peticion.TipoComprobante} no está en el catálogo del SAT.");

        var repetido = await baseDeDatos.Series
            .AsNoTracking()
            .AnyAsync(s => s.Prefijo == prefijo && (serieExistente == null || s.Id != serieExistente.Id), ct);

        if (repetido)
            return ErrorNegocio.Conflicto("prefijo-repetido",
                $"Ya existe una serie con el prefijo {prefijo} en esta empresa.");

        return null;
    }

    private static SerieDto ADto(Serie s)
        => new(s.Id, s.Prefijo, s.FolioInicial, s.FolioActual, s.TipoComprobante, s.Activa);
}
