using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_B_ClasificacionCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Remolque",
                table: "SatConfiguracionAutotransporte",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialPeligroso",
                table: "SatClaveProdServCartaPorte",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialPeligroso",
                table: "MercanciasCartaPorte",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remolque",
                table: "SatConfiguracionAutotransporte");

            migrationBuilder.DropColumn(
                name: "MaterialPeligroso",
                table: "SatClaveProdServCartaPorte");

            migrationBuilder.DropColumn(
                name: "MaterialPeligroso",
                table: "MercanciasCartaPorte");
        }
    }
}
