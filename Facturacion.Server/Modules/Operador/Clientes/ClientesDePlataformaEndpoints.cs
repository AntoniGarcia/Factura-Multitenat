using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Operador.Paquetes;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Las cuentas contratantes desde el panel del proveedor.
/// <para>
/// Las da de baja alta/reactivar una empresa con su motivo, y cambiar su dato de
/// contacto. Desactivar una cuenta completa sigue fuera de alcance: deja fuera a todos sus
/// usuarios y sus empresas de golpe, y merece su propio diseño.
/// </para>
/// </summary>
public static class ClientesDePlataformaEndpoints
{
    public static void MapClientesDePlataforma(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/cuentas")
            .WithTags("Operador · Clientes")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar).RequireAuthorization(PoliticasDeOperador.VerClientes);
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization(PoliticasDeOperador.VerClientes);
        grupo.MapGet("/{id:guid}/empresas/{empresaId:guid}", ObtenerEmpresa).RequireAuthorization(PoliticasDeOperador.VerClientes);

        grupo.MapPost("/{id:guid}/empresas/{empresaId:guid}/activo", CambiarActivoEmpresa);
        grupo.MapPost("/{id:guid}/empresas/{empresaId:guid}/licencias", ActualizarLicencias)
            .RequireAuthorization(PoliticasDeOperador.GestionarLicencias);
        grupo.MapPost("/{id:guid}/correo-contacto", CambiarCorreoDeContacto)
            .RequireAuthorization(PoliticasDeOperador.EditarContactoCuentas);
        grupo.MapPut("/{id:guid}/titular/contrasena", async (Guid id, PeticionRestablecerContrasenaDeUsuario peticion,
            ServicioDeUsuariosDePlataforma usuarios, HttpContext contexto, CancellationToken ct) =>
        {
            if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();
            var resultado = await usuarios.RestablecerContrasenaDelTitularAsync(operadorId, id, peticion, ct);
            return resultado.EsExito ? Results.NoContent() : resultado.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.VerClientes, PoliticasDeOperador.RestablecerContrasenasUsuarios);
        grupo.MapPost("/{id:guid}/limites", CambiarLimites)
            .RequireAuthorization(PoliticasDeOperador.GestionarLimites);
        // Los paquetes exclusivos son de la cuenta, igual que la bolsa que recibe sus timbres.
        grupo.MapPost("/{id:guid}/paquetes", CrearPaquete)
            .RequireAuthorization(PoliticasDeOperador.CrearPaquetesExclusivos);
        grupo.MapPut("/{id:guid}/paquetes/{paqueteId:guid}", ActualizarPaquete)
            .RequireAuthorization(PoliticasDeOperador.EditarPaquetesExclusivos);
        grupo.MapPost("/{id:guid}/paquetes/{paqueteId:guid}/activo", CambiarActivoPaquete);
        grupo.MapDelete("/{id:guid}/paquetes/{paqueteId:guid}", async (Guid id, Guid paqueteId,
            ServicioDePaquetesDeOperador paquetes, HttpContext contexto, CancellationToken ct) =>
        {
            var resultado = await paquetes.EliminarAsync(paqueteId, id, ct);
            return resultado.EsExito ? Results.NoContent() : resultado.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.VerClientes, PermisosDePanel.EliminarPaquetesExclusivos);
    }

    private static async Task<IResult> Listar(
        ServicioDeClientesDePlataforma clientes,
        CancellationToken ct,
        string? texto = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await clientes.ListarAsync(texto, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> Obtener(
        Guid id, ServicioDeClientesDePlataforma clientes, CancellationToken ct)
    {
        var cuenta = await clientes.ObtenerAsync(id, ct);

        return cuenta is null ? Results.NotFound() : Results.Ok(cuenta);
    }

    private static async Task<IResult> ObtenerEmpresa(
        Guid id, Guid empresaId, ServicioDeClientesDePlataforma clientes, CancellationToken ct, string? texto = null)
    {
        var empresa = await clientes.ObtenerEmpresaAsync(id, empresaId, texto, ct);

        return empresa is null ? Results.NotFound() : Results.Ok(empresa);
    }

    private static async Task<IResult> CambiarActivoEmpresa(
        Guid id,
        Guid empresaId,
        PeticionCambiarActivoDeEmpresa peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        if (!PermisosDePanel.Autoriza(contexto.User.FindAll(ClavesDeClaim.Permiso).Select(c => c.Value),
                peticion.Activo ? PermisosDePanel.ReactivarEmpresas : PermisosDePanel.DesactivarEmpresas))
            return Results.Forbid(authenticationSchemes: [EsquemasDeAutenticacion.Operador]);

        var resultado = await clientes.CambiarActivoEmpresaAsync(operadorId, id, empresaId, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> ActualizarLicencias(
        Guid id,
        Guid empresaId,
        PeticionActualizarLicenciasDeEmpresa peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await clientes.ActualizarLicenciasAsync(operadorId, id, empresaId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarCorreoDeContacto(
        Guid id,
        PeticionCambiarCorreoDeContactoDeCuenta peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await clientes.CambiarCorreoDeContactoDeCuentaAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarLimites(
        Guid id,
        PeticionCambiarLimitesDeCuenta peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await clientes.CambiarLimitesAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CrearPaquete(
        Guid id,
        PeticionGuardarPaquetePersonalizado peticion,
        ServicioDePaquetesPersonalizados paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await paquetes.CrearAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> ActualizarPaquete(
        Guid id, Guid paqueteId,
        PeticionGuardarPaquetePersonalizado peticion,
        ServicioDePaquetesPersonalizados paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await paquetes.ActualizarAsync(
            operadorId, id, paqueteId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarActivoPaquete(
        Guid id, Guid paqueteId,
        PeticionCambiarActivoPaquete peticion,
        ServicioDePaquetesPersonalizados paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        if (!PermisosDePanel.Autoriza(contexto.User.FindAll(ClavesDeClaim.Permiso).Select(c => c.Value),
                peticion.Activo ? PermisosDePanel.ReactivarPaquetesExclusivos : PermisosDePanel.DesactivarPaquetesExclusivos))
            return Results.Forbid(authenticationSchemes: [EsquemasDeAutenticacion.Operador]);

        var resultado = await paquetes.CambiarActivoAsync(
            operadorId, id, paqueteId, peticion.Activo, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}
