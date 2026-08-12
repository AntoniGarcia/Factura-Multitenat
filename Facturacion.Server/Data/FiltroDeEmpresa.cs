using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Data;

/// <summary>
/// Aplica el aislamiento por empresa a todo el modelo, de una vez. Vive fuera de
/// <see cref="AppDbContext"/> para no meter Fluent API en el archivo compartido.
///
/// <para><b>Qué queda cubierto</b></para>
/// Toda entidad que implemente <see cref="IEntidadDeEmpresa"/> o
/// <see cref="IEntidadDeEmpresaOpcional"/>. Nadie tiene que acordarse de filtrar: basta
/// implementar la interfaz al declarar la entidad.
///
/// <para><b>Qué queda fuera, y por qué</b></para>
/// <list type="bullet">
///   <item><description>
///     <b>Tablas de Identity</b> — un usuario existe antes de elegir empresa y puede
///     pertenecer a varias. Se acotan por cuenta y por el usuario del token.
///   </description></item>
///   <item><description>
///     <b><c>Cuenta</c> y <c>Empresa</c></b> — filtrar las empresas por la empresa activa
///     haría imposible listar las empresas del usuario, que es lo que necesita el selector
///     de la barra superior. Se acotan siempre por cuenta.
///   </description></item>
///   <item><description>
///     <b><c>UsuarioEmpresa</c> y <c>UsuarioEmpresaPermiso</c></b> — llevan
///     <c>EmpresaId</c>, pero son el cableado de la tenencia, no datos de una empresa.
///     Armar el token exige leerlos a través de todas las empresas del usuario.
///   </description></item>
///   <item><description>
///     <b><c>RefreshToken</c></b> — la sesión sobrevive al cambio de empresa activa: ese
///     cambio emite un access token nuevo pero no toca la cookie.
///   </description></item>
///   <item><description>
///     <b><c>Permiso</c></b> — catálogo fijo de seis valores, igual para todos.
///   </description></item>
///   <item><description>
///     <b>Catálogos del SAT</b> (fase 3) — son del SAT, no de ninguna empresa.
///   </description></item>
/// </list>
///
/// <para><b>Falla cerrado</b></para>
/// Sin empresa activa el parámetro va nulo y la comparación en SQL Server queda en
/// desconocido, así que no se devuelve ningún renglón. Es la dirección correcta de fallo:
/// ante un error de configuración se ve de menos, nunca de más.
///
/// <para><b>Lo que este filtro no hace</b></para>
/// No protege escrituras: un <c>Add</c> con la empresa equivocada entraría igual. De eso se
/// encarga <c>SelladoDeEmpresaInterceptor</c>.
/// </summary>
public static class FiltroDeEmpresa
{
    public static void AplicarFiltroDeEmpresa(this ModelBuilder constructor, AppDbContext contexto)
    {
        foreach (var entidad in constructor.Model.GetEntityTypes())
        {
            var tipo = entidad.ClrType;

            var filtro =
                typeof(IEntidadDeEmpresa).IsAssignableFrom(tipo) ? Obligatorio(tipo, contexto) :
                typeof(IEntidadDeEmpresaOpcional).IsAssignableFrom(tipo) ? Opcional(tipo, contexto) :
                null;

            if (filtro is not null)
                constructor.Entity(tipo).HasQueryFilter(filtro);
        }
    }

    // e => (Guid?)e.EmpresaId == contexto.EmpresaActual
    private static LambdaExpression Obligatorio(Type tipo, AppDbContext contexto)
    {
        var e = Expression.Parameter(tipo, "e");
        var columna = Expression.Convert(
            Expression.Property(e, nameof(IEntidadDeEmpresa.EmpresaId)), typeof(Guid?));

        return Expression.Lambda(Expression.Equal(columna, EmpresaActual(contexto)), e);
    }

    // e => e.EmpresaId != null && e.EmpresaId == contexto.EmpresaActual
    // La comparación contra nulo es explícita para que un contexto sin empresa activa no
    // devuelva los renglones de alcance de cuenta en lugar de ninguno.
    private static LambdaExpression Opcional(Type tipo, AppDbContext contexto)
    {
        var e = Expression.Parameter(tipo, "e");
        var columna = Expression.Property(e, nameof(IEntidadDeEmpresaOpcional.EmpresaId));

        var cuerpo = Expression.AndAlso(
            Expression.NotEqual(columna, Expression.Constant(null, typeof(Guid?))),
            Expression.Equal(columna, EmpresaActual(contexto)));

        return Expression.Lambda(cuerpo, e);
    }

    // Acceso a un miembro del propio DbContext: Entity Framework lo sustituye por el
    // contexto de la consulta en curso, así que el valor se reevalúa por consulta y el
    // modelo compilado se puede seguir compartiendo entre peticiones.
    private static MemberExpression EmpresaActual(AppDbContext contexto)
        => Expression.Property(Expression.Constant(contexto), nameof(AppDbContext.EmpresaActual));
}
