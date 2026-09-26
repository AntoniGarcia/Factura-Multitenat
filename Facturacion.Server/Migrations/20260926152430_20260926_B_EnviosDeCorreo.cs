using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260926_B_EnviosDeCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnviosDeCorreo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destinatarios = table.Column<string>(type: "nvarchar(2600)", maxLength: 2600, nullable: false),
                    CopiaOculta = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IncluyoXml = table.Column<bool>(type: "bit", nullable: false),
                    Exitoso = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EnviadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnviadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosDeCorreo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosDeCorreo_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosDeCorreo_PorComprobante",
                table: "EnviosDeCorreo",
                columns: new[] { "ComprobanteId", "EnviadoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnviosDeCorreo");
        }
    }
}
