using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260926_B_ConsultaSatEnSolicitud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultadaUtc",
                table: "SolicitudesCancelacion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsCancelableSat",
                table: "SolicitudesCancelacion",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoCfdiSat",
                table: "SolicitudesCancelacion",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstatusCancelacionSat",
                table: "SolicitudesCancelacion",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsultadaUtc",
                table: "SolicitudesCancelacion");

            migrationBuilder.DropColumn(
                name: "EsCancelableSat",
                table: "SolicitudesCancelacion");

            migrationBuilder.DropColumn(
                name: "EstadoCfdiSat",
                table: "SolicitudesCancelacion");

            migrationBuilder.DropColumn(
                name: "EstatusCancelacionSat",
                table: "SolicitudesCancelacion");
        }
    }
}
