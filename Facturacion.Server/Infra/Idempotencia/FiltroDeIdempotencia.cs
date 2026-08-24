using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Infra.Idempotencia;

/// <summary>
/// Hace cumplir el <c>Idempotency-Key</c> de ARQUITECTURA.md §4 en las operaciones que cobran.
///
/// <para><b>La clave se reclama ANTES de ejecutar</b></para>
/// El orden es lo único que hace que esto funcione. Si se ejecutara primero y se guardara
/// después, dos peticiones simultáneas con la misma clave entrarían las dos, cobrarían las
/// dos, y solo al guardar descubriríamos el duplicado —cuando ya cobramos dos veces—. Aquí
/// el renglón se inserta primero, con la respuesta todavía vacía: el índice único sobre
/// (empresa, clave) hace que solo una petición gane. La que pierde no ejecuta nada.
///
/// <para><b>Cuatro desenlaces para el que repite</b></para>
/// <list type="bullet">
///   <item>Misma clave, mismo cuerpo, misma ruta, ya terminada → su respuesta y su código, tal cual.</item>
///   <item>La original sigue en curso → 409, porque todavía no hay respuesta que copiar.</item>
///   <item>Misma clave, cuerpo distinto → 422. Devolverle la respuesta de la otra petición
///   sería peor que fallar: creería que se cobró lo que pidió.</item>
///   <item>Misma clave, otra ruta → 422, por lo mismo.</item>
/// </list>
/// </summary>
public sealed class FiltroDeIdempotencia(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno empresa,
    ILogger<FiltroDeIdempotencia> registro) : IEndpointFilter
{
    private const string Encabezado = "Idempotency-Key";

    /// <summary>Tope de la columna en <c>ClaveIdempotenciaConfiguracion</c>.</summary>
    private const int LargoMaximoClave = 128;

    /// <summary>Ventana de ARQUITECTURA.md §4. La purga por horas borra lo que la rebasa.</summary>
    private static readonly TimeSpan Ventana = TimeSpan.FromHours(24);

    /// <summary>Estado que marca «reclamada pero todavía sin respuesta».</summary>
    private const int EnCurso = 0;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var http = contexto.HttpContext;
        var ct = http.RequestAborted;

        if (!http.Request.Headers.TryGetValue(Encabezado, out var valores) ||
            string.IsNullOrWhiteSpace(valores.ToString()))
        {
            return ErrorNegocio.Validacion(
                "idempotency-key-requerida",
                $"Falta el encabezado {Encabezado}. Toda operación que cobra lo exige para que " +
                "reintentar no cobre dos veces.").AResultado(http);
        }

        var clave = valores.ToString();

        if (clave.Length > LargoMaximoClave)
        {
            return ErrorNegocio.Validacion(
                "idempotency-key-invalida",
                $"El {Encabezado} no puede pasar de {LargoMaximoClave} caracteres.").AResultado(http);
        }

        var ruta = http.Request.Path.Value ?? string.Empty;
        var hash = await HashDelCuerpo(http.Request, ct);

        // La búsqueda es solo por clave, igual que el índice único: buscar también por ruta
        // encontraría «nada» para una clave reciclada en otro endpoint y el insert chocaría
        // contra el índice sin que supiéramos por qué.
        var existente = await baseDeDatos.ClavesIdempotencia
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Clave == clave, ct);

        if (existente is not null)
            return DeLaClaveExistente(existente, ruta, hash, clave, http);

        var reclamo = new ClaveIdempotencia
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresa.EmpresaId,
            UsuarioId = empresa.UsuarioActual ?? Guid.Empty,
            Clave = clave,
            Endpoint = ruta,
            HashPeticion = hash,
            CodigoEstado = EnCurso,
            RespuestaJson = string.Empty,
            CreadoUtc = DateTime.UtcNow,
            ExpiraUtc = DateTime.UtcNow.Add(Ventana)
        };

        baseDeDatos.ClavesIdempotencia.Add(reclamo);

        try
        {
            await baseDeDatos.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Otra petición con la misma clave ganó la carrera entre la consulta de arriba y
            // este insert. El índice único es lo que la detiene aquí, no la consulta previa:
            // consultar y luego insertar sin candado siempre deja esta ventana abierta.
            baseDeDatos.Entry(reclamo).State = EntityState.Detached;

            var ganadora = await baseDeDatos.ClavesIdempotencia
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Clave == clave, ct);

            return ganadora is null
                ? ErrorNegocio.Conflicto(
                    "idempotency-key-en-conflicto",
                    "No se pudo registrar la operación. Vuelve a intentarlo.").AResultado(http)
                : DeLaClaveExistente(ganadora, ruta, hash, clave, http);
        }

        var respuesta = await siguiente(contexto);

        await GuardarRespuesta(reclamo.Id, respuesta, http, ct);

        return respuesta;
    }

    private object DeLaClaveExistente(
        ClaveIdempotencia existente, string ruta, string hash, string clave, HttpContext http)
    {
        if (existente.Endpoint != ruta || existente.HashPeticion != hash)
        {
            registro.LogWarning(
                "Idempotency-Key reutilizada para otra operación. Original {Original}, nueva {Nueva}",
                existente.Endpoint, ruta);

            return ErrorNegocio.Regla(
                "idempotency-key-reutilizada",
                $"Ya se usó el {Encabezado} «{clave}» para otra petición. " +
                "Usa una clave nueva para una operación nueva.").AResultado(http);
        }

        if (existente.CodigoEstado == EnCurso)
        {
            return ErrorNegocio.Conflicto(
                "idempotency-key-en-curso",
                "La operación con esa clave todavía se está procesando. Espera un momento y " +
                "consulta el resultado antes de reintentar.").AResultado(http);
        }

        return Results.Content(
            existente.RespuestaJson, "application/json", Encoding.UTF8, existente.CodigoEstado);
    }

    /// <summary>
    /// Guarda la respuesta para poder repetirla. Solo se guardan las que salieron bien: si la
    /// operación falló, la clave se libera para que el cliente pueda reintentar de verdad
    /// —quedarse con un error grabado convertiría un fallo pasajero en permanente durante 24
    /// horas—.
    /// </summary>
    private async Task GuardarRespuesta(Guid reclamoId, object? respuesta, HttpContext http, CancellationToken ct)
    {
        var reclamo = await baseDeDatos.ClavesIdempotencia.FirstOrDefaultAsync(c => c.Id == reclamoId, ct);

        if (reclamo is null) return;

        var codigo = CodigoDe(respuesta, http);

        if (codigo is < 200 or >= 300)
        {
            baseDeDatos.ClavesIdempotencia.Remove(reclamo);
            await baseDeDatos.SaveChangesAsync(ct);
            return;
        }

        reclamo.CodigoEstado = codigo;
        reclamo.RespuestaJson = Serializar(respuesta);

        await baseDeDatos.SaveChangesAsync(ct);
    }

    private static int CodigoDe(object? respuesta, HttpContext http) => respuesta switch
    {
        IStatusCodeHttpResult conCodigo when conCodigo.StatusCode is not null => conCodigo.StatusCode.Value,
        null => http.Response.StatusCode,
        _ => StatusCodes.Status200OK
    };

    private static string Serializar(object? respuesta)
    {
        // Los endpoints que pasan por aquí devuelven Results.Ok(dto); se guarda el DTO, que es
        // exactamente lo que el cliente recibiría.
        var valor = respuesta is IValueHttpResult conValor ? conValor.Value : respuesta;

        return JsonSerializer.Serialize(valor, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    /// <summary>
    /// SHA-256 del cuerpo. Es lo que permite distinguir un reintento legítimo de una clave
    /// reciclada para otra compra.
    /// </summary>
    private static async Task<string> HashDelCuerpo(HttpRequest peticion, CancellationToken ct)
    {
        peticion.EnableBuffering();
        peticion.Body.Position = 0;

        using var flujo = new MemoryStream();
        await peticion.Body.CopyToAsync(flujo, ct);

        // Se rebobina para que el enlace del modelo pueda volver a leer el cuerpo después.
        peticion.Body.Position = 0;

        return Convert.ToHexString(SHA256.HashData(flujo.ToArray()));
    }
}
