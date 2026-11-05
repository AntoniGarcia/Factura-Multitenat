-- Actualiza los indicadores SAT y la declaración capturada de cada mercancía.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.SatConfiguracionAutotransporte', N'U') IS NULL
   OR OBJECT_ID(N'dbo.SatClaveProdServCartaPorte', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MercanciasCartaPorte', N'U') IS NULL
    THROW 50000, N'La base seleccionada no tiene las tablas de Carta Porte.', 1;
IF COL_LENGTH(N'dbo.SatConfiguracionAutotransporte', N'Remolque') IS NULL
    ALTER TABLE dbo.SatConfiguracionAutotransporte ADD Remolque nvarchar(3) NULL;
IF COL_LENGTH(N'dbo.SatClaveProdServCartaPorte', N'MaterialPeligroso') IS NULL
    ALTER TABLE dbo.SatClaveProdServCartaPorte ADD MaterialPeligroso nvarchar(3) NULL;
IF COL_LENGTH(N'dbo.MercanciasCartaPorte', N'MaterialPeligroso') IS NULL
    ALTER TABLE dbo.MercanciasCartaPorte ADD MaterialPeligroso nvarchar(2) NULL;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104193407_20261104_A_CrucesFiscalesComercioExterior'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104223450_20261104_B_ClasificacionCartaPorte'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261104223450_20261104_B_ClasificacionCartaPorte'', N''10.0.14'');';
COMMIT;
