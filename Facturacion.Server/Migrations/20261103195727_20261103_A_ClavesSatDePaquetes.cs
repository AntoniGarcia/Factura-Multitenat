using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_ClavesSatDePaquetes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveProdServ",
                table: "Paquetes",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveUnidad",
                table: "Paquetes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveProdServAlComprar",
                table: "ComprasTimbres",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveUnidadAlComprar",
                table: "ComprasTimbres",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaveProdServ",
                table: "Paquetes");

            migrationBuilder.DropColumn(
                name: "ClaveUnidad",
                table: "Paquetes");

            migrationBuilder.DropColumn(
                name: "ClaveProdServAlComprar",
                table: "ComprasTimbres");

            migrationBuilder.DropColumn(
                name: "ClaveUnidadAlComprar",
                table: "ComprasTimbres");
        }
    }
}
