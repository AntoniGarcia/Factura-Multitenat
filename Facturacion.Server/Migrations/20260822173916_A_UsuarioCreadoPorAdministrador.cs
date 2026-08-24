using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_UsuarioCreadoPorAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitaciones");

            migrationBuilder.AddColumn<bool>(
                name: "CreadoPorAdministrador",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "administrar_usuarios",
                column: "Descripcion",
                value: "Dar de alta usuarios y asignar permisos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreadoPorAdministrador",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "Invitaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AceptadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    CreadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HashToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PermisosClaves = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RevocadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitaciones", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "administrar_usuarios",
                column: "Descripcion",
                value: "Invitar usuarios y asignar permisos");

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
    }
}
