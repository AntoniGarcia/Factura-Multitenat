using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_PaquetesCorregidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000001"),
                column: "Orden",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000002"),
                column: "Orden",
                value: 5);

            migrationBuilder.InsertData(
                table: "Paquetes",
                columns: new[] { "Id", "Activo", "CantidadTimbres", "Nombre", "Orden", "PrecioPorTimbre", "PrecioTotal", "VigenciaMeses" },
                values: new object[,]
                {
                    { new Guid("9c1f0a10-0000-4000-8000-000000000006"), true, 50, "50 timbres", 1, 3.50m, 175.00m, 12 },
                    { new Guid("9c1f0a10-0000-4000-8000-000000000007"), true, 200, "200 timbres", 2, 2.50m, 500.00m, 12 },
                    { new Guid("9c1f0a10-0000-4000-8000-000000000008"), true, 800, "800 timbres", 4, 1.90m, 1520.00m, 12 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000008"));

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000001"),
                column: "Orden",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000002"),
                column: "Orden",
                value: 2);
        }
    }
}
