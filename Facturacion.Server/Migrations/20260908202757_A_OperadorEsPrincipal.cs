using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_OperadorEsPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPrincipal",
                table: "OperadoresPlataforma",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill: el operador más antiguo es el dueño del SaaS (el que se dio de alta
            // por consola al crear la base). Se marca como principal para que la regla
            // «no se desactiva» aplique desde el primer día sin depender de quién lo cree.
            migrationBuilder.Sql("""
                UPDATE OperadoresPlataforma
                SET EsPrincipal = 1
                WHERE Id = (SELECT TOP (1) Id FROM OperadoresPlataforma ORDER BY FechaAltaUtc, Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsPrincipal",
                table: "OperadoresPlataforma");
        }
    }
}
