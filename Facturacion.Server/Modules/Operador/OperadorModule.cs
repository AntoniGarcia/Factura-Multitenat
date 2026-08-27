using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Operador.Clientes;
using Facturacion.Server.Modules.Operador.Compras;
using Facturacion.Server.Modules.Operador.Membresias;
using Facturacion.Server.Modules.Operador.Paquetes;
using Facturacion.Server.Modules.Operador.Tablero;
using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Modules.Operador;

/// <summary>
/// Punto de entrada del panel del proveedor del SaaS: administración de los paquetes que se
/// venden, acreditación de pagos y consulta de las cuentas contratantes.
///
/// <para>
/// Es un módulo aparte del de plataforma aunque compartan base de datos, porque atiende a la
/// otra identidad del sistema. Todo lo que cuelgue de aquí exige la política de operador y
/// nunca la de un permiso de empresa.
/// </para>
/// </summary>
public static class OperadorModule
{
    public static IServiceCollection AddOperador(this IServiceCollection servicios)
    {
        // El mismo PBKDF2 con el que Identity cifra las contraseñas de los inquilinos, usado
        // suelto: el operador no vive en AspNetUsers, pero no hay razón para que su contraseña
        // se guarde peor.
        servicios.AddSingleton<IPasswordHasher<OperadorPlataforma>, PasswordHasher<OperadorPlataforma>>();

        servicios.AddScoped<ServicioDeRefreshTokensDeOperador>();
        servicios.AddScoped<ServicioDeAutenticacionDeOperador>();
        servicios.AddScoped<ServicioDePerfilDeOperador>();
        servicios.AddScoped<ServicioDePaquetesDeOperador>();
        servicios.AddScoped<ServicioDeComprasDeOperador>();
        servicios.AddScoped<ServicioDeClientesDePlataforma>();
        servicios.AddScoped<ServicioDeMembresias>();
        servicios.AddScoped<ServicioDeTableroDeOperador>();

        return servicios;
    }

    public static WebApplication MapOperador(this WebApplication aplicacion)
    {
        aplicacion.MapOperadorAuth();
        aplicacion.MapPaquetesDeOperador();
        aplicacion.MapComprasDeOperador();
        aplicacion.MapClientesDePlataforma();
        aplicacion.MapMembresiasYTablero();

        return aplicacion;
    }
}
