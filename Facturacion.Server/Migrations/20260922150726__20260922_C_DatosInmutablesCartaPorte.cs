using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260922_C_DatosInmutablesCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FiguraNombre",
                table: "TrasladosCartaPorte",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiguraNumeroLicencia",
                table: "TrasladosCartaPorte",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiguraRfc",
                table: "TrasladosCartaPorte",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiguraTipo",
                table: "TrasladosCartaPorte",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCcp",
                table: "TrasladosCartaPorte",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehiculoAnioModelo",
                table: "TrasladosCartaPorte",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoAseguradora",
                table: "TrasladosCartaPorte",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoConfiguracionAutotransporte",
                table: "TrasladosCartaPorte",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoNumeroPermiso",
                table: "TrasladosCartaPorte",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoPlaca",
                table: "TrasladosCartaPorte",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoPoliza",
                table: "TrasladosCartaPorte",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiculoTipoPermiso",
                table: "TrasladosCartaPorte",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrasladosCartaPorte_IdCcp",
                table: "TrasladosCartaPorte",
                column: "IdCcp",
                unique: true,
                filter: "[IdCcp] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrasladosCartaPorte_IdCcp",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "FiguraNombre",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "FiguraNumeroLicencia",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "FiguraRfc",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "FiguraTipo",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "IdCcp",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoAnioModelo",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoAseguradora",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoConfiguracionAutotransporte",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoNumeroPermiso",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoPlaca",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoPoliza",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "VehiculoTipoPermiso",
                table: "TrasladosCartaPorte");
        }
    }
}
