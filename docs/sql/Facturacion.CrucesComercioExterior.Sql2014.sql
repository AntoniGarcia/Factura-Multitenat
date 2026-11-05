-- Actualiza solamente los metadatos SAT de esta fase sobre una base ya existente.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SatPais', N'U') IS NULL OR OBJECT_ID(N'dbo.SatFraccionArancelaria', N'U') IS NULL
    THROW 50000, N'La base seleccionada no tiene los catalogos requeridos.', 1;

IF COL_LENGTH(N'dbo.SatPais', N'Agrupaciones') IS NULL
    ALTER TABLE dbo.SatPais ADD Agrupaciones nvarchar(500) NULL;
IF COL_LENGTH(N'dbo.SatPais', N'FormatoCodigoPostal') IS NULL
    ALTER TABLE dbo.SatPais ADD FormatoCodigoPostal nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.SatPais', N'FormatoRegistroTributario') IS NULL
    ALTER TABLE dbo.SatPais ADD FormatoRegistroTributario nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.SatPais', N'ValidacionRegistroTributario') IS NULL
    ALTER TABLE dbo.SatPais ADD ValidacionRegistroTributario nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.SatFraccionArancelaria', N'UnidadMedida') IS NULL
    ALTER TABLE dbo.SatFraccionArancelaria ADD UnidadMedida nvarchar(2) NULL;

-- No inventa el historial de una base creada manualmente.
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261103231509_20261103_B_IshEnFactura'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104193407_20261104_A_CrucesFiscalesComercioExterior'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261104193407_20261104_A_CrucesFiscalesComercioExterior'', N''10.0.14'');';

COMMIT;
