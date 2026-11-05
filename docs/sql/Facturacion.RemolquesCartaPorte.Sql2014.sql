SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.TrasladosCartaPorte', N'U') IS NULL
    THROW 50000, N'Falta la tabla de traslados Carta Porte.', 1;
IF OBJECT_ID(N'dbo.SatSubTipoRemolque', N'U') IS NULL
    CREATE TABLE dbo.SatSubTipoRemolque (
        Clave nvarchar(6) NOT NULL CONSTRAINT PK_SatSubTipoRemolque PRIMARY KEY,
        Descripcion nvarchar(500) NOT NULL,
        FechaInicioVigencia date NOT NULL,
        FechaFinVigencia date NULL,
        Vigente bit NOT NULL
    );
IF OBJECT_ID(N'dbo.RemolquesCartaPorte', N'U') IS NULL
    CREATE TABLE dbo.RemolquesCartaPorte (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_RemolquesCartaPorte PRIMARY KEY,
        EmpresaId uniqueidentifier NOT NULL,
        TrasladoCartaPorteId uniqueidentifier NOT NULL,
        Orden int NOT NULL,
        SubTipoRem nvarchar(6) NOT NULL,
        Placa nvarchar(7) NOT NULL,
        CONSTRAINT FK_RemolquesCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId
            FOREIGN KEY (TrasladoCartaPorteId) REFERENCES dbo.TrasladosCartaPorte(Id) ON DELETE CASCADE
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.RemolquesCartaPorte')
    AND name=N'IX_RemolquesCartaPorte_TrasladoCartaPorteId_Orden')
    CREATE UNIQUE INDEX IX_RemolquesCartaPorte_TrasladoCartaPorteId_Orden
        ON dbo.RemolquesCartaPorte(TrasladoCartaPorteId, Orden);
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104223450_20261104_B_ClasificacionCartaPorte'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104224848_20261104_B_RemolquesCartaPorte'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261104224848_20261104_B_RemolquesCartaPorte'', N''10.0.14'');';
COMMIT;
