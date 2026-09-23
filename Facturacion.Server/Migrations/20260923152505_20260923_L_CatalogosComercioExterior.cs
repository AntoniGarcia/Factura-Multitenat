using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260923_L_CatalogosComercioExterior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SatFraccionArancelaria",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatFraccionArancelaria", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatIncoterm",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatIncoterm", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatUnidadAduana",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatUnidadAduana", x => x.Clave);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatFraccionArancelaria_Vigente_Clave",
                table: "SatFraccionArancelaria",
                columns: new[] { "Vigente", "Clave" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatFraccionArancelaria");

            migrationBuilder.DropTable(
                name: "SatIncoterm");

            migrationBuilder.DropTable(
                name: "SatUnidadAduana");
        }
    }
}
