using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260924_R_OrigenCartaPorteEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CartaPorteCalle",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaPorteCodigoPostal",
                table: "Empresas",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaPorteEstado",
                table: "Empresas",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaPorteMunicipio",
                table: "Empresas",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaPorteNumeroExterior",
                table: "Empresas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaPorteNumeroInterior",
                table: "Empresas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CartaPorteCalle",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CartaPorteCodigoPostal",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CartaPorteEstado",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CartaPorteMunicipio",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CartaPorteNumeroExterior",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CartaPorteNumeroInterior",
                table: "Empresas");
        }
    }
}
