using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_CatalogosSat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoVersion",
                columns: table => new
                {
                    Catalogo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VersionCatalogo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RevisionCatalogo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FechaPublicacion = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCargaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RenglonesVigentes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoVersion", x => x.Catalogo);
                });

            migrationBuilder.CreateTable(
                name: "SatClaveProdServ",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IncluirIvaTrasladado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IncluirIepsTrasladado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ComplementoQueDebeIncluir = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EstimuloFranjaFronteriza = table.Column<bool>(type: "bit", nullable: false),
                    PalabrasSimilares = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatClaveProdServ", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatClaveUnidad",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Simbolo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatClaveUnidad", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatCodigoPostal",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ClaveEstado = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClaveMunicipio = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ClaveLocalidad = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    EstimuloFranjaFronteriza = table.Column<bool>(type: "bit", nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatCodigoPostal", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatColonia",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ClaveCodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IdInterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatColonia", x => new { x.Clave, x.ClaveCodigoPostal });
                });

            migrationBuilder.CreateTable(
                name: "SatEstado",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClavePais = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatEstado", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatExportacion",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatExportacion", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatFormaPago",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatFormaPago", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatImpuesto",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Retencion = table.Column<bool>(type: "bit", nullable: false),
                    Traslado = table.Column<bool>(type: "bit", nullable: false),
                    LocalOFederal = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatImpuesto", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatMes",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatMes", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatMetodoPago",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatMetodoPago", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatMoneda",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Decimales = table.Column<int>(type: "int", nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatMoneda", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatMunicipio",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClaveEstado = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IdInterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatMunicipio", x => new { x.Clave, x.ClaveEstado });
                });

            migrationBuilder.CreateTable(
                name: "SatObjetoImp",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatObjetoImp", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatPais",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatPais", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatPeriodicidad",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatPeriodicidad", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatRegimenFiscal",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    AplicaFisica = table.Column<bool>(type: "bit", nullable: false),
                    AplicaMoral = table.Column<bool>(type: "bit", nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatRegimenFiscal", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTasaOCuota",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RangoOFijo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ValorMinimo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ValorMaximo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Impuesto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Factor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Traslado = table.Column<bool>(type: "bit", nullable: false),
                    Retencion = table.Column<bool>(type: "bit", nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatTasaOCuota", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTipoDeComprobante",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ValorMaximo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatTipoDeComprobante", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTipoFactor",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatTipoFactor", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatTipoRelacion",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatTipoRelacion", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "SatUsoCfdi",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    AplicaFisica = table.Column<bool>(type: "bit", nullable: false),
                    AplicaMoral = table.Column<bool>(type: "bit", nullable: false),
                    RegimenesFiscalesAplicables = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FechaInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatUsoCfdi", x => x.Clave);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatColonia_ClaveCodigoPostal",
                table: "SatColonia",
                column: "ClaveCodigoPostal");

            migrationBuilder.CreateIndex(
                name: "IX_SatColonia_IdInterno",
                table: "SatColonia",
                column: "IdInterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatMunicipio_IdInterno",
                table: "SatMunicipio",
                column: "IdInterno",
                unique: true);

            // Fluent API no tiene forma de expresar FULLTEXT INDEX de SQL Server (ver
            // SatClaveProdServConfiguracion). ARQUITECTURA.md §7 pide texto completo sobre la
            // descripción de c_ClaveProdServ y sobre colonia y municipio de c_CodigoPostal;
            // 3082 es el único idioma español registrado en esta instancia
            // (sys.fulltext_languages), verificado antes de escribir esto. SatColonia y
            // SatMunicipio usan IX_..._IdInterno como llave del índice: SQL Server exige que
            // sea de una sola columna, y su llave real es compuesta.
            //
            // suppressTransaction: SQL Server no permite CREATE FULLTEXT CATALOG/INDEX
            // dentro de una transacción, y EF Core envuelve toda la migración en una por
            // omisión. Se comprobó aplicando la migración de verdad: sin esto, falla con
            // "CREATE FULLTEXT CATALOG statement cannot be used inside a user transaction".
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'CatalogoTextoCompleto')
                    CREATE FULLTEXT CATALOG CatalogoTextoCompleto AS DEFAULT;

                CREATE FULLTEXT INDEX ON SatClaveProdServ(Descripcion LANGUAGE 3082, PalabrasSimilares LANGUAGE 3082)
                    KEY INDEX PK_SatClaveProdServ ON CatalogoTextoCompleto
                    WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;

                CREATE FULLTEXT INDEX ON SatColonia(Nombre LANGUAGE 3082)
                    KEY INDEX IX_SatColonia_IdInterno ON CatalogoTextoCompleto
                    WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;

                CREATE FULLTEXT INDEX ON SatMunicipio(Descripcion LANGUAGE 3082)
                    KEY INDEX IX_SatMunicipio_IdInterno ON CatalogoTextoCompleto
                    WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los índices de texto completo se van solos al borrar la tabla, pero el
            // catálogo que los contiene no: si queda huérfano, la siguiente vez que se
            // aplique esta migración el CREATE FULLTEXT CATALOG de arriba lo encuentra y no
            // hace nada (el IF NOT EXISTS ya lo cubre), así que no hace falta borrarlo aquí.

            migrationBuilder.DropTable(
                name: "CatalogoVersion");

            migrationBuilder.DropTable(
                name: "SatClaveProdServ");

            migrationBuilder.DropTable(
                name: "SatClaveUnidad");

            migrationBuilder.DropTable(
                name: "SatCodigoPostal");

            migrationBuilder.DropTable(
                name: "SatColonia");

            migrationBuilder.DropTable(
                name: "SatEstado");

            migrationBuilder.DropTable(
                name: "SatExportacion");

            migrationBuilder.DropTable(
                name: "SatFormaPago");

            migrationBuilder.DropTable(
                name: "SatImpuesto");

            migrationBuilder.DropTable(
                name: "SatMes");

            migrationBuilder.DropTable(
                name: "SatMetodoPago");

            migrationBuilder.DropTable(
                name: "SatMoneda");

            migrationBuilder.DropTable(
                name: "SatMunicipio");

            migrationBuilder.DropTable(
                name: "SatObjetoImp");

            migrationBuilder.DropTable(
                name: "SatPais");

            migrationBuilder.DropTable(
                name: "SatPeriodicidad");

            migrationBuilder.DropTable(
                name: "SatRegimenFiscal");

            migrationBuilder.DropTable(
                name: "SatTasaOCuota");

            migrationBuilder.DropTable(
                name: "SatTipoDeComprobante");

            migrationBuilder.DropTable(
                name: "SatTipoFactor");

            migrationBuilder.DropTable(
                name: "SatTipoRelacion");

            migrationBuilder.DropTable(
                name: "SatUsoCfdi");
        }
    }
}
