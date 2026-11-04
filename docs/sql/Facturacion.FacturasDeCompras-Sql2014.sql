-- Aplicar después de la configuración fiscal del proveedor, con respaldo previo.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.ConfiguracionFiscalProveedor', N'U') IS NULL
        THROW 51000, N'Falta aplicar la configuración fiscal del proveedor.', 1;
    IF OBJECT_ID(N'dbo.FacturasDeCompras', N'U') IS NOT NULL
        THROW 51000, N'La tabla de facturas de compras ya existe. No se aplicaron cambios.', 1;
    CREATE TABLE dbo.FacturasDeCompras (
        Id uniqueidentifier NOT NULL,
        CompraId uniqueidentifier NOT NULL,
        CuentaId uniqueidentifier NOT NULL,
        EmpresaId uniqueidentifier NOT NULL,
        Estado nvarchar(20) NOT NULL,
        EmisorRfc nvarchar(13) NOT NULL,
        EmisorNombre nvarchar(254) NOT NULL,
        EmisorRegimen nvarchar(3) NOT NULL,
        EmisorCodigoPostal nvarchar(5) NOT NULL,
        EmisorZonaHoraria nvarchar(64) NOT NULL,
        Serie nvarchar(25) NOT NULL,
        VersionProveedor varbinary(8) NOT NULL,
        ReceptorRfc nvarchar(13) NOT NULL,
        ReceptorNombre nvarchar(254) NOT NULL,
        ReceptorRegimen nvarchar(3) NOT NULL,
        ReceptorCodigoPostal nvarchar(5) NOT NULL,
        UsoCfdi nvarchar(3) NOT NULL,
        Descripcion nvarchar(1000) NOT NULL,
        CantidadTimbres int NOT NULL,
        ClaveProdServ nvarchar(8) NOT NULL,
        ClaveUnidad nvarchar(3) NOT NULL,
        Subtotal decimal(18,6) NOT NULL,
        Iva decimal(18,6) NOT NULL,
        TasaIva decimal(18,6) NOT NULL,
        Total decimal(18,6) NOT NULL,
        FormaPago nvarchar(2) NOT NULL,
        CreadaUtc datetime2 NOT NULL,
        ActualizadaUtc datetime2 NOT NULL,
        CreadaPorOperadorId uniqueidentifier NOT NULL,
        ActualizadaPorOperadorId uniqueidentifier NOT NULL,
        Version rowversion NOT NULL,
        CONSTRAINT PK_FacturasDeCompras PRIMARY KEY (Id),
        CONSTRAINT FK_FacturasDeCompras_ComprasTimbres_CompraId FOREIGN KEY (CompraId)
            REFERENCES dbo.ComprasTimbres (Id)
    );
    CREATE UNIQUE INDEX IX_FacturasDeCompras_CompraId ON dbo.FacturasDeCompras (CompraId);
    CREATE INDEX IX_FacturasDeCompras_CuentaId_CreadaUtc ON dbo.FacturasDeCompras (CuentaId, CreadaUtc);
    INSERT INTO dbo.OperadoresPermisos (OperadorId, Permiso)
    SELECT o.Id, N'panel_facturar_compras' FROM dbo.OperadoresPlataforma o
    WHERE o.EsPrincipal = 1 AND NOT EXISTS (
        SELECT 1 FROM dbo.OperadoresPermisos p
        WHERE p.OperadorId = o.Id AND p.Permiso = N'panel_facturar_compras');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
