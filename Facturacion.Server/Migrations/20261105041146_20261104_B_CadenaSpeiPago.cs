using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_B_CadenaSpeiPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CadPago",
                table: "Pagos",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertPago",
                table: "Pagos",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelloPago",
                table: "Pagos",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoCadPago",
                table: "Pagos",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CadPago",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CertPago",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "SelloPago",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "TipoCadPago",
                table: "Pagos");
        }
    }
}
