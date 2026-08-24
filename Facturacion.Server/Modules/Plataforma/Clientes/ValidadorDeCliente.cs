using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Clientes;

/// <summary>
/// Las reglas de CFDI 4.0 que rompen timbrados, todas en un solo lugar (ARQUITECTURA.md §7).
/// Se ejecutan <b>siempre en el servidor</b>: el <c>Client</c> valida lo mismo para dar
/// respuesta inmediata, pero esa validación viaja al navegador y es legible y modificable
/// (ARQUITECTURA.md §3).
///
/// <para><b>Por qué los mensajes están escritos así</b></para>
/// Los lee un contador, no un programador. «El régimen 605 no admite el uso G03» no le dice
/// qué hacer; «Sueldos y Salarios no puede recibir facturas de gastos en general» sí. El
/// código de error va aparte, para soporte.
/// </summary>
public sealed class ValidadorDeCliente(AppDbContext baseDeDatos, IServicioCatalogosSat catalogos)
{
    public async Task<ErrorNegocio?> ValidarAsync(
        PeticionGuardarCliente peticion, string rfcNormalizado, string nombreNormalizado, CancellationToken ct)
    {
        var rfc = Shared.Comun.Rfc.Validar(rfcNormalizado);

        if (!rfc.EsValido)
            return ErrorNegocio.Validacion("rfc-invalido", rfc.Mensaje!);

        if (string.IsNullOrWhiteSpace(nombreNormalizado))
            return ErrorNegocio.Validacion("nombre-vacio", "Escribe el nombre o razón social del cliente.");

        // Los genéricos traen sus propias reglas fijas y no pasan por las demás.
        if (Shared.Comun.Rfc.EsGenerico(rfcNormalizado))
            return ValidarGenerico(rfcNormalizado, peticion, nombreNormalizado);

        var regimen = await baseDeDatos.SatRegimenesFiscales
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Clave == peticion.RegimenFiscal, ct);

        if (regimen is null)
            return ErrorNegocio.Validacion("regimen-desconocido",
                $"El régimen fiscal {peticion.RegimenFiscal} no está en el catálogo del SAT.");

        var esMoral = Shared.Comun.Rfc.EsPersonaMoral(rfcNormalizado);

        if (esMoral && !regimen.AplicaMoral)
            return ErrorNegocio.Validacion("regimen-incompatible",
                $"«{regimen.Descripcion}» es un régimen de personas físicas, y este RFC es de persona moral. " +
                "Revisa la constancia del cliente.");

        if (!esMoral && !regimen.AplicaFisica)
            return ErrorNegocio.Validacion("regimen-incompatible",
                $"«{regimen.Descripcion}» es un régimen de personas morales, y este RFC es de persona física. " +
                "Revisa la constancia del cliente.");

        var existeCp = await baseDeDatos.SatCodigosPostales
            .AsNoTracking()
            .AnyAsync(c => c.Clave == peticion.DomicilioFiscalCp, ct);

        if (!existeCp)
            return ErrorNegocio.Validacion("cp-desconocido",
                $"El código postal {peticion.DomicilioFiscalCp} no existe en el catálogo del SAT. " +
                "Tiene que ser el de la constancia, no el de entrega.");

        // El uso de CFDI es opcional aquí —es una preferencia para precargar— pero si viene,
        // tiene que ser compatible: guardar una preferencia imposible solo aplaza el rechazo
        // hasta el momento de timbrar.
        if (!string.IsNullOrWhiteSpace(peticion.UsoCfdiPreferido))
        {
            var compatible = await catalogos.EsUsoCfdiCompatibleAsync(
                peticion.UsoCfdiPreferido, peticion.RegimenFiscal, esMoral, ct);

            if (!compatible)
            {
                var uso = await catalogos.ResolverAsync("c_UsoCFDI", peticion.UsoCfdiPreferido, ct);

                return ErrorNegocio.Validacion("uso-cfdi-incompatible",
                    $"El uso «{uso?.Descripcion ?? peticion.UsoCfdiPreferido}» no lo admite el régimen " +
                    $"«{regimen.Descripcion}». El SAT rechazaría la factura. " +
                    "Elige otro uso o revisa el régimen del cliente.");
            }
        }

        var errorClaves = await ValidarClavesDeCatalogoAsync(peticion, ct);
        if (errorClaves is not null) return errorClaves;

        // Residencia fiscal solo aplica al genérico de extranjero; en cualquier otro RFC es
        // un dato que el SAT no espera y que haría fallar la validación del comprobante.
        if (!string.IsNullOrWhiteSpace(peticion.ResidenciaFiscal))
            return ErrorNegocio.Validacion("residencia-no-aplica",
                "La residencia fiscal solo se captura para el RFC genérico de extranjero (XEXX010101000).");

        return null;
    }

    /// <summary>
    /// Reglas fijas de los dos genéricos (ARQUITECTURA.md §7). Para el nacional el SAT no admite
    /// variaciones: nombre, régimen y uso son exactamente esos tres valores.
    /// </summary>
    private static ErrorNegocio? ValidarGenerico(
        string rfc, PeticionGuardarCliente peticion, string nombreNormalizado)
    {
        if (rfc == Shared.Comun.Rfc.GenericoNacional)
        {
            if (nombreNormalizado != "PUBLICO EN GENERAL")
                return ErrorNegocio.Validacion("generico-nombre",
                    "Para el RFC XAXX010101000 el nombre tiene que ser exactamente PUBLICO EN GENERAL.");

            if (peticion.RegimenFiscal != "616")
                return ErrorNegocio.Validacion("generico-regimen",
                    "Para el RFC XAXX010101000 el régimen tiene que ser 616, Sin obligaciones fiscales.");

            if (!string.IsNullOrWhiteSpace(peticion.UsoCfdiPreferido) && peticion.UsoCfdiPreferido != "S01")
                return ErrorNegocio.Validacion("generico-uso",
                    "Para el RFC XAXX010101000 el uso de CFDI tiene que ser S01, Sin efectos fiscales.");

            return null;
        }

        // Extranjero: el SAT sí espera residencia fiscal y registro de identidad tributaria.
        if (string.IsNullOrWhiteSpace(peticion.ResidenciaFiscal))
            return ErrorNegocio.Validacion("extranjero-sin-residencia",
                "Para el RFC XEXX010101000 hay que capturar el país de residencia fiscal.");

        return null;
    }

    /// <summary>Método de pago y forma de pago, si vienen, tienen que existir en el catálogo.</summary>
    private async Task<ErrorNegocio?> ValidarClavesDeCatalogoAsync(
        PeticionGuardarCliente peticion, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(peticion.MetodoPagoPreferido))
        {
            var existe = await baseDeDatos.SatMetodosPago
                .AsNoTracking().AnyAsync(m => m.Clave == peticion.MetodoPagoPreferido, ct);

            if (!existe)
                return ErrorNegocio.Validacion("metodo-pago-desconocido",
                    $"El método de pago {peticion.MetodoPagoPreferido} no está en el catálogo del SAT.");
        }

        if (!string.IsNullOrWhiteSpace(peticion.FormaPagoPreferida))
        {
            var existe = await baseDeDatos.SatFormasPago
                .AsNoTracking().AnyAsync(f => f.Clave == peticion.FormaPagoPreferida, ct);

            if (!existe)
                return ErrorNegocio.Validacion("forma-pago-desconocida",
                    $"La forma de pago {peticion.FormaPagoPreferida} no está en el catálogo del SAT.");
        }

        return null;
    }
}
