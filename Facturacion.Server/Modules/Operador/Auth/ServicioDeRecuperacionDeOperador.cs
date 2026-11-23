using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Facturacion.Server.Infra.Seguridad.RecuperacionDeContrasena;
namespace Facturacion.Server.Modules.Operador.Auth;

public sealed class ServicioDeRecuperacionDeOperador(
    AppDbContext baseDeDatos,
    ColaDeRecuperacionDeOperador cola,
    IPasswordHasher<OperadorPlataforma> hasher,
    ServicioDeRefreshTokensDeOperador refrescos,
    IServicioDeBitacora bitacora)
{
    public async Task<Resultado> RestablecerAsync(PeticionRestablecerContrasenaOperador peticion, CancellationToken ct)
    {
        if (peticion.ContrasenaNueva is not { Length: >= 12 and <= 128 } || string.IsNullOrWhiteSpace(peticion.ContrasenaNueva))
            return ErrorNegocio.Validacion("contrasena-invalida", "Escribe una contraseña mayor a 12 caracteres .");

        var correo = NormalizarCorreo(peticion.Correo);
        if (correo is null || peticion.Codigo?.Trim() is not { Length: 12 } codigo ||
            cola.Validar(correo, codigo.ToUpperInvariant()) is not { } emitido)
            return CodigoInvalido();

        var candidatos = baseDeDatos.OperadoresPlataforma.Where(o => o.Id == emitido.Id && o.Activo &&
            o.CorreoNormalizado == correo && EF.Functions.Collate(o.HashContrasena, "Latin1_General_100_BIN2") == emitido.VersionDeCredencial);
        var operador = await candidatos.AsNoTracking().SingleOrDefaultAsync(ct);
        if (operador is null) return CodigoInvalido();

        var hashNuevo = hasher.HashPassword(operador, peticion.ContrasenaNueva);

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        // El hash anterior liga el código a la contraseña vigente y permite que solo una
        // confirmación gane si se recibe el mismo código simultáneamente.
        var modificados = await candidatos.ExecuteUpdateAsync(s => s
            .SetProperty(o => o.HashContrasena, hashNuevo)
            .SetProperty(o => o.AccesosFallidos, 0)
            .SetProperty(o => o.BloqueosConsecutivos, 0)
            .SetProperty(o => o.BloqueadoHastaUtc, (DateTime?)null), ct);
        if (modificados == 0) return CodigoInvalido();

        var sesionesRevocadas = await refrescos.InvalidarTodasLasFamiliasDelOperadorAsync(
            operador.Id, "recuperación de contraseña", ct);
        bitacora.Registrar(EntidadesDeBitacora.OperadorPlataforma, operador.Id.ToString(),
            AccionesDeBitacora.ContrasenaCambiada, despues: new { PorRecuperacion = true, TokensRevocados = sesionesRevocadas },
            operadorId: operador.Id);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return Resultado.Exito();
    }
}
