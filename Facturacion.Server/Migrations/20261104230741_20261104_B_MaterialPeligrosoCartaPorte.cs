using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_B_MaterialPeligrosoCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AseguraMedAmbiente",
                table: "TrasladosCartaPorte",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolizaMedAmbiente",
                table: "TrasladosCartaPorte",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CveMaterialPeligroso",
                table: "MercanciasCartaPorte",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescripEmbalaje",
                table: "MercanciasCartaPorte",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Embalaje",
                table: "MercanciasCartaPorte",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SatMaterialPeligroso",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatMaterialPeligroso", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTipoEmbalaje",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatTipoEmbalaje", x => x.Clave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatMaterialPeligroso");

            migrationBuilder.DropTable(
                name: "SatTipoEmbalaje");

            migrationBuilder.DropColumn(
                name: "AseguraMedAmbiente",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "PolizaMedAmbiente",
                table: "TrasladosCartaPorte");

            migrationBuilder.DropColumn(
                name: "CveMaterialPeligroso",
                table: "MercanciasCartaPorte");

            migrationBuilder.DropColumn(
                name: "DescripEmbalaje",
                table: "MercanciasCartaPorte");

            migrationBuilder.DropColumn(
                name: "Embalaje",
                table: "MercanciasCartaPorte");
        }
    }
}
