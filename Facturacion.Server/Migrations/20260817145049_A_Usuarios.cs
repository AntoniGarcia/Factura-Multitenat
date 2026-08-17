using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_Usuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invitaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HashToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PermisosClaves = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AceptadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_HashToken",
                table: "Invitaciones",
                column: "HashToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_PendientePorCorreoYEmpresa",
                table: "Invitaciones",
                columns: new[] { "EmpresaId", "Correo" },
                unique: true,
                filter: "[AceptadaUtc] IS NULL AND [RevocadaUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitaciones");
        }
    }
}
