using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261007_B_ReceptorExtranjero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceptorNumRegIdTrib",
                table: "Comprobantes",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceptorResidenciaFiscal",
                table: "Comprobantes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceptorNumRegIdTrib",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "ReceptorResidenciaFiscal",
                table: "Comprobantes");
        }
    }
}
