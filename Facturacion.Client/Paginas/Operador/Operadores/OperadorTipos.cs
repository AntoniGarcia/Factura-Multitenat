namespace Facturacion.Client.Paginas.Operador.Operadores;

/// <summary>Modo del formulario de operador.</summary>
public enum ModoFormulario { Crear, Editar }

/// <summary>Resultado del diálogo de creación/edición de operador.</summary>
public sealed record RespuestaFormularioOperador(string Mensaje);