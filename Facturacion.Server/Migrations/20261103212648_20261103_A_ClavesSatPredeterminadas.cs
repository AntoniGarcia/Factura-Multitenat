using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_ClavesSatPredeterminadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClaveUnidad",
                table: "Paquetes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "E48",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaveProdServ",
                table: "Paquetes",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                defaultValue: "81112106",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.Sql("""
DECLARE @Cambios TABLE (
    Id uniqueidentifier, CuentaId uniqueidentifier,
    ProductoAnterior nvarchar(8), UnidadAnterior nvarchar(3),
    ProductoNuevo nvarchar(8), UnidadNueva nvarchar(3));
UPDATE dbo.Paquetes
SET ClaveProdServ = CASE WHEN NULLIF(LTRIM(RTRIM(ClaveProdServ)), N'') IS NULL THEN N'81112106' ELSE ClaveProdServ END,
    ClaveUnidad = CASE WHEN NULLIF(LTRIM(RTRIM(ClaveUnidad)), N'') IS NULL THEN N'E48' ELSE ClaveUnidad END
OUTPUT inserted.Id, inserted.CuentaId, deleted.ClaveProdServ, deleted.ClaveUnidad,
    inserted.ClaveProdServ, inserted.ClaveUnidad INTO @Cambios
WHERE NULLIF(LTRIM(RTRIM(ClaveProdServ)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(ClaveUnidad)), N'') IS NULL;
INSERT dbo.Bitacora (CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
SELECT CuentaId, N'Paquete', LOWER(CONVERT(nvarchar(36), Id)), N'paquete_claves_predeterminadas',
    N'{"ClaveProdServ":' + CASE WHEN ProductoAnterior IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ProductoAnterior, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N',"ClaveUnidad":' + CASE WHEN UnidadAnterior IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UnidadAnterior, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N'}' ,
    N'{"ClaveProdServ":' + CASE WHEN ProductoNuevo IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ProductoNuevo, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N',"ClaveUnidad":' + CASE WHEN UnidadNueva IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UnidadNueva, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N'}' , SYSUTCDATETIME()
FROM @Cambios;
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClaveUnidad",
                table: "Paquetes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true,
                oldDefaultValue: "E48");

            migrationBuilder.AlterColumn<string>(
                name: "ClaveProdServ",
                table: "Paquetes",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true,
                oldDefaultValue: "81112106");

        }
    }
}
