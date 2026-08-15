using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_PaquetesSembrados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Paquetes",
                columns: new[] { "Id", "Activo", "CantidadTimbres", "Nombre", "Orden", "PrecioPorTimbre", "PrecioTotal", "VigenciaMeses" },
                values: new object[,]
                {
                    { new Guid("9c1f0a10-0000-4000-8000-000000000001"), true, 500, "500 timbres", 1, 2.00m, 1000.00m, 12 },
                    { new Guid("9c1f0a10-0000-4000-8000-000000000002"), true, 1000, "1000 timbres", 2, 1.80m, 1800.00m, 12 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000002"));
        }
    }
}
