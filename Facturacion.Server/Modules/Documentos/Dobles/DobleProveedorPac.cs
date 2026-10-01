using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// PAC inventado para <c>Pac:Modo=Simulado</c>: deja probar de punta a punta —timbrado,
/// PDF, correo, cancelación y su seguimiento— mientras el PAC real lo integra otra persona.
/// Implementa <see cref="IProveedorPac"/> igual que lo hará el real, así que todo lo que se
/// prueba aquí es el mismo código que correrá después.
///
/// <para><b>Qué hace</b></para>
/// <list type="bullet">
///   <item><description>
///     Timbra siempre: agrega un <c>TimbreFiscalDigital</c> con UUID nuevo, sello del SAT al
///     azar y la leyenda «sin validez fiscal», que el PDF muestra en el encabezado. La misma
///     clave de idempotencia devuelve el mismo timbre, como un PAC de verdad.
///   </description></item>
///   <item><description>
///     Cancela de inmediato lo que el SAT cancelaría sin aceptación (total de hasta $1,000,
///     RFC genérico, pagos y traslados). Lo demás queda esperando al receptor, que acepta a
///     los dos minutos —o rechaza, con <c>Pac:SimuladoReceptorRechaza=true</c>—; se ve al
///     consultar el estatus.
///   </description></item>
/// </list>
///
/// <para><b>Por qué guarda su estado en un archivo</b></para>
/// Si viviera solo en memoria, reiniciar el servidor olvidaría las cancelaciones en espera y
/// la consulta devolvería el comprobante como vigente sin solicitud, que el sistema toma como
/// cancelación que no prosperó. El archivo va en la carpeta temporal del sistema, lejos del
/// almacén cifrado.
/// </para>
/// </summary>
[DobleDePrueba]
public sealed class DobleProveedorPac : IProveedorPac
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    private const string Leyenda = "Timbre simulado en desarrollo. Sin validez fiscal.";

    /// <summary>El RFC genérico de pruebas del SAT: no suplanta a ningún PAC real.</summary>
    private const string RfcProveedor = "AAA010101AAA";

    private const string NoCertificadoSat = "00000000000000000000";

    private static readonly TimeSpan EsperaDelReceptor = TimeSpan.FromMinutes(2);

    private readonly string _archivo = Path.Combine(Path.GetTempPath(), "facturacion-pac-simulado.json");
    private readonly bool _receptorRechaza;
    private readonly ILogger<DobleProveedorPac> _registro;
    private readonly Lock _candado = new();
    private readonly Estado _estado;

    public DobleProveedorPac(IHostEnvironment entorno, IConfiguration configuracion, ILogger<DobleProveedorPac> registro)
    {
        if (!entorno.IsDevelopment())
            throw new InvalidOperationException("El PAC simulado solo existe en Development.");

        _receptorRechaza = configuracion.GetValue<bool>("Pac:SimuladoReceptorRechaza");
        _registro = registro;
        _estado = Cargar();

        _registro.LogWarning("PAC SIMULADO activo: los timbres y cancelaciones son inventados. Estado en {Archivo}.", _archivo);
    }

    // ── Timbrado ────────────────────────────────────────────────────────────────────────

    public Task<RespuestaDePac> TimbrarAsync(string xml, string claveIdempotencia, CancellationToken ct)
    {
        lock (_candado)
        {
            if (_estado.PorClave.TryGetValue(claveIdempotencia, out var previo))
                return Task.FromResult(ARespuesta(previo));

            XDocument documento;
            try
            {
                documento = XDocument.Parse(xml);
            }
            catch (System.Xml.XmlException)
            {
                return Task.FromResult(new RespuestaDePac(ResultadoDePac.Rechazado,
                    CodigoError: "SIM-301", Mensaje: "PAC simulado: el XML no está bien formado."));
            }

            var raiz = documento.Root!;
            var selloCfd = (string?)raiz.Attribute("Sello");

            if (string.IsNullOrWhiteSpace(selloCfd))
                return Task.FromResult(new RespuestaDePac(ResultadoDePac.Rechazado,
                    CodigoError: "SIM-302", Mensaje: "PAC simulado: el XML no trae sello."));

            var uuid = Guid.NewGuid();
            var fechaUtc = DateTime.UtcNow.AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond));
            var fechaLocal = HoraDelCentro(fechaUtc).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var selloSat = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256));

            var complemento = raiz.Element(Cfdi + "Complemento");
            if (complemento is null)
            {
                complemento = new XElement(Cfdi + "Complemento");
                raiz.Add(complemento);
            }

            complemento.Add(new XElement(Tfd + "TimbreFiscalDigital",
                new XAttribute(XNamespace.Xmlns + "tfd", Tfd),
                new XAttribute("Version", "1.1"),
                new XAttribute("UUID", uuid.ToString().ToUpperInvariant()),
                new XAttribute("FechaTimbrado", fechaLocal),
                new XAttribute("RfcProvCertif", RfcProveedor),
                new XAttribute("Leyenda", Leyenda),
                new XAttribute("SelloCFD", selloCfd),
                new XAttribute("NoCertificadoSAT", NoCertificadoSat),
                new XAttribute("SelloSAT", selloSat)));

            var timbre = new Timbre
            {
                Uuid = uuid,
                FechaUtc = fechaUtc,
                SelloSat = selloSat,
                CadenaOriginal = $"||1.1|{uuid.ToString().ToUpperInvariant()}|{fechaLocal}|{RfcProveedor}|{Leyenda}|{selloCfd}|{NoCertificadoSat}||",
                Xml = (documento.Declaration?.ToString() ?? """<?xml version="1.0" encoding="utf-8"?>""") +
                      documento.ToString(SaveOptions.DisableFormatting)
            };

            var total = decimal.TryParse((string?)raiz.Attribute("Total"), NumberStyles.Number, CultureInfo.InvariantCulture, out var t) ? t : 0m;
            var rfcReceptor = (string?)raiz.Element(Cfdi + "Receptor")?.Attribute("Rfc") ?? string.Empty;

            _estado.PorClave[claveIdempotencia] = timbre;
            _estado.PorUuid[uuid] = new Comprobante
            {
                // Lo que el SAT cancela sin pedirle permiso al receptor, simplificado.
                SinAceptacion = total <= 1000m || rfcReceptor is "XAXX010101000" or "XEXX010101000"
            };

            Guardar();

            _registro.LogInformation("PAC simulado: timbrado {Uuid}.", uuid);

            return Task.FromResult(ARespuesta(timbre));
        }
    }

    public Task<RespuestaDePac> ConsultarAsync(string xml, string claveIdempotencia, CancellationToken ct)
    {
        lock (_candado)
        {
            return Task.FromResult(_estado.PorClave.TryGetValue(claveIdempotencia, out var timbre)
                ? ARespuesta(timbre)
                : new RespuestaDePac(ResultadoDePac.NoEncontrado, Mensaje: "PAC simulado: la clave no se recibió nunca."));
        }
    }

    // ── Cancelación ─────────────────────────────────────────────────────────────────────

    public Task<RespuestaDeCancelacion> CancelarAsync(DatosDeCancelacion datos, CancellationToken ct)
    {
        lock (_candado)
        {
            if (!_estado.PorUuid.TryGetValue(datos.Uuid, out var comprobante))
                return Task.FromResult(new RespuestaDeCancelacion(ResultadoDeCancelacion.Rechazado,
                    "205", "PAC simulado: el UUID no existe."));

            switch (comprobante.Estado)
            {
                case EstadosDelDoble.Cancelado:
                    return Task.FromResult(new RespuestaDeCancelacion(ResultadoDeCancelacion.Rechazado,
                        "202", "PAC simulado: el comprobante ya estaba cancelado."));

                case EstadosDelDoble.EnEspera:
                    return Task.FromResult(new RespuestaDeCancelacion(ResultadoDeCancelacion.EnEsperaDelReceptor,
                        "201", "PAC simulado: ya hay una solicitud esperando al receptor."));
            }

            RespuestaDeCancelacion respuesta;

            if (comprobante.SinAceptacion)
            {
                comprobante.Estado = EstadosDelDoble.Cancelado;
                comprobante.EstatusCancelacion = "Cancelado sin aceptación";
                respuesta = new RespuestaDeCancelacion(ResultadoDeCancelacion.Cancelado,
                    "201", "PAC simulado: cancelado sin aceptación.", Acuse: "<Acuse simulado />");
            }
            else
            {
                comprobante.Estado = EstadosDelDoble.EnEspera;
                comprobante.EstatusCancelacion = "En proceso";
                comprobante.SolicitudUtc = DateTime.UtcNow;
                respuesta = new RespuestaDeCancelacion(ResultadoDeCancelacion.EnEsperaDelReceptor,
                    "201", $"PAC simulado: el receptor responde en {EsperaDelReceptor.TotalMinutes:0} minutos.",
                    Acuse: "<Acuse simulado />");
            }

            Guardar();

            return Task.FromResult(respuesta);
        }
    }

    public Task<EstatusSatDePac> ConsultarEstatusAsync(DatosDeConsultaSat datos, CancellationToken ct)
    {
        lock (_candado)
        {
            if (!_estado.PorUuid.TryGetValue(datos.Uuid, out var comprobante))
                return Task.FromResult(new EstatusSatDePac(true, "No Encontrado",
                    CodigoEstatus: "N - 602: Comprobante no encontrado."));

            if (comprobante.Estado == EstadosDelDoble.EnEspera &&
                DateTime.UtcNow - comprobante.SolicitudUtc >= EsperaDelReceptor)
            {
                if (_receptorRechaza)
                {
                    comprobante.Estado = EstadosDelDoble.Vigente;
                    comprobante.EstatusCancelacion = "Solicitud rechazada";
                }
                else
                {
                    comprobante.Estado = EstadosDelDoble.Cancelado;
                    comprobante.EstatusCancelacion = "Cancelado con aceptación";
                }

                Guardar();
            }

            return Task.FromResult(new EstatusSatDePac(
                true,
                comprobante.Estado == EstadosDelDoble.Cancelado ? "Cancelado" : "Vigente",
                comprobante.SinAceptacion ? "Cancelable sin aceptación" : "Cancelable con aceptación",
                comprobante.EstatusCancelacion,
                "S - Comprobante obtenido satisfactoriamente."));
        }
    }

    // ── Apoyo ───────────────────────────────────────────────────────────────────────────

    private static RespuestaDePac ARespuesta(Timbre timbre) => new(
        ResultadoDePac.Timbrado,
        timbre.Xml,
        timbre.Uuid,
        timbre.FechaUtc,
        NoCertificadoSat,
        timbre.SelloSat,
        timbre.CadenaOriginal);

    /// <summary>El SAT sella con la hora del centro del país, sin horario de verano desde 2022.</summary>
    private static DateTime HoraDelCentro(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(-6);
        }
    }

    private Estado Cargar()
    {
        try
        {
            return File.Exists(_archivo)
                ? JsonSerializer.Deserialize<Estado>(File.ReadAllText(_archivo)) ?? new Estado()
                : new Estado();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _registro.LogWarning(ex, "PAC simulado: no se pudo leer {Archivo}; se empieza vacío.", _archivo);
            return new Estado();
        }
    }

    private void Guardar() => File.WriteAllText(_archivo, JsonSerializer.Serialize(_estado));

    private static class EstadosDelDoble
    {
        public const string Vigente = "vigente";
        public const string EnEspera = "en_espera";
        public const string Cancelado = "cancelado";
    }

    private sealed class Estado
    {
        public Dictionary<string, Timbre> PorClave { get; set; } = [];

        public Dictionary<Guid, Comprobante> PorUuid { get; set; } = [];
    }

    private sealed class Timbre
    {
        public Guid Uuid { get; set; }

        public DateTime FechaUtc { get; set; }

        public string SelloSat { get; set; } = string.Empty;

        public string CadenaOriginal { get; set; } = string.Empty;

        public string Xml { get; set; } = string.Empty;
    }

    private sealed class Comprobante
    {
        public bool SinAceptacion { get; set; }

        public string Estado { get; set; } = EstadosDelDoble.Vigente;

        public string? EstatusCancelacion { get; set; }

        public DateTime? SolicitudUtc { get; set; }
    }
}
