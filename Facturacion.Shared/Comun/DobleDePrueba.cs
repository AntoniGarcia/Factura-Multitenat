namespace Facturacion.Shared.Comun;

/// <summary>
/// Marca una implementación de contrato como doble de prueba: algo que devuelve datos
/// inventados o fijos para que la otra mitad pueda avanzar antes de que exista lo real.
///
/// <para><b>Para qué sirve de verdad</b></para>
/// Un doble en desarrollo es útil; el mismo doble en producción emite comprobantes contra
/// datos falsos y nadie se entera hasta que el SAT rechaza. La verificación de arranque
/// (<c>VerificacionDeContratos</c>) busca esta marca y **revienta el arranque** si encuentra
/// un doble registrado fuera de <c>Development</c>.
///
/// <para>
/// Ponerla es responsabilidad de quien escribe el doble. Como olvidarla es justo el error
/// que se quiere evitar, la verificación además reconoce por nombre los prefijos y sufijos
/// habituales (<c>Doble</c>, <c>Falso</c>, <c>Fake</c>, <c>Stub</c>, <c>Simulado</c>); la
/// marca explícita es la que no depende de cómo se llame la clase.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DobleDePruebaAttribute : Attribute;
