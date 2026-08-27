using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>Una membresía en el histórico de la cuenta.</summary>
public sealed record MembresiaDeOperadorDto(
    Guid Id,
    Guid CuentaId,
    string CuentaNombre,
    DateTime InicioUtc,
    DateTime FinUtc,
    string Estado,
    DateTime CreadaUtc);

/// <summary>
/// Alta o renovación de la suscripción de una cuenta.
/// <para>
/// Aquí el identificador de cuenta <b>sí</b> viaja en el cuerpo, y es legítimo: el operador no
/// tiene tenencia que deducir del token —no pertenece a ninguna cuenta— así que su aislamiento
/// es la política del panel, no un claim de empresa. No es una excepción a ARQUITECTURA.md §4,
/// que habla de que el inquilino no mande la empresa sobre la que actúa.
/// </para>
/// </summary>
public sealed record PeticionRegistrarMembresia(
    Guid CuentaId,
    DateTime InicioUtc,
    DateTime FinUtc);

/// <summary>Las cifras del negocio del SaaS.</summary>
/// <param name="ComprasPendientes">Cuántas esperan que se acredite el pago.</param>
/// <param name="MontoPendiente">Cuánto suman esas compras: es dinero por cobrar.</param>
/// <param name="IngresoDelMes">Suma de lo acreditado en el mes en curso.</param>
/// <param name="TimbresVendidosDelMes">Timbres entregados en el mes, ya acreditados.</param>
/// <param name="MembresiasPorVencer">Suscripciones que caducan en los próximos treinta días.</param>
public sealed record TableroDeOperadorDto(
    int ComprasPendientes,
    decimal MontoPendiente,
    decimal IngresoDelMes,
    int TimbresVendidosDelMes,
    int CuentasActivas,
    int EmpresasActivas,
    int MembresiasPorVencer,
    IReadOnlyList<CompraDeOperadorDto> UltimasAcreditadas,
    IReadOnlyList<PuntoMensualDto> PorMes,
    IReadOnlyList<VentaPorPaqueteDto> PorPaquete);

/// <summary>
/// Un mes de la serie histórica. Se manda el mes ya con etiqueta hecha para que el navegador
/// no tenga que reconstruir nombres de mes ni preocuparse de la zona horaria.
/// </summary>
public sealed record PuntoMensualDto(
    string Etiqueta,
    decimal Ingreso,
    int Timbres);

/// <summary>Cuánto se ha vendido de cada paquete; sirve para saber cuál sostiene el negocio.</summary>
public sealed record VentaPorPaqueteDto(
    string Nombre,
    int Compras,
    decimal Ingreso);
