using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_B_IshEnFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AplicaIsh",
                table: "Conceptos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TasaIsh",
                table: "Comprobantes",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AplicaIsh",
                table: "Conceptos");

            migrationBuilder.DropColumn(
                name: "TasaIsh",
                table: "Comprobantes");
        }
    }
}
