using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;

namespace Facturacion.Server.Modules.Documentos.Traslados;

public static class ReglasDeRemolques
{
    public static string NormalizarPlaca(string? placa)
        => string.Concat((placa ?? "").Where(c => c != '-' && !char.IsWhiteSpace(c))).ToUpperInvariant();

    public static ErrorNegocio? ValidarDatos(IReadOnlyList<RemolqueCartaPorteDto> remolques)
    {
        if (remolques.Count > 2)
            return ErrorNegocio.Validacion("remolques-excedidos", "Registra como máximo dos remolques.");
        if (remolques.Any(x => x is null || string.IsNullOrWhiteSpace(x.SubTipoRem) || x.SubTipoRem.Trim().Length > 6 ||
                NormalizarPlaca(x.Placa) is not { Length: >= 5 and <= 7 } ||
                NormalizarPlaca(x.Placa).Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9'))))
            return ErrorNegocio.Validacion("remolque-invalido", "Cada remolque requiere subtipo SAT y placa de 5 a 7 letras o números.");
        if (remolques.Select(x => NormalizarPlaca(x.Placa)).Distinct().Count() != remolques.Count)
            return ErrorNegocio.Validacion("remolque-repetido", "Las placas de los remolques no deben repetirse.");
        return null;
    }

    public static ErrorNegocio? ValidarConfiguracion(string? indicador, int cantidad)
        => indicador switch
        {
            "0" when cantidad > 0 => ErrorNegocio.Validacion("configuracion-sin-remolques", "La configuración del vehículo no permite remolques."),
            "1" when cantidad == 0 => ErrorNegocio.Validacion("remolque-obligatorio", "La configuración del vehículo requiere al menos un remolque."),
            "0" or "1" or "0,1" => null,
            _ => ErrorNegocio.Regla("configuracion-porte-sin-datos", "Actualiza el catálogo de configuraciones de autotransporte.")
        };
}
