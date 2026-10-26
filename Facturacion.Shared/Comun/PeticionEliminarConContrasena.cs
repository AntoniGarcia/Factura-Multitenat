using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Comun;

public sealed record PeticionEliminarConContrasena(
    [property: Required, StringLength(128)] string Contrasena);
