using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_RechazoDeCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaUtc",
                table: "ComprasTimbres",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelacion",
                table: "ComprasTimbres",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanceladaUtc",
                table: "ComprasTimbres");

            migrationBuilder.DropColumn(
                name: "MotivoCancelacion",
                table: "ComprasTimbres");
        }
    }
}
