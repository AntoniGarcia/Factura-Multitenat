using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Data;

/// <summary>Filtra las lecturas de entidades con empresa obligatoria u opcional. Identidad, pertenencias, cuenta y catálogos comunes se controlan por separado. Sin empresa activa no devuelve datos de empresa; las escrituras las valida SelladoDeEmpresaInterceptor.</summary>
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
