-- Ejecutar en la base del sistema después del respaldo.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.ConfiguracionFiscalProveedor', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.CertificadosProveedor', N'U') IS NOT NULL
        THROW 51000, N'Las tablas fiscales del proveedor ya existen. No se aplicaron cambios.', 1;

    CREATE TABLE dbo.ConfiguracionFiscalProveedor (
        Id tinyint NOT NULL,
        Rfc nvarchar(13) NOT NULL,
        NombreFiscal nvarchar(254) NOT NULL,
        RegimenFiscal nvarchar(3) NOT NULL,
        CodigoPostalExpedicion nvarchar(5) NOT NULL,
        ZonaHoraria nvarchar(64) NOT NULL,
        Serie nvarchar(25) NOT NULL,
        ActualizadaUtc datetime2 NOT NULL,
        ActualizadaPorOperadorId uniqueidentifier NOT NULL,
        Version rowversion NOT NULL,
        CONSTRAINT PK_ConfiguracionFiscalProveedor PRIMARY KEY (Id),
        CONSTRAINT CK_ConfiguracionFiscalProveedor_Unica CHECK (Id = 1)
    );
    CREATE TABLE dbo.CertificadosProveedor (
        Id uniqueidentifier NOT NULL,
        ConfiguracionId tinyint NOT NULL,
        Rfc nvarchar(13) NOT NULL,
        NumeroSerie nvarchar(40) NOT NULL,
        VigenciaDesdeUtc datetime2 NOT NULL,
        VigenciaHastaUtc datetime2 NOT NULL,
        CerCifrado varbinary(max) NOT NULL,
        KeyCifrada varbinary(max) NOT NULL,
        ContrasenaCifrada nvarchar(3000) NOT NULL,
        Activo bit NOT NULL,
        CargadoUtc datetime2 NOT NULL,
        CargadoPorOperadorId uniqueidentifier NOT NULL,
        CONSTRAINT PK_CertificadosProveedor PRIMARY KEY (Id),
        CONSTRAINT FK_CertificadosProveedor_ConfiguracionFiscalProveedor_ConfiguracionId
            FOREIGN KEY (ConfiguracionId) REFERENCES dbo.ConfiguracionFiscalProveedor (Id)
    );
    CREATE UNIQUE INDEX IX_CertificadosProveedor_ConfiguracionId
        ON dbo.CertificadosProveedor (ConfiguracionId) WHERE Activo = 1;
    CREATE UNIQUE INDEX IX_CertificadosProveedor_ConfiguracionId_NumeroSerie
        ON dbo.CertificadosProveedor (ConfiguracionId, NumeroSerie);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
