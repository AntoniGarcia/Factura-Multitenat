using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260922_H_PartesNotariales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdquirentesEnCopropiedad",
                table: "DatosNotaria",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnajenantesEnCopropiedad",
                table: "DatosNotaria",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PartesNotariales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatosNotariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Curp = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    Porcentaje = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartesNotariales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartesNotariales_DatosNotaria_DatosNotariaId",
                        column: x => x.DatosNotariaId,
                        principalTable: "DatosNotaria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartesNotariales_DatosNotariaId_Rol_Orden",
                table: "PartesNotariales",
                columns: new[] { "DatosNotariaId", "Rol", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartesNotariales");

            migrationBuilder.DropColumn(
                name: "AdquirentesEnCopropiedad",
                table: "DatosNotaria");

            migrationBuilder.DropColumn(
                name: "EnajenantesEnCopropiedad",
                table: "DatosNotaria");
        }
    }
}
