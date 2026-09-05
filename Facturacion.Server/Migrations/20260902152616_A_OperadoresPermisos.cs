using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_OperadoresPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperadoresPermisos",
                columns: table => new
                {
                    OperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperadoresPermisos", x => new { x.OperadorId, x.Permiso });
                    table.ForeignKey(
                        name: "FK_OperadoresPermisos_OperadoresPlataforma_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "OperadoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperadoresPermisos_Permiso",
                table: "OperadoresPermisos",
                column: "Permiso");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperadoresPermisos");
        }
    }
}
