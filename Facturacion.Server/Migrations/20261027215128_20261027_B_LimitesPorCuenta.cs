using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261027_B_LimitesPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LimiteDeEmpresas",
                table: "Cuentas",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "LimiteDeUsuarios",
                table: "Cuentas",
                type: "int",
                nullable: false,
                defaultValue: 3);

            // Ninguna cuenta existente queda pasada de su límite (AGENTS.md §11, 27 de octubre):
            // se queda con el mayor entre lo que ya usa y el valor por omisión. Se cuenta igual
            // que LimitesDeCuenta: usuarios activos con acceso activo a alguna empresa, y todas
            // las empresas, activas o no.
            migrationBuilder.Sql("""
                UPDATE c SET
                    LimiteDeUsuarios = CASE WHEN u.Total > 3 THEN u.Total ELSE 3 END,
                    LimiteDeEmpresas = CASE WHEN e.Total > 1 THEN e.Total ELSE 1 END
                FROM Cuentas c
                CROSS APPLY (
                    SELECT COUNT(*) AS Total FROM AspNetUsers us
                    WHERE us.CuentaId = c.Id AND us.Activo = 1
                      AND EXISTS (SELECT 1 FROM UsuariosEmpresas ue WHERE ue.UsuarioId = us.Id AND ue.Activo = 1)
                ) u
                CROSS APPLY (SELECT COUNT(*) AS Total FROM Empresas em WHERE em.CuentaId = c.Id) e;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimiteDeEmpresas",
                table: "Cuentas");

            migrationBuilder.DropColumn(
                name: "LimiteDeUsuarios",
                table: "Cuentas");
        }
    }
}
