SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.FacturasDeCompras', N'U') IS NULL OR OBJECT_ID(N'dbo.CertificadosProveedor', N'U') IS NULL
        THROW 51000, N'Faltan las dos fases anteriores de facturas del proveedor.', 1;
    IF OBJECT_ID(N'dbo.IntentosFacturasProveedor', N'U') IS NOT NULL OR
       OBJECT_ID(N'dbo.SeriesFacturasProveedor', N'U') IS NOT NULL OR
       OBJECT_ID(N'dbo.ReservarFolioProveedor', N'P') IS NOT NULL OR
       COL_LENGTH(N'dbo.FacturasDeCompras', N'Folio') IS NOT NULL
        THROW 51000, N'La actualización ya existe o está incompleta. No se aplicaron cambios.', 1;
    ALTER TABLE dbo.FacturasDeCompras ADD Folio int NULL, Uuid uniqueidentifier NULL, FechaTimbradoUtc datetime2 NULL;
    CREATE TABLE dbo.SeriesFacturasProveedor (
        Rfc nvarchar(13) NOT NULL, Serie nvarchar(25) NOT NULL, UltimoFolio int NOT NULL,
        CONSTRAINT PK_SeriesFacturasProveedor PRIMARY KEY (Rfc, Serie)
    );
    CREATE TABLE dbo.IntentosFacturasProveedor (
        Id uniqueidentifier NOT NULL, CuentaId uniqueidentifier NOT NULL,
        FacturaId uniqueidentifier NOT NULL, CertificadoId uniqueidentifier NOT NULL,
        OperadorId uniqueidentifier NOT NULL, ClaveSolicitud nvarchar(128) NOT NULL,
        VersionSolicitada varbinary(8) NOT NULL, Control uniqueidentifier NOT NULL,
        Estado nvarchar(20) NOT NULL, Folio int NOT NULL,
        FechaEmisionUtc datetime2 NOT NULL, ActualizadaUtc datetime2 NOT NULL,
        XmlEnviado nvarchar(max) NULL, CadenaOriginal nvarchar(max) NULL, Sello nvarchar(max) NULL,
        NoCertificado nvarchar(40) NULL, CodigoError nvarchar(100) NULL, XmlTimbrado nvarchar(max) NULL,
        Uuid uniqueidentifier NULL, FechaTimbradoUtc datetime2 NULL, NoCertificadoSat nvarchar(40) NULL,
        SelloSat nvarchar(max) NULL, CadenaOriginalSat nvarchar(max) NULL,
        CONSTRAINT PK_IntentosFacturasProveedor PRIMARY KEY (Id),
        CONSTRAINT FK_IntentosFacturasProveedor_CertificadosProveedor_CertificadoId FOREIGN KEY (CertificadoId)
            REFERENCES dbo.CertificadosProveedor (Id),
        CONSTRAINT FK_IntentosFacturasProveedor_FacturasDeCompras_FacturaId FOREIGN KEY (FacturaId)
            REFERENCES dbo.FacturasDeCompras (Id)
    );
    CREATE INDEX IX_IntentosFacturasProveedor_CertificadoId ON dbo.IntentosFacturasProveedor (CertificadoId);
    CREATE INDEX IX_IntentosFacturasProveedor_Estado_ActualizadaUtc ON dbo.IntentosFacturasProveedor (Estado, ActualizadaUtc);
    CREATE UNIQUE INDEX IX_IntentosFacturasProveedor_FacturaId ON dbo.IntentosFacturasProveedor (FacturaId)
        WHERE Estado = N'timbrando';
    CREATE UNIQUE INDEX IX_IntentosFacturasProveedor_OperadorId_ClaveSolicitud ON dbo.IntentosFacturasProveedor (OperadorId, ClaveSolicitud);
    EXEC(N'CREATE PROCEDURE dbo.ReservarFolioProveedor
    @Rfc nvarchar(13), @Serie nvarchar(25)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.SeriesFacturasProveedor WITH (UPDLOCK, HOLDLOCK)
                       WHERE Rfc = @Rfc AND Serie = @Serie)
            INSERT dbo.SeriesFacturasProveedor (Rfc, Serie, UltimoFolio) VALUES (@Rfc, @Serie, 0);
        UPDATE dbo.SeriesFacturasProveedor WITH (UPDLOCK, ROWLOCK)
        SET UltimoFolio = UltimoFolio + 1
        OUTPUT inserted.UltimoFolio AS Value
        WHERE Rfc = @Rfc AND Serie = @Serie AND UltimoFolio < 2147483647;
        IF @@ROWCOUNT <> 1 THROW 51000, N''La serie del proveedor no tiene folios disponibles.'', 1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;');
    INSERT dbo.OperadoresPermisos (OperadorId, Permiso)
    SELECT o.Id, N'panel_timbrar_compras' FROM dbo.OperadoresPlataforma o
    WHERE o.EsPrincipal = 1 AND NOT EXISTS
        (SELECT 1 FROM dbo.OperadoresPermisos p WHERE p.OperadorId = o.Id AND p.Permiso = N'panel_timbrar_compras');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
