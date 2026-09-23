using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260918_A_CatalogosCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SatConfiguracionAutotransporte",
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
                    table.PrimaryKey("PK_SatConfiguracionAutotransporte", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatFiguraTransporte",
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
                    table.PrimaryKey("PK_SatFiguraTransporte", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTipoPermiso",
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
                    table.PrimaryKey("PK_SatTipoPermiso", x => x.Clave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatConfiguracionAutotransporte");

            migrationBuilder.DropTable(
                name: "SatFiguraTransporte");

            migrationBuilder.DropTable(
                name: "SatTipoPermiso");
        }
    }
}
