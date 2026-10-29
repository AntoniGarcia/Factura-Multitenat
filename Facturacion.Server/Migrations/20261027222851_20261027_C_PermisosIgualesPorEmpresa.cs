using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261027_C_PermisosIgualesPorEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Desde el 27 de octubre (AGENTS.md §11) un usuario tiene los mismos permisos en todas
            // sus empresas. Quien los tenía distintos queda con la unión en cada una: nadie pierde
            // lo que podía hacer. Se completan también los accesos inactivos, para que al
            // reactivarlos entren con el juego vigente. El titular no tiene renglones.
            migrationBuilder.Sql("""
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT ue.UsuarioId, ue.EmpresaId, c.PermisoClave, SYSUTCDATETIME(), o.OtorgadoPorUsuarioId
                FROM UsuariosEmpresas ue
                JOIN AspNetUsers u ON u.Id = ue.UsuarioId AND u.EsTitular = 0
                CROSS APPLY (
                    SELECT DISTINCT p.PermisoClave FROM UsuariosEmpresasPermisos p WHERE p.UsuarioId = ue.UsuarioId
                ) c
                CROSS APPLY (
                    SELECT TOP 1 p.OtorgadoPorUsuarioId FROM UsuariosEmpresasPermisos p
                    WHERE p.UsuarioId = ue.UsuarioId AND p.PermisoClave = c.PermisoClave
                    ORDER BY p.OtorgadoUtc
                ) o
                WHERE NOT EXISTS (
                    SELECT 1 FROM UsuariosEmpresasPermisos x
                    WHERE x.UsuarioId = ue.UsuarioId AND x.EmpresaId = ue.EmpresaId AND x.PermisoClave = c.PermisoClave);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: los renglones completados no se distinguen de los que ya estaban,
            // y dejarlos no rompe nada con el modelo anterior, que leía cada empresa por separado.
        }
    }
}
