using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_AltaConVerificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AltasPendientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NombreCuenta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HashCodigo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    ConsumidoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpCreacion = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltasPendientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AltasPendientes_Correo",
                table: "AltasPendientes",
                column: "Correo");

            migrationBuilder.CreateIndex(
                name: "IX_AltasPendientes_Expira",
                table: "AltasPendientes",
                column: "ExpiraUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AltasPendientes");
        }
    }
}
