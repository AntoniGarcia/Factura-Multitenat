using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261124_B_XmlTimbradoEnBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Acuse",
                table: "SolicitudesCancelacion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "XmlsDeComprobante",
                columns: table => new
                {
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GuardadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XmlsDeComprobante", x => x.ComprobanteId);
                    table.ForeignKey(
                        name: "FK_XmlsDeComprobante_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XmlsDeComprobante");

            migrationBuilder.DropColumn(
                name: "Acuse",
                table: "SolicitudesCancelacion");
        }
    }
}
