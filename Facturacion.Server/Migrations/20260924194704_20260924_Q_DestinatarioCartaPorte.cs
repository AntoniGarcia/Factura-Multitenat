using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260924_Q_DestinatarioCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreRemitenteDestinatario",
                table: "UbicacionesCartaPorte",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClienteDestinoId",
                table: "TrasladosCartaPorte",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreRemitenteDestinatario",
                table: "UbicacionesCartaPorte");

            migrationBuilder.DropColumn(
                name: "ClienteDestinoId",
                table: "TrasladosCartaPorte");
        }
    }
}
