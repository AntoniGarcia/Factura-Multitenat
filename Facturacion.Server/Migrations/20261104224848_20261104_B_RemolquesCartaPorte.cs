using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_B_RemolquesCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemolquesCartaPorte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrasladoCartaPorteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    SubTipoRem = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemolquesCartaPorte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemolquesCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId",
                        column: x => x.TrasladoCartaPorteId,
                        principalTable: "TrasladosCartaPorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SatSubTipoRemolque",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatSubTipoRemolque", x => x.Clave);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemolquesCartaPorte_TrasladoCartaPorteId_Orden",
                table: "RemolquesCartaPorte",
                columns: new[] { "TrasladoCartaPorteId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemolquesCartaPorte");

            migrationBuilder.DropTable(
                name: "SatSubTipoRemolque");
        }
    }
}
