using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260922_J_PorcentajesRetencionesObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeDevoluciones",
                table: "DatosObra",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRetenciones",
                table: "DatosObra",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PorcentajeDevoluciones",
                table: "DatosObra");

            migrationBuilder.DropColumn(
                name: "PorcentajeRetenciones",
                table: "DatosObra");
        }
    }
}
