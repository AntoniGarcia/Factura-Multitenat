using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <summary>
    /// Los timbres pasan de la empresa a la cuenta (AGENTS.md §11, 28 de octubre de 2026): una
    /// bolsa por cuenta con la suma de las de sus empresas; compras y movimientos son de la
    /// cuenta y conservan la empresa que los originó; los paquetes exclusivos son de la cuenta.
    /// </summary>
    public partial class _20261028_A_TimbresPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_BolsasTimbres_Empresas_EmpresaId", table: "BolsasTimbres");
            migrationBuilder.DropForeignKey(name: "FK_Paquetes_Empresas_EmpresaId", table: "Paquetes");

            // El índice único se quita antes de unir las bolsas: mientras dura la conversión,
            // una cuenta con dos empresas tiene dos renglones con el mismo CuentaId.
            migrationBuilder.DropIndex(name: "IX_BolsasTimbres_EmpresaId", table: "BolsasTimbres");
            migrationBuilder.DropIndex(name: "IX_Paquetes_EmpresaId_Activo_Orden", table: "Paquetes");
            migrationBuilder.DropIndex(name: "IX_MovimientosTimbre_EmpresaId_MomentoUtc", table: "MovimientosTimbre");
            migrationBuilder.DropIndex(name: "IX_ComprasTimbres_EmpresaId_CreadaUtc", table: "ComprasTimbres");

            migrationBuilder.RenameColumn(name: "EmpresaId", table: "BolsasTimbres", newName: "CuentaId");
            migrationBuilder.RenameColumn(name: "EmpresaId", table: "Paquetes", newName: "CuentaId");

            migrationBuilder.AddColumn<Guid>(
                name: "CuentaId",
                table: "MovimientosTimbre",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CuentaId",
                table: "ComprasTimbres",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Las columnas renombradas todavía guardan la empresa: se traducen a su cuenta. La
            // bolsa que queda por cuenta es la más antigua y recibe la suma de todas, así que
            // la suma de los movimientos de la cuenta sigue cuadrando con ella.
            migrationBuilder.Sql("""
                UPDATE p SET p.CuentaId = e.CuentaId
                FROM dbo.Paquetes AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;

                UPDATE m SET m.CuentaId = e.CuentaId
                FROM dbo.MovimientosTimbre AS m JOIN dbo.Empresas AS e ON e.Id = m.EmpresaId;

                UPDATE c SET c.CuentaId = e.CuentaId
                FROM dbo.ComprasTimbres AS c JOIN dbo.Empresas AS e ON e.Id = c.EmpresaId;

                UPDATE b SET b.CuentaId = e.CuentaId
                FROM dbo.BolsasTimbres AS b JOIN dbo.Empresas AS e ON e.Id = b.CuentaId;

                -- La bolsa que se conserva se elige antes de tocar nada: si se eligiera después
                -- de actualizar su fecha, podría empatar con otra y borrarse la equivocada.
                SELECT b.CuentaId,
                       SUM(b.Disponibles) AS Disponibles,
                       SUM(b.Reservados) AS Reservados,
                       MAX(b.ActualizadaUtc) AS ActualizadaUtc,
                       (SELECT TOP 1 x.Id FROM dbo.BolsasTimbres AS x
                         WHERE x.CuentaId = b.CuentaId ORDER BY x.ActualizadaUtc, x.Id) AS Conservar
                INTO #BolsasPorCuenta
                FROM dbo.BolsasTimbres AS b
                GROUP BY b.CuentaId;

                DELETE b FROM dbo.BolsasTimbres AS b
                JOIN #BolsasPorCuenta AS t ON t.CuentaId = b.CuentaId
                WHERE b.Id <> t.Conservar;

                UPDATE b
                   SET b.Disponibles    = t.Disponibles,
                       b.Reservados     = t.Reservados,
                       b.ActualizadaUtc = t.ActualizadaUtc
                FROM dbo.BolsasTimbres AS b
                JOIN #BolsasPorCuenta AS t ON t.Conservar = b.Id;

                DROP TABLE #BolsasPorCuenta;
                """);

            migrationBuilder.CreateIndex(name: "IX_BolsasTimbres_CuentaId", table: "BolsasTimbres", column: "CuentaId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Paquetes_CuentaId_Activo_Orden", table: "Paquetes", columns: new[] { "CuentaId", "Activo", "Orden" });
            migrationBuilder.CreateIndex(name: "IX_MovimientosTimbre_CuentaId_MomentoUtc", table: "MovimientosTimbre", columns: new[] { "CuentaId", "MomentoUtc" });
            migrationBuilder.CreateIndex(name: "IX_ComprasTimbres_CuentaId_CreadaUtc", table: "ComprasTimbres", columns: new[] { "CuentaId", "CreadaUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_BolsasTimbres_Cuentas_CuentaId", table: "BolsasTimbres", column: "CuentaId",
                principalTable: "Cuentas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Paquetes_Cuentas_CuentaId", table: "Paquetes", column: "CuentaId",
                principalTable: "Cuentas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            CrearProcedimientosPorCuenta(migrationBuilder);
        }

        /// <summary>
        /// Vuelve a una bolsa por empresa calculada con la suma de los movimientos de cada
        /// empresa, que es exacta mientras ninguna haya gastado timbres comprados por otra. Si
        /// ya lo hizo, la que gastó queda con saldo negativo y la que compró con el excedente:
        /// el total de la cuenta se conserva. Los paquetes exclusivos vuelven a la empresa más
        /// antigua de su cuenta.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_BolsasTimbres_Cuentas_CuentaId", table: "BolsasTimbres");
            migrationBuilder.DropForeignKey(name: "FK_Paquetes_Cuentas_CuentaId", table: "Paquetes");

            migrationBuilder.DropIndex(name: "IX_BolsasTimbres_CuentaId", table: "BolsasTimbres");
            migrationBuilder.DropIndex(name: "IX_Paquetes_CuentaId_Activo_Orden", table: "Paquetes");
            migrationBuilder.DropIndex(name: "IX_MovimientosTimbre_CuentaId_MomentoUtc", table: "MovimientosTimbre");
            migrationBuilder.DropIndex(name: "IX_ComprasTimbres_CuentaId_CreadaUtc", table: "ComprasTimbres");

            migrationBuilder.RenameColumn(name: "CuentaId", table: "BolsasTimbres", newName: "EmpresaId");
            migrationBuilder.RenameColumn(name: "CuentaId", table: "Paquetes", newName: "EmpresaId");

            migrationBuilder.Sql("""
                UPDATE p SET p.EmpresaId = (
                    SELECT TOP 1 e.Id FROM dbo.Empresas AS e
                    WHERE e.CuentaId = p.EmpresaId ORDER BY e.FechaAltaUtc, e.Id)
                FROM dbo.Paquetes AS p
                WHERE p.EmpresaId IS NOT NULL;

                DELETE FROM dbo.BolsasTimbres;

                INSERT INTO dbo.BolsasTimbres (Id, EmpresaId, Disponibles, Reservados, ActualizadaUtc)
                SELECT NEWID(), m.EmpresaId, SUM(m.DeltaDisponible), SUM(m.DeltaReservado), MAX(m.MomentoUtc)
                FROM dbo.MovimientosTimbre AS m
                GROUP BY m.EmpresaId;
                """);

            migrationBuilder.DropColumn(name: "CuentaId", table: "MovimientosTimbre");
            migrationBuilder.DropColumn(name: "CuentaId", table: "ComprasTimbres");

            migrationBuilder.CreateIndex(name: "IX_BolsasTimbres_EmpresaId", table: "BolsasTimbres", column: "EmpresaId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Paquetes_EmpresaId_Activo_Orden", table: "Paquetes", columns: new[] { "EmpresaId", "Activo", "Orden" });
            migrationBuilder.CreateIndex(name: "IX_MovimientosTimbre_EmpresaId_MomentoUtc", table: "MovimientosTimbre", columns: new[] { "EmpresaId", "MomentoUtc" });
            migrationBuilder.CreateIndex(name: "IX_ComprasTimbres_EmpresaId_CreadaUtc", table: "ComprasTimbres", columns: new[] { "EmpresaId", "CreadaUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_BolsasTimbres_Empresas_EmpresaId", table: "BolsasTimbres", column: "EmpresaId",
                principalTable: "Empresas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Paquetes_Empresas_EmpresaId", table: "Paquetes", column: "EmpresaId",
                principalTable: "Empresas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            CrearProcedimientosPorEmpresa(migrationBuilder);
        }

        // Mismos parámetros que antes: quien llama sigue pasando la empresa, y el procedimiento
        // encuentra la bolsa por la cuenta de esa empresa. La empresa queda en el movimiento.
        // Se borran y se crean en vez de CREATE OR ALTER, que no existe antes de SQL Server 2016 SP1.
        private static void CrearProcedimientosPorCuenta(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ReservarTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;
                GO
                CREATE PROCEDURE dbo.ReservarTimbre
                    @EmpresaId     uniqueidentifier,
                    @ReservaId     uniqueidentifier,
                    @ComprobanteId uniqueidentifier,
                    @MomentoUtc    datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;
                    DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

                    BEGIN TRANSACTION;

                    -- La condición de saldo va en el WHERE: comprobar y descontar es una sola
                    -- operación atómica, aunque dos empresas de la cuenta timbren a la vez.
                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles - 1,
                           b.Reservados     = b.Reservados + 1,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles - 1,
                           @Reservados      = b.Reservados + 1
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.CuentaId = @CuentaId
                       AND b.Disponibles >= 1;

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
                        (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
                    VALUES
                        (NEWID(), @CuentaId, @EmpresaId, 'reserva', -1, 1,
                         @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Reservado, @Disponibles AS DisponiblesDespues;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ConfirmarTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;
                GO
                CREATE PROCEDURE dbo.ConfirmarTimbre
                    @EmpresaId  uniqueidentifier,
                    @ReservaId  uniqueidentifier,
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;
                    DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

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
                     WHERE b.CuentaId = @CuentaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
                    VALUES
                        (NEWID(), @CuentaId, @EmpresaId, 'consumo', 0, -1,
                         @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Resuelto;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.DevolverTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;
                GO
                CREATE PROCEDURE dbo.DevolverTimbre
                    @EmpresaId  uniqueidentifier,
                    @ReservaId  uniqueidentifier,
                    @Motivo     nvarchar(300),
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Disponibles int, @Reservados int;
                    DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

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
                     WHERE b.CuentaId = @CuentaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, ReservaId, Motivo, MomentoUtc)
                    VALUES
                        (NEWID(), @CuentaId, @EmpresaId, 'devolucion', 1, -1,
                         @Disponibles, @Reservados, @ReservaId, @Motivo, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Resuelto;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.AcreditarCompra', N'P') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;
                GO
                CREATE PROCEDURE dbo.AcreditarCompra
                    @CompraId   uniqueidentifier,
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @CuentaId uniqueidentifier, @EmpresaId uniqueidentifier, @Cantidad int,
                            @Disponibles int, @Reservados int;

                    BEGIN TRANSACTION;

                    UPDATE c WITH (UPDLOCK, ROWLOCK)
                       SET c.Estado        = 'pagada',
                           c.AcreditadaUtc = @MomentoUtc,
                           c.VenceUtc      = DATEADD(month, c.VigenciaMeses, @MomentoUtc),
                           @CuentaId       = c.CuentaId,
                           @EmpresaId      = c.EmpresaId,
                           @Cantidad       = c.CantidadTimbres
                      FROM dbo.ComprasTimbres AS c
                     WHERE c.Id = @CompraId
                       AND c.Estado = 'pendiente_de_pago';

                    IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS Acreditada, NULL AS DisponiblesDespues;
                        RETURN;
                    END

                    -- UPDLOCK + HOLDLOCK sobre una llave que todavía no existe toma un candado de
                    -- rango: dos acreditaciones simultáneas de la primera compra de una cuenta no
                    -- insertan dos bolsas.
                    IF NOT EXISTS (SELECT 1 FROM dbo.BolsasTimbres WITH (UPDLOCK, HOLDLOCK)
                                    WHERE CuentaId = @CuentaId)
                        INSERT INTO dbo.BolsasTimbres
                            (Id, CuentaId, Disponibles, Reservados, ActualizadaUtc)
                        VALUES
                            (NEWID(), @CuentaId, 0, 0, @MomentoUtc);

                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles + @Cantidad,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles + @Cantidad,
                           @Reservados      = b.Reservados
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.CuentaId = @CuentaId;

                    INSERT INTO dbo.MovimientosTimbre
                        (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
                         DisponiblesDespues, ReservadosDespues, CompraId, MomentoUtc)
                    VALUES
                        (NEWID(), @CuentaId, @EmpresaId, 'compra', @Cantidad, 0,
                         @Disponibles, @Reservados, @CompraId, @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT CAST(1 AS bit) AS Acreditada, @Disponibles AS DisponiblesDespues;
                END
                """);
        }

        // Las versiones de 20260815134713_A_Timbres, para volver a la bolsa por empresa.
        private static void CrearProcedimientosPorEmpresa(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ReservarTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;
                GO
                CREATE PROCEDURE dbo.ReservarTimbre
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

                    UPDATE b WITH (UPDLOCK, ROWLOCK)
                       SET b.Disponibles    = b.Disponibles - 1,
                           b.Reservados     = b.Reservados + 1,
                           b.ActualizadaUtc = @MomentoUtc,
                           @Disponibles     = b.Disponibles - 1,
                           @Reservados      = b.Reservados + 1
                      FROM dbo.BolsasTimbres AS b
                     WHERE b.EmpresaId = @EmpresaId
                       AND b.Disponibles >= 1;

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ConfirmarTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;
                GO
                CREATE PROCEDURE dbo.ConfirmarTimbre
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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.DevolverTimbre', N'P') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;
                GO
                CREATE PROCEDURE dbo.DevolverTimbre
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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.AcreditarCompra', N'P') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;
                GO
                CREATE PROCEDURE dbo.AcreditarCompra
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
    }
}
