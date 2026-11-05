SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.TrasladosCartaPorte', N'U') IS NULL OR OBJECT_ID(N'dbo.MercanciasCartaPorte', N'U') IS NULL
    THROW 50000, N'Faltan las tablas de Carta Porte.', 1;
IF COL_LENGTH(N'dbo.TrasladosCartaPorte', N'AseguraMedAmbiente') IS NULL
    ALTER TABLE dbo.TrasladosCartaPorte ADD AseguraMedAmbiente nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.TrasladosCartaPorte', N'PolizaMedAmbiente') IS NULL
    ALTER TABLE dbo.TrasladosCartaPorte ADD PolizaMedAmbiente nvarchar(30) NULL;
IF COL_LENGTH(N'dbo.MercanciasCartaPorte', N'CveMaterialPeligroso') IS NULL
    ALTER TABLE dbo.MercanciasCartaPorte ADD CveMaterialPeligroso nvarchar(4) NULL;
IF COL_LENGTH(N'dbo.MercanciasCartaPorte', N'Embalaje') IS NULL
    ALTER TABLE dbo.MercanciasCartaPorte ADD Embalaje nvarchar(5) NULL;
IF COL_LENGTH(N'dbo.MercanciasCartaPorte', N'DescripEmbalaje') IS NULL
    ALTER TABLE dbo.MercanciasCartaPorte ADD DescripEmbalaje nvarchar(100) NULL;
IF OBJECT_ID(N'dbo.SatMaterialPeligroso', N'U') IS NULL
    CREATE TABLE dbo.SatMaterialPeligroso (
        Clave nvarchar(4) NOT NULL CONSTRAINT PK_SatMaterialPeligroso PRIMARY KEY,
        Descripcion nvarchar(max) NOT NULL,
        FechaInicioVigencia date NOT NULL, FechaFinVigencia date NULL, Vigente bit NOT NULL
    );
IF OBJECT_ID(N'dbo.SatTipoEmbalaje', N'U') IS NULL
    CREATE TABLE dbo.SatTipoEmbalaje (
        Clave nvarchar(5) NOT NULL CONSTRAINT PK_SatTipoEmbalaje PRIMARY KEY,
        Descripcion nvarchar(500) NOT NULL,
        FechaInicioVigencia date NOT NULL, FechaFinVigencia date NULL, Vigente bit NOT NULL
    );
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104224848_20261104_B_RemolquesCartaPorte'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104230741_20261104_B_MaterialPeligrosoCartaPorte'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261104230741_20261104_B_MaterialPeligrosoCartaPorte'', N''10.0.14'');';
COMMIT;
