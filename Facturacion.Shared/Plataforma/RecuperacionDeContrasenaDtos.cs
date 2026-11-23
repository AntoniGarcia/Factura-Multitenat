namespace Facturacion.Shared.Plataforma;

public sealed record PeticionRecuperarContrasena(string Correo);

public sealed record PeticionRestablecerContrasena(string Correo, string Codigo, string ContrasenaNueva);
