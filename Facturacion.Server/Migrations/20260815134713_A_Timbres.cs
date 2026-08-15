using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_Timbres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BolsasTimbres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Disponibles = table.Column<int>(type: "int", nullable: false),
                    Reservados = table.Column<int>(type: "int", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BolsasTimbres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BolsasTimbres_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprasTimbres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaqueteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombrePaquete = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CantidadTimbres = table.Column<int>(type: "int", nullable: false),
                    PrecioPorTimbre = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    VigenciaMeses = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcreditadaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VenceUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasTimbres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Membresias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CuentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Membresias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Membresias_Cuentas_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosTimbre",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeltaDisponible = table.Column<int>(type: "int", nullable: false),
                    DeltaReservado = table.Column<int>(type: "int", nullable: false),
                    DisponiblesDespues = table.Column<int>(type: "int", nullable: false),
                    ReservadosDespues = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MomentoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosTimbre", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Paquetes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CantidadTimbres = table.Column<int>(type: "int", nullable: false),
                    PrecioPorTimbre = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    VigenciaMeses = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paquetes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReservasTimbre",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoResolucion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasTimbre", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BolsasTimbres_EmpresaId",
                table: "BolsasTimbres",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprasTimbres_EmpresaId_CreadaUtc",
                table: "ComprasTimbres",
                columns: new[] { "EmpresaId", "CreadaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasTimbres_Estado",
                table: "ComprasTimbres",
                column: "Estado",
                filter: "[Estado] = 'pendiente_de_pago'");

            migrationBuilder.CreateIndex(
                name: "IX_Membresias_CuentaId_FinUtc",
                table: "Membresias",
                columns: new[] { "CuentaId", "FinUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosTimbre_EmpresaId_MomentoUtc",
                table: "MovimientosTimbre",
                columns: new[] { "EmpresaId", "MomentoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Paquetes_Activo_Orden",
                table: "Paquetes",
                columns: new[] { "Activo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasTimbre_CreadaUtc",
                table: "ReservasTimbre",
                column: "CreadaUtc",
                filter: "[Estado] = 'reservado'");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasTimbre_EmpresaId_ComprobanteId",
                table: "ReservasTimbre",
                columns: new[] { "EmpresaId", "ComprobanteId" });

            CrearProcedimientos(migrationBuilder);
        }

        /// <summary>
        /// Las cuatro operaciones que mueven el saldo viven en SQL y no en C# por la misma
        /// razón que la reserva de folios: leer el saldo, decidir y guardarlo desde la
        /// aplicación deja una ventana en la que dos peticiones leen el mismo número y las
        /// dos creen que había timbre. Aquí la lectura, la decisión y el descuento son un
        /// solo <c>UPDATE ... WITH (UPDLOCK)</c> dentro de una transacción corta.
        /// <para>
        /// Las cuatro exigen <c>@EmpresaId</c> en su <c>WHERE</c>: la empresa viene del claim
        /// del token, y pedir timbres de otra empresa simplemente no encuentra renglón.
        /// </para>
        /// </summary>
        private static void CrearProcedimientos(MigrationBuilder migrationBuilder)
        {
            // ── Reservar ────────────────────────────────────────────────────────────────
            // Devuelve una fila con Reservado = 0 cuando no alcanza el saldo. No lanza: que
            // un cliente se quede sin timbres es un hecho de negocio previsto, no una falla
            // del sistema, y el contrato pide un error de negocio claro.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.ReservarTimbre
                    @EmpresaId     uniqueidentifier,
                    @ReservaId     uniqueidentifier,
                    @ComprobanteId uniqueidentifier,
                    @MomentoUtc    datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;

                    BEGIN TRANSACTION;

                    -- La condición de saldo va en el WHERE, no en un IF previo: así la
                    -- comprobación y el descuento son la misma operación atómica y no hay
                    -- ventana entre una y otro.
                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles - 1,
                           b.Reservados     = b.Reservados + 1,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles - 1,
                           @Reservados      = b.Reservados + 1
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.EmpresaId = @EmpresaId
                       AND b.Disponibles >= 1;

                    -- Nulo tanto si la empresa no tiene bolsa como si tiene cero timbres.
                    -- Para el que llama significan lo mismo: no hay timbre que apartar.
                    IF @Disponibles IS NULL
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS Reservado, 0 AS DisponiblesDespues;
                        RETURN;
                    END

                    INSERT INTO dbo.ReservasTimbre
                        (Id, EmpresaId, ComprobanteId, Estado, CreadaUtc)
                    VALUES
                        (@ReservaId, @EmpresaId, @ComprobanteId, 'reservado', @MomentoUtc);

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
                    VALUES
                        (NEWID(), @EmpresaId, 'reserva', -1, 1,
                         @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Reservado, @Disponibles AS DisponiblesDespues;
                END
                """);

            // ── Confirmar ───────────────────────────────────────────────────────────────
            // El estado esperado va en el WHERE del UPDATE de la reserva. Ese es el candado
            // que impide confirmar dos veces la misma reserva y gastar dos timbres por un
            // solo comprobante: la segunda llamada no encuentra renglón que actualizar.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.ConfirmarTimbre
                    @EmpresaId  uniqueidentifier,
                    @ReservaId  uniqueidentifier,
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;

                    BEGIN TRANSACTION;

                    UPDATE r WITH (UPDLOCK, ROWLOCK)
                       SET r.Estado      = 'confirmado',
                           r.ResueltaUtc = @MomentoUtc
                      FROM dbo.ReservasTimbre AS r
                     WHERE r.Id = @ReservaId
                       AND r.EmpresaId = @EmpresaId
                       AND r.Estado = 'reservado';

                    IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS Resuelto;
                        RETURN;
                    END

                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Reservados     = b.Reservados - 1,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles,
                           @Reservados      = b.Reservados - 1
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.EmpresaId = @EmpresaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
                    VALUES
                        (NEWID(), @EmpresaId, 'consumo', 0, -1,
                         @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Resuelto;
                END
                """);

            // ── Devolver ────────────────────────────────────────────────────────────────
            // A diferencia del folio, el timbre sí vuelve: es dinero pagado y el PAC no cobró
            // nada por un comprobante que no se timbró.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.DevolverTimbre
                    @EmpresaId  uniqueidentifier,
                    @ReservaId  uniqueidentifier,
                    @Motivo     nvarchar(300),
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;

                    BEGIN TRANSACTION;

                    UPDATE r WITH (UPDLOCK, ROWLOCK)
                       SET r.Estado           = 'devuelto',
                           r.ResueltaUtc      = @MomentoUtc,
                           r.MotivoResolucion = @Motivo
                      FROM dbo.ReservasTimbre AS r
                     WHERE r.Id = @ReservaId
                       AND r.EmpresaId = @EmpresaId
                       AND r.Estado = 'reservado';

                    IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS Resuelto;
                        RETURN;
                    END

                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles + 1,
                           b.Reservados     = b.Reservados - 1,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles + 1,
                           @Reservados      = b.Reservados - 1
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.EmpresaId = @EmpresaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, Motivo, MomentoUtc)
                    VALUES
                        (NEWID(), @EmpresaId, 'devolucion', 1, -1,
                         @Disponibles, @Reservados, @ReservaId, @Motivo, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Resuelto;
                END
                """);

            // ── Acreditar una compra ────────────────────────────────────────────────────
            // Único punto donde se crea saldo. El estado esperado va en el WHERE por la misma
            // razón que en confirmar: acreditar dos veces la misma compra regalaría un
            // paquete entero de timbres.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.AcreditarCompra
                    @CompraId   uniqueidentifier,
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @EmpresaId uniqueidentifier, @Cantidad int, @Vigencia int,
                            @Disponibles int, @Reservados int;

                    BEGIN TRANSACTION;

                    UPDATE c WITH (UPDLOCK, ROWLOCK)
                       SET c.Estado        = 'pagada',
                           c.AcreditadaUtc = @MomentoUtc,
                           c.VenceUtc      = DATEADD(month, c.VigenciaMeses, @MomentoUtc),
                           @EmpresaId      = c.EmpresaId,
                           @Cantidad       = c.CantidadTimbres,
                           @Vigencia       = c.VigenciaMeses
                      FROM dbo.ComprasTimbres AS c
                     WHERE c.Id = @CompraId
                       AND c.Estado = 'pendiente_de_pago';

                    IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS Acreditada, NULL AS DisponiblesDespues;
                        RETURN;
                    END

                    -- UPDLOCK + HOLDLOCK sobre una llave que todavía no existe toma un
                    -- candado de rango: sin él, dos acreditaciones simultáneas de la primera
                    -- compra de una empresa insertarían dos bolsas y el saldo se partiría.
                    IF NOT EXISTS (SELECT 1 FROM dbo.BolsasTimbres WITH (UPDLOCK, HOLDLOCK)
                                    WHERE EmpresaId = @EmpresaId)
                        INSERT INTO dbo.BolsasTimbres
                            (Id, EmpresaId, Disponibles, Reservados, ActualizadaUtc)
                        VALUES
                            (NEWID(), @EmpresaId, 0, 0, @MomentoUtc);

                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles + @Cantidad,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles + @Cantidad,
                           @Reservados      = b.Reservados
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.EmpresaId = @EmpresaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, CompraId, MomentoUtc)
                    VALUES
                        (NEWID(), @EmpresaId, 'compra', @Cantidad, 0,
                         @Disponibles, @Reservados, @CompraId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Acreditada, @Disponibles AS DisponiblesDespues;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ReservarTimbre;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ConfirmarTimbre;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.DevolverTimbre;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.AcreditarCompra;");

            migrationBuilder.DropTable(
                name: "BolsasTimbres");

            migrationBuilder.DropTable(
                name: "ComprasTimbres");

            migrationBuilder.DropTable(
                name: "Membresias");

            migrationBuilder.DropTable(
                name: "MovimientosTimbre");

            migrationBuilder.DropTable(
                name: "Paquetes");

            migrationBuilder.DropTable(
                name: "ReservasTimbre");
        }
    }
}
