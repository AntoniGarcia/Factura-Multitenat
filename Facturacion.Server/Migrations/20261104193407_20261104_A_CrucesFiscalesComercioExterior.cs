using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_A_CrucesFiscalesComercioExterior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Agrupaciones",
                table: "SatPais",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormatoCodigoPostal",
                table: "SatPais",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormatoRegistroTributario",
                table: "SatPais",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacionRegistroTributario",
                table: "SatPais",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedida",
                table: "SatFraccionArancelaria",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Agrupaciones",
                table: "SatPais");

            migrationBuilder.DropColumn(
                name: "FormatoCodigoPostal",
                table: "SatPais");

            migrationBuilder.DropColumn(
                name: "FormatoRegistroTributario",
                table: "SatPais");

            migrationBuilder.DropColumn(
                name: "ValidacionRegistroTributario",
                table: "SatPais");

            migrationBuilder.DropColumn(
                name: "UnidadMedida",
                table: "SatFraccionArancelaria");
        }
    }
}
