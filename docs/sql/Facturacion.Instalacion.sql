SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [Bitacora] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] uniqueidentifier NULL,
        [CuentaId] uniqueidentifier NULL,
        [UsuarioId] uniqueidentifier NULL,
        [Entidad] nvarchar(128) NOT NULL,
        [EntidadId] nvarchar(64) NULL,
        [Accion] nvarchar(64) NOT NULL,
        [ValorAnterior] nvarchar(max) NULL,
        [ValorNuevo] nvarchar(max) NULL,
        [MomentoUtc] datetime2 NOT NULL,
        [TraceId] nvarchar(64) NULL,
        [IpOrigen] nvarchar(45) NULL,
        CONSTRAINT [PK_Bitacora] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [ClavesIdempotencia] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Clave] nvarchar(128) NOT NULL,
        [Endpoint] nvarchar(256) NOT NULL,
        [HashPeticion] nvarchar(64) NOT NULL,
        [CodigoEstado] int NOT NULL,
        [RespuestaJson] nvarchar(max) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ClavesIdempotencia] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [Cuentas] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [CorreoContacto] nvarchar(254) NOT NULL,
        [Activa] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Cuentas] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [Permisos] (
        [Clave] nvarchar(32) NOT NULL,
        [Descripcion] nvarchar(128) NOT NULL,
        CONSTRAINT [PK_Permisos] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [CuentaId] uniqueidentifier NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [Empresas] (
        [Id] uniqueidentifier NOT NULL,
        [CuentaId] uniqueidentifier NOT NULL,
        [Rfc] nvarchar(13) NOT NULL,
        [NombreFiscal] nvarchar(254) NOT NULL,
        [RegimenFiscal] nvarchar(3) NOT NULL,
        [CodigoPostalExpedicion] nvarchar(5) NOT NULL,
        [ZonaHoraria] nvarchar(64) NOT NULL,
        [Activa] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [LicNotarios] bit NOT NULL,
        [LicObras] bit NOT NULL,
        [LicComercio] bit NOT NULL,
        [LicINE] bit NOT NULL,
        CONSTRAINT [PK_Empresas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Empresas_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [FamiliaId] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [HashToken] nvarchar(64) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [ConsumidoUtc] datetime2 NULL,
        [RevocadoUtc] datetime2 NULL,
        [MotivoRevocacion] nvarchar(128) NULL,
        [ReemplazadoPorId] uniqueidentifier NULL,
        [IpCreacion] nvarchar(45) NULL,
        [AgenteUsuario] nvarchar(256) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [UsuariosEmpresas] (
        [UsuarioId] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UsuariosEmpresas] PRIMARY KEY ([UsuarioId], [EmpresaId]),
        CONSTRAINT [FK_UsuariosEmpresas_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuariosEmpresas_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE TABLE [UsuariosEmpresasPermisos] (
        [UsuarioId] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [PermisoClave] nvarchar(32) NOT NULL,
        [OtorgadoUtc] datetime2 NOT NULL,
        [OtorgadoPorUsuarioId] uniqueidentifier NULL,
        CONSTRAINT [PK_UsuariosEmpresasPermisos] PRIMARY KEY ([UsuarioId], [EmpresaId], [PermisoClave]),
        CONSTRAINT [FK_UsuariosEmpresasPermisos_Permisos_PermisoClave] FOREIGN KEY ([PermisoClave]) REFERENCES [Permisos] ([Clave]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuariosEmpresasPermisos_UsuariosEmpresas_UsuarioId_EmpresaId] FOREIGN KEY ([UsuarioId], [EmpresaId]) REFERENCES [UsuariosEmpresas] ([UsuarioId], [EmpresaId]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''administrar_usuarios'''', N''''Invitar usuarios y asignar permisos''''),
    (N''''cancelar'''', N''''Cancelar comprobantes timbrados''''),
    (N''''comprar_timbres'''', N''''Comprar paquetes de timbres''''),
    (N''''configurar_empresa'''', N''''Configurar la empresa, sus series y sus certificados''''),
    (N''''timbrar'''', N''''Emitir y timbrar comprobantes''''),
    (N''''ver_reportes'''', N''''Consultar reportes de la empresa'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AspNetUsers_CuentaId] ON [AspNetUsers] ([CuentaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Bitacora_CuentaId_MomentoUtc] ON [Bitacora] ([CuentaId], [MomentoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Bitacora_EmpresaId_MomentoUtc] ON [Bitacora] ([EmpresaId], [MomentoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ClavesIdempotencia_EmpresaId_Clave] ON [ClavesIdempotencia] ([EmpresaId], [Clave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ClavesIdempotencia_ExpiraUtc] ON [ClavesIdempotencia] ([ExpiraUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Empresas_CuentaId_Rfc] ON [Empresas] ([CuentaId], [Rfc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_RefreshTokens_FamiliaId] ON [RefreshTokens] ([FamiliaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RefreshTokens_HashToken] ON [RefreshTokens] ([HashToken]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_RefreshTokens_UsuarioId] ON [RefreshTokens] ([UsuarioId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_UsuariosEmpresas_EmpresaId] ON [UsuariosEmpresas] ([EmpresaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_UsuariosEmpresasPermisos_PermisoClave] ON [UsuariosEmpresasPermisos] ([PermisoClave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812152209_A_Fundacion'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260812152209_A_Fundacion'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812192743_A_Autenticacion'
)
BEGIN
    EXEC(N'ALTER TABLE [RefreshTokens] ADD [EmpresaActivaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812192743_A_Autenticacion'
)
BEGIN
    EXEC(N'ALTER TABLE [AspNetUsers] ADD [BloqueosConsecutivos] int NOT NULL DEFAULT 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812192743_A_Autenticacion'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260812192743_A_Autenticacion'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814143157_A_PreferenciaDeTema'
)
BEGIN
    EXEC(N'ALTER TABLE [AspNetUsers] ADD [TemaPreferido] nvarchar(16) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814143157_A_PreferenciaDeTema'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260814143157_A_PreferenciaDeTema'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [CatalogoVersion] (
        [Catalogo] nvarchar(32) NOT NULL,
        [VersionCatalogo] nvarchar(16) NOT NULL,
        [RevisionCatalogo] nvarchar(16) NOT NULL,
        [FechaPublicacion] date NULL,
        [FechaCargaUtc] datetime2 NOT NULL,
        [RenglonesVigentes] int NOT NULL,
        CONSTRAINT [PK_CatalogoVersion] PRIMARY KEY ([Catalogo])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatClaveProdServ] (
        [Clave] nvarchar(8) NOT NULL,
        [Descripcion] nvarchar(512) NOT NULL,
        [IncluirIvaTrasladado] nvarchar(16) NULL,
        [IncluirIepsTrasladado] nvarchar(16) NULL,
        [ComplementoQueDebeIncluir] nvarchar(128) NULL,
        [EstimuloFranjaFronteriza] bit NOT NULL,
        [PalabrasSimilares] nvarchar(1024) NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatClaveProdServ] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatClaveUnidad] (
        [Clave] nvarchar(20) NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [Descripcion] nvarchar(1024) NULL,
        [Nota] nvarchar(512) NULL,
        [Simbolo] nvarchar(32) NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatClaveUnidad] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatCodigoPostal] (
        [Clave] nvarchar(5) NOT NULL,
        [ClaveEstado] nvarchar(3) NOT NULL,
        [ClaveMunicipio] nvarchar(3) NULL,
        [ClaveLocalidad] nvarchar(4) NULL,
        [EstimuloFranjaFronteriza] bit NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatCodigoPostal] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatColonia] (
        [Clave] nvarchar(4) NOT NULL,
        [ClaveCodigoPostal] nvarchar(5) NOT NULL,
        [IdInterno] int NOT NULL IDENTITY,
        [Nombre] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatColonia] PRIMARY KEY ([Clave], [ClaveCodigoPostal])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatEstado] (
        [Clave] nvarchar(3) NOT NULL,
        [ClavePais] nvarchar(3) NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatEstado] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatExportacion] (
        [Clave] nvarchar(2) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatExportacion] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatFormaPago] (
        [Clave] nvarchar(4) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatFormaPago] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatImpuesto] (
        [Clave] nvarchar(3) NOT NULL,
        [Descripcion] nvarchar(64) NOT NULL,
        [Retencion] bit NOT NULL,
        [Traslado] bit NOT NULL,
        [LocalOFederal] nvarchar(16) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatImpuesto] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatMes] (
        [Clave] nvarchar(2) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatMes] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatMetodoPago] (
        [Clave] nvarchar(3) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatMetodoPago] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatMoneda] (
        [Clave] nvarchar(3) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [Decimales] int NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatMoneda] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatMunicipio] (
        [Clave] nvarchar(3) NOT NULL,
        [ClaveEstado] nvarchar(3) NOT NULL,
        [IdInterno] int NOT NULL IDENTITY,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatMunicipio] PRIMARY KEY ([Clave], [ClaveEstado])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatObjetoImp] (
        [Clave] nvarchar(2) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatObjetoImp] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatPais] (
        [Clave] nvarchar(3) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatPais] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatPeriodicidad] (
        [Clave] nvarchar(2) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatPeriodicidad] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatRegimenFiscal] (
        [Clave] nvarchar(3) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [AplicaFisica] bit NOT NULL,
        [AplicaMoral] bit NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatRegimenFiscal] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatTasaOCuota] (
        [Clave] nvarchar(64) NOT NULL,
        [RangoOFijo] nvarchar(16) NOT NULL,
        [ValorMinimo] decimal(18,6) NULL,
        [ValorMaximo] decimal(18,6) NOT NULL,
        [Impuesto] nvarchar(3) NOT NULL,
        [Factor] nvarchar(16) NOT NULL,
        [Traslado] bit NOT NULL,
        [Retencion] bit NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatTasaOCuota] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatTipoDeComprobante] (
        [Clave] nvarchar(1) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [ValorMaximo] decimal(18,6) NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatTipoDeComprobante] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatTipoFactor] (
        [Clave] nvarchar(16) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatTipoFactor] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatTipoRelacion] (
        [Clave] nvarchar(2) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatTipoRelacion] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE TABLE [SatUsoCfdi] (
        [Clave] nvarchar(4) NOT NULL,
        [Descripcion] nvarchar(254) NOT NULL,
        [AplicaFisica] bit NOT NULL,
        [AplicaMoral] bit NOT NULL,
        [RegimenesFiscalesAplicables] nvarchar(512) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatUsoCfdi] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SatColonia_ClaveCodigoPostal] ON [SatColonia] ([ClaveCodigoPostal]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SatColonia_IdInterno] ON [SatColonia] ([IdInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SatMunicipio_IdInterno] ON [SatMunicipio] ([IdInterno]);');
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = ''CatalogoTextoCompleto'')
        CREATE FULLTEXT CATALOG CatalogoTextoCompleto AS DEFAULT;

    CREATE FULLTEXT INDEX ON SatClaveProdServ(Descripcion LANGUAGE 3082, PalabrasSimilares LANGUAGE 3082)
        KEY INDEX PK_SatClaveProdServ ON CatalogoTextoCompleto
        WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;

    CREATE FULLTEXT INDEX ON SatColonia(Nombre LANGUAGE 3082)
        KEY INDEX IX_SatColonia_IdInterno ON CatalogoTextoCompleto
        WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;

    CREATE FULLTEXT INDEX ON SatMunicipio(Descripcion LANGUAGE 3082)
        KEY INDEX IX_SatMunicipio_IdInterno ON CatalogoTextoCompleto
        WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214305_A_CatalogosSat'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260814214305_A_CatalogosSat'', N''10.0.10'');');
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214724_A_AnchoTasaOCuota'
)
BEGIN
    EXEC(N'DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[SatTasaOCuota]'') AND [c].[name] = N''Impuesto'');
    IF @var IS NOT NULL EXEC(N''ALTER TABLE [SatTasaOCuota] DROP CONSTRAINT '' + @var + '';'');
    ALTER TABLE [SatTasaOCuota] ALTER COLUMN [Impuesto] nvarchar(64) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214724_A_AnchoTasaOCuota'
)
BEGIN
    EXEC(N'DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[SatTasaOCuota]'') AND [c].[name] = N''Clave'');
    IF @var1 IS NOT NULL EXEC(N''ALTER TABLE [SatTasaOCuota] DROP CONSTRAINT '' + @var1 + '';'');
    ALTER TABLE [SatTasaOCuota] ALTER COLUMN [Clave] nvarchar(160) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814214724_A_AnchoTasaOCuota'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260814214724_A_AnchoTasaOCuota'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Calle] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CodigoPostal] nvarchar(5) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Colonia] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CorreoContacto] nvarchar(254) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Estado] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Localidad] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [LogoNombreOriginal] nvarchar(256) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [LogoRuta] nvarchar(256) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [LogoTipoMime] nvarchar(64) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Municipio] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [NumeroExterior] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [NumeroInterior] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Pais] nvarchar(64) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Referencia] nvarchar(256) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [Telefono] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE TABLE [CertificadosCsd] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [NumeroSerie] nvarchar(20) NOT NULL,
        [VigenciaDesdeUtc] datetime2 NOT NULL,
        [VigenciaHastaUtc] datetime2 NOT NULL,
        [RutaCer] nvarchar(256) NOT NULL,
        [RutaKey] nvarchar(256) NOT NULL,
        [ContrasenaCifrada] nvarchar(1024) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaCargaUtc] datetime2 NOT NULL,
        [CargadoPorUsuarioId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CertificadosCsd] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CertificadosCsd_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE TABLE [ConfiguracionesEmpresa] (
        [EmpresaId] uniqueidentifier NOT NULL,
        [TasaIvaPorDefecto] decimal(18,6) NOT NULL,
        [TasaRetencionIvaPorDefecto] decimal(18,6) NOT NULL,
        [TasaRetencionIsrPorDefecto] decimal(18,6) NOT NULL,
        [DiasAvisoCaducidadCertificado] int NOT NULL,
        CONSTRAINT [PK_ConfiguracionesEmpresa] PRIMARY KEY ([EmpresaId]),
        CONSTRAINT [FK_ConfiguracionesEmpresa_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE TABLE [Series] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Prefijo] nvarchar(25) NOT NULL,
        [FolioInicial] int NOT NULL,
        [FolioActual] int NOT NULL,
        [TipoComprobante] nvarchar(1) NOT NULL,
        [Activa] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Series] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Series_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE TABLE [ReservasFolio] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [SerieId] uniqueidentifier NOT NULL,
        [Folio] int NOT NULL,
        [Estado] nvarchar(16) NOT NULL,
        [ComprobanteId] uniqueidentifier NULL,
        [MomentoUtc] datetime2 NOT NULL,
        [MomentoResolucionUtc] datetime2 NULL,
        CONSTRAINT [PK_ReservasFolio] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReservasFolio_Series_SerieId] FOREIGN KEY ([SerieId]) REFERENCES [Series] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_CertificadosCsd_UnoActivoPorEmpresa] ON [CertificadosCsd] ([EmpresaId]) WHERE [Activo] = 1'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ReservasFolio_EmpresaId_Estado] ON [ReservasFolio] ([EmpresaId], [Estado]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ReservasFolio_SinFolioRepetido] ON [ReservasFolio] ([SerieId], [Folio]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Series_EmpresaId_Prefijo] ON [Series] ([EmpresaId], [Prefijo]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ReservarFolio'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ReservarFolio;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ReservarFolio
        @SerieId    uniqueidentifier,
        @EmpresaId  uniqueidentifier,
        @ReservaId  uniqueidentifier,
        @MomentoUtc datetime2(7)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @Folio int, @Prefijo nvarchar(25);

        BEGIN TRANSACTION;

        UPDATE s WITH (UPDLOCK, ROWLOCK)
           SET @Folio   = s.FolioActual + 1,
               s.FolioActual = s.FolioActual + 1,
               @Prefijo = s.Prefijo
          FROM dbo.Series AS s
         WHERE s.Id = @SerieId
           AND s.EmpresaId = @EmpresaId
           AND s.Activa = 1;

        IF @Folio IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, ''La serie no existe, no es de esta empresa, o está inactiva.'', 1;
        END

        INSERT INTO dbo.ReservasFolio (Id, EmpresaId, SerieId, Folio, Estado, MomentoUtc)
        VALUES (@ReservaId, @EmpresaId, @SerieId, @Folio, ''reservado'', @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT @ReservaId AS ReservaId,
               @SerieId   AS SerieId,
               @Prefijo   AS Serie,
               @Folio     AS Folio;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814225631_A_EmpresaYFolios'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260814225631_A_EmpresaYFolios'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815021339_A_Clientes'
)
BEGIN
    EXEC(N'CREATE TABLE [Clientes] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ClaveInterna] int NOT NULL,
        [Rfc] nvarchar(13) NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [RegimenFiscal] nvarchar(3) NOT NULL,
        [DomicilioFiscalCp] nvarchar(5) NOT NULL,
        [ResidenciaFiscal] nvarchar(3) NULL,
        [NumRegIdTrib] nvarchar(40) NULL,
        [UsoCfdiPreferido] nvarchar(4) NULL,
        [MetodoPagoPreferido] nvarchar(3) NULL,
        [FormaPagoPreferida] nvarchar(4) NULL,
        [Telefono] nvarchar(32) NULL,
        [CorreoPrincipal] nvarchar(254) NULL,
        [Calle] nvarchar(128) NULL,
        [NumeroExterior] nvarchar(32) NULL,
        [NumeroInterior] nvarchar(32) NULL,
        [Colonia] nvarchar(128) NULL,
        [Localidad] nvarchar(128) NULL,
        [Referencia] nvarchar(256) NULL,
        [Municipio] nvarchar(128) NULL,
        [Estado] nvarchar(128) NULL,
        [Pais] nvarchar(64) NULL,
        [CodigoPostal] nvarchar(5) NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [FechaModificacionUtc] datetime2 NULL,
        CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Clientes_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815021339_A_Clientes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_ClaveInternaPorEmpresa] ON [Clientes] ([EmpresaId], [ClaveInterna]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815021339_A_Clientes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Clientes_EmpresaId_Activo_Nombre] ON [Clientes] ([EmpresaId], [Activo], [Nombre]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815021339_A_Clientes'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Clientes_RfcPorEmpresa] ON [Clientes] ([EmpresaId], [Rfc]) WHERE [Rfc] <> ''''XAXX010101000'''' AND [Rfc] <> ''''XEXX010101000'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815021339_A_Clientes'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260815021339_A_Clientes'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'CREATE TABLE [Productos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [CodigoInterno] int NOT NULL,
        [ClaveProdServ] nvarchar(8) NOT NULL,
        [ClaveUnidad] nvarchar(20) NOT NULL,
        [UnidadTexto] nvarchar(64) NOT NULL,
        [Descripcion] nvarchar(1000) NOT NULL,
        [ValorUnitario] decimal(18,6) NOT NULL,
        [PesoKg] decimal(18,6) NULL,
        [ObjetoImp] nvarchar(2) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [FechaModificacionUtc] datetime2 NULL,
        CONSTRAINT [PK_Productos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Productos_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'CREATE TABLE [ProductosImpuestos] (
        [Id] uniqueidentifier NOT NULL,
        [ProductoId] uniqueidentifier NOT NULL,
        [Impuesto] nvarchar(3) NOT NULL,
        [TipoFactor] nvarchar(16) NOT NULL,
        [TasaOCuota] decimal(18,6) NULL,
        [EsRetencion] bit NOT NULL,
        CONSTRAINT [PK_ProductosImpuestos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductosImpuestos_Productos_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [Productos] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Productos_CodigoInternoPorEmpresa] ON [Productos] ([EmpresaId], [CodigoInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_EmpresaId_Activo_Descripcion] ON [Productos] ([EmpresaId], [Activo], [Descripcion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductosImpuestos_SinImpuestoRepetido] ON [ProductosImpuestos] ([ProductoId], [Impuesto], [EsRetencion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815040739_A_Productos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260815040739_A_Productos'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [BolsasTimbres] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Disponibles] int NOT NULL,
        [Reservados] int NOT NULL,
        [ActualizadaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BolsasTimbres] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BolsasTimbres_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [ComprasTimbres] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [PaqueteId] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [NombrePaquete] nvarchar(100) NOT NULL,
        [CantidadTimbres] int NOT NULL,
        [PrecioPorTimbre] decimal(18,6) NOT NULL,
        [PrecioTotal] decimal(18,6) NOT NULL,
        [VigenciaMeses] int NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [CreadaUtc] datetime2 NOT NULL,
        [AcreditadaUtc] datetime2 NULL,
        [VenceUtc] datetime2 NULL,
        CONSTRAINT [PK_ComprasTimbres] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [Membresias] (
        [Id] uniqueidentifier NOT NULL,
        [CuentaId] uniqueidentifier NOT NULL,
        [InicioUtc] datetime2 NOT NULL,
        [FinUtc] datetime2 NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [CreadaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Membresias] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Membresias_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [MovimientosTimbre] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [DeltaDisponible] int NOT NULL,
        [DeltaReservado] int NOT NULL,
        [DisponiblesDespues] int NOT NULL,
        [ReservadosDespues] int NOT NULL,
        [Motivo] nvarchar(300) NULL,
        [UsuarioId] uniqueidentifier NULL,
        [CompraId] uniqueidentifier NULL,
        [ReservaId] uniqueidentifier NULL,
        [MomentoUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_MovimientosTimbre] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [Paquetes] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [CantidadTimbres] int NOT NULL,
        [PrecioPorTimbre] decimal(18,6) NOT NULL,
        [PrecioTotal] decimal(18,6) NOT NULL,
        [VigenciaMeses] int NOT NULL,
        [Activo] bit NOT NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_Paquetes] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE TABLE [ReservasTimbre] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [CreadaUtc] datetime2 NOT NULL,
        [ResueltaUtc] datetime2 NULL,
        [MotivoResolucion] nvarchar(300) NULL,
        CONSTRAINT [PK_ReservasTimbre] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BolsasTimbres_EmpresaId] ON [BolsasTimbres] ([EmpresaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ComprasTimbres_EmpresaId_CreadaUtc] ON [ComprasTimbres] ([EmpresaId], [CreadaUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'EXEC(N''CREATE INDEX [IX_ComprasTimbres_Estado] ON [ComprasTimbres] ([Estado]) WHERE [Estado] = ''''pendiente_de_pago'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Membresias_CuentaId_FinUtc] ON [Membresias] ([CuentaId], [FinUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MovimientosTimbre_EmpresaId_MomentoUtc] ON [MovimientosTimbre] ([EmpresaId], [MomentoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Paquetes_Activo_Orden] ON [Paquetes] ([Activo], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'EXEC(N''CREATE INDEX [IX_ReservasTimbre_CreadaUtc] ON [ReservasTimbre] ([CreadaUtc]) WHERE [Estado] = ''''reservado'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ReservasTimbre_EmpresaId_ComprobanteId] ON [ReservasTimbre] ([EmpresaId], [ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ReservarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ReservarTimbre
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
            (@ReservaId, @EmpresaId, @ComprobanteId, ''reservado'', @MomentoUtc);

        INSERT INTO dbo.MovimientosTimbre
            (Id, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
        VALUES
            (NEWID(), @EmpresaId, ''reserva'', -1, 1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Reservado, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ConfirmarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ConfirmarTimbre
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
           SET r.Estado      = ''confirmado'',
               r.ResueltaUtc = @MomentoUtc
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

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
            (NEWID(), @EmpresaId, ''consumo'', 0, -1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.DevolverTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.DevolverTimbre
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
           SET r.Estado           = ''devuelto'',
               r.ResueltaUtc      = @MomentoUtc,
               r.MotivoResolucion = @Motivo
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

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
            (NEWID(), @EmpresaId, ''devolucion'', 1, -1,
             @Disponibles, @Reservados, @ReservaId, @Motivo, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.AcreditarCompra'', N''P'') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.AcreditarCompra
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
           SET c.Estado        = ''pagada'',
               c.AcreditadaUtc = @MomentoUtc,
               c.VenceUtc      = DATEADD(month, c.VigenciaMeses, @MomentoUtc),
               @EmpresaId      = c.EmpresaId,
               @Cantidad       = c.CantidadTimbres,
               @Vigencia       = c.VigenciaMeses
          FROM dbo.ComprasTimbres AS c
         WHERE c.Id = @CompraId
           AND c.Estado = ''pendiente_de_pago'';

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
            (NEWID(), @EmpresaId, ''compra'', @Cantidad, 0,
             @Disponibles, @Reservados, @CompraId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Acreditada, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134713_A_Timbres'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260815134713_A_Timbres'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134957_A_PaquetesSembrados'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''Activo'', N''CantidadTimbres'', N''Nombre'', N''Orden'', N''PrecioPorTimbre'', N''PrecioTotal'', N''VigenciaMeses'') AND [object_id] = OBJECT_ID(N''[Paquetes]''))
        SET IDENTITY_INSERT [Paquetes] ON;
    EXEC(N''INSERT INTO [Paquetes] ([Id], [Activo], [CantidadTimbres], [Nombre], [Orden], [PrecioPorTimbre], [PrecioTotal], [VigenciaMeses])
    VALUES (''''9c1f0a10-0000-4000-8000-000000000001'''', CAST(1 AS bit), 500, N''''500 timbres'''', 1, 2.0, 1000.0, 12),
    (''''9c1f0a10-0000-4000-8000-000000000002'''', CAST(1 AS bit), 1000, N''''1000 timbres'''', 2, 1.8, 1800.0, 12)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''Activo'', N''CantidadTimbres'', N''Nombre'', N''Orden'', N''PrecioPorTimbre'', N''PrecioTotal'', N''VigenciaMeses'') AND [object_id] = OBJECT_ID(N''[Paquetes]''))
        SET IDENTITY_INSERT [Paquetes] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815134957_A_PaquetesSembrados'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260815134957_A_PaquetesSembrados'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817145049_A_Usuarios'
)
BEGIN
    EXEC(N'CREATE TABLE [Invitaciones] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Correo] nvarchar(254) NOT NULL,
        [Nombre] nvarchar(128) NOT NULL,
        [HashToken] nvarchar(64) NOT NULL,
        [PermisosClaves] nvarchar(256) NOT NULL,
        [CreadaUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [AceptadaUtc] datetime2 NULL,
        [RevocadaUtc] datetime2 NULL,
        [CreadaPorUsuarioId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Invitaciones] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817145049_A_Usuarios'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Invitaciones_HashToken] ON [Invitaciones] ([HashToken]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817145049_A_Usuarios'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Invitaciones_PendientePorCorreoYEmpresa] ON [Invitaciones] ([EmpresaId], [Correo]) WHERE [AceptadaUtc] IS NULL AND [RevocadaUtc] IS NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817145049_A_Usuarios'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260817145049_A_Usuarios'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817182648_A_EmpresaEnProductoImpuesto'
)
BEGIN
    EXEC(N'ALTER TABLE [ProductosImpuestos] ADD [EmpresaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817182648_A_EmpresaEnProductoImpuesto'
)
BEGIN
    EXEC(N'UPDATE impuesto
    SET impuesto.EmpresaId = producto.EmpresaId
    FROM ProductosImpuestos AS impuesto
    INNER JOIN Productos AS producto ON producto.Id = impuesto.ProductoId;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817182648_A_EmpresaEnProductoImpuesto'
)
BEGIN
    EXEC(N'DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[ProductosImpuestos]'') AND [c].[name] = N''EmpresaId'');
    IF @var2 IS NOT NULL EXEC(N''ALTER TABLE [ProductosImpuestos] DROP CONSTRAINT '' + @var2 + '';'');
    ALTER TABLE [ProductosImpuestos] ALTER COLUMN [EmpresaId] uniqueidentifier NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817182648_A_EmpresaEnProductoImpuesto'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260817182648_A_EmpresaEnProductoImpuesto'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [Comprobantes] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Estatus] nvarchar(16) NOT NULL,
        [SerieId] uniqueidentifier NULL,
        [Serie] nvarchar(25) NULL,
        [Folio] int NULL,
        [Uuid] uniqueidentifier NULL,
        [TipoDeComprobante] nvarchar(1) NOT NULL,
        [FechaEmisionUtc] datetime2 NOT NULL,
        [LugarExpedicion] nvarchar(5) NOT NULL,
        [Moneda] nvarchar(3) NOT NULL,
        [TipoCambio] decimal(18,6) NULL,
        [FormaPago] nvarchar(2) NULL,
        [MetodoPago] nvarchar(3) NULL,
        [Exportacion] nvarchar(2) NOT NULL,
        [CondicionesDePago] nvarchar(1000) NULL,
        [EmisorRfc] nvarchar(13) NOT NULL,
        [EmisorNombre] nvarchar(254) NOT NULL,
        [EmisorRegimenFiscal] nvarchar(3) NOT NULL,
        [ClienteId] uniqueidentifier NULL,
        [ReceptorRfc] nvarchar(13) NOT NULL,
        [ReceptorNombre] nvarchar(254) NOT NULL,
        [ReceptorRegimenFiscal] nvarchar(3) NOT NULL,
        [ReceptorDomicilioFiscal] nvarchar(5) NOT NULL,
        [ReceptorUsoCfdi] nvarchar(3) NOT NULL,
        [GlobalPeriodicidad] nvarchar(2) NULL,
        [GlobalMeses] nvarchar(2) NULL,
        [GlobalAnio] int NULL,
        [SubTotal] decimal(18,6) NOT NULL,
        [Descuento] decimal(18,6) NOT NULL,
        [TotalImpuestosTrasladados] decimal(18,6) NOT NULL,
        [TotalImpuestosRetenidos] decimal(18,6) NOT NULL,
        [Total] decimal(18,6) NOT NULL,
        [FechaTimbradoUtc] datetime2 NULL,
        [NoCertificadoEmisor] nvarchar(20) NULL,
        [NoCertificadoSat] nvarchar(20) NULL,
        [SelloCfd] nvarchar(max) NULL,
        [SelloSat] nvarchar(max) NULL,
        [CadenaOriginalSat] nvarchar(max) NULL,
        [RutaXml] nvarchar(400) NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ModificadoUtc] datetime2 NULL,
        [CreadoPorUsuarioId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Comprobantes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comprobantes_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [ComprobantesRelacionados] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [TipoRelacion] nvarchar(2) NOT NULL,
        [UuidRelacionado] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ComprobantesRelacionados] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComprobantesRelacionados_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [Conceptos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Orden] int NOT NULL,
        [ProductoId] uniqueidentifier NULL,
        [ClaveProdServ] nvarchar(8) NOT NULL,
        [ClaveUnidad] nvarchar(20) NOT NULL,
        [UnidadTexto] nvarchar(64) NULL,
        [NoIdentificacion] nvarchar(100) NULL,
        [Descripcion] nvarchar(1000) NOT NULL,
        [Cantidad] decimal(18,6) NOT NULL,
        [ValorUnitario] decimal(18,6) NOT NULL,
        [Importe] decimal(18,6) NOT NULL,
        [Descuento] decimal(18,6) NOT NULL,
        [ObjetoImp] nvarchar(2) NOT NULL,
        CONSTRAINT [PK_Conceptos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conceptos_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [IntentosTimbrado] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Numero] int NOT NULL,
        [IniciadoUtc] datetime2 NOT NULL,
        [TerminadoUtc] datetime2 NULL,
        [Resultado] nvarchar(32) NOT NULL,
        [ClaveIdempotencia] nvarchar(128) NULL,
        [CodigoError] nvarchar(32) NULL,
        [MensajeError] nvarchar(2000) NULL,
        [DuracionMs] int NULL,
        CONSTRAINT [PK_IntentosTimbrado] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IntentosTimbrado_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [SolicitudesCancelacion] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Motivo] nvarchar(2) NOT NULL,
        [UuidSustituye] uniqueidentifier NULL,
        [Estado] nvarchar(32) NOT NULL,
        [SolicitadaUtc] datetime2 NOT NULL,
        [ResueltaUtc] datetime2 NULL,
        [CodigoRespuesta] nvarchar(32) NULL,
        [MensajeRespuesta] nvarchar(2000) NULL,
        [SolicitadaPorUsuarioId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SolicitudesCancelacion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SolicitudesCancelacion_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE TABLE [ConceptosImpuestos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ConceptoId] uniqueidentifier NOT NULL,
        [Impuesto] nvarchar(3) NOT NULL,
        [TipoFactor] nvarchar(16) NOT NULL,
        [TasaOCuota] decimal(18,6) NULL,
        [Base] decimal(18,6) NOT NULL,
        [Importe] decimal(18,6) NULL,
        [EsRetencion] bit NOT NULL,
        CONSTRAINT [PK_ConceptosImpuestos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConceptosImpuestos_Conceptos_ConceptoId] FOREIGN KEY ([ConceptoId]) REFERENCES [Conceptos] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Comprobantes_EmpresaId_Estatus_FechaEmisionUtc] ON [Comprobantes] ([EmpresaId], [Estatus], [FechaEmisionUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Comprobantes_EmpresaId_FechaEmisionUtc] ON [Comprobantes] ([EmpresaId], [FechaEmisionUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Comprobantes_FolioPorSerie] ON [Comprobantes] ([EmpresaId], [SerieId], [Folio]) WHERE [Folio] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Comprobantes_Uuid] ON [Comprobantes] ([Uuid]) WHERE [Uuid] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ComprobantesRelacionados_SinRepetir] ON [ComprobantesRelacionados] ([ComprobanteId], [UuidRelacionado]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Conceptos_OrdenPorComprobante] ON [Conceptos] ([ComprobanteId], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ConceptosImpuestos_SinImpuestoRepetido] ON [ConceptosImpuestos] ([ConceptoId], [Impuesto], [EsRetencion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'EXEC(N''CREATE INDEX [IX_IntentosTimbrado_EnVuelo] ON [IntentosTimbrado] ([EmpresaId], [IniciadoUtc]) WHERE [Resultado] = ''''en_vuelo'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_IntentosTimbrado_NumeroPorComprobante] ON [IntentosTimbrado] ([ComprobanteId], [Numero]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SolicitudesCancelacion_EmpresaId_Estado] ON [SolicitudesCancelacion] ([EmpresaId], [Estado]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SolicitudesCancelacion_PorComprobante] ON [SolicitudesCancelacion] ([ComprobanteId], [SolicitadaUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817211601_B_DocumentosBase'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260817211601_B_DocumentosBase'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818160413_B_XmlEnviadoEnIntento'
)
BEGIN
    EXEC(N'ALTER TABLE [IntentosTimbrado] ADD [XmlEnviado] nvarchar(max) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818160413_B_XmlEnviadoEnIntento'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260818160413_B_XmlEnviadoEnIntento'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818170939_B2_ObservacionesDeComprobante'
)
BEGIN
    EXEC(N'ALTER TABLE [Comprobantes] ADD [Observaciones] nvarchar(2000) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818170939_B2_ObservacionesDeComprobante'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260818170939_B2_ObservacionesDeComprobante'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE TABLE [Pagos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [FechaPagoUtc] datetime2 NOT NULL,
        [FormaDePagoP] nvarchar(2) NOT NULL,
        [MonedaP] nvarchar(3) NOT NULL,
        [TipoCambioP] decimal(18,6) NULL,
        [Monto] decimal(18,6) NOT NULL,
        [NumOperacion] nvarchar(100) NULL,
        [RfcEmisorCtaOrd] nvarchar(13) NULL,
        [NomBancoOrdExt] nvarchar(300) NULL,
        [CtaOrdenante] nvarchar(50) NULL,
        [RfcEmisorCtaBen] nvarchar(13) NULL,
        [CtaBeneficiario] nvarchar(50) NULL,
        CONSTRAINT [PK_Pagos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pagos_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE TABLE [PagosDocumentos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [PagoId] uniqueidentifier NOT NULL,
        [ComprobantePagadoId] uniqueidentifier NULL,
        [IdDocumento] uniqueidentifier NOT NULL,
        [Serie] nvarchar(25) NULL,
        [Folio] nvarchar(40) NULL,
        [MonedaDR] nvarchar(3) NOT NULL,
        [EquivalenciaDR] decimal(18,6) NOT NULL,
        [NumParcialidad] int NOT NULL,
        [ImpSaldoAnt] decimal(18,6) NOT NULL,
        [ImpPagado] decimal(18,6) NOT NULL,
        [ImpSaldoInsoluto] decimal(18,6) NOT NULL,
        [ObjetoImpDR] nvarchar(2) NOT NULL,
        CONSTRAINT [PK_PagosDocumentos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PagosDocumentos_Pagos_PagoId] FOREIGN KEY ([PagoId]) REFERENCES [Pagos] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE TABLE [PagosDocumentosImpuestos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [DocumentoPagadoId] uniqueidentifier NOT NULL,
        [Impuesto] nvarchar(3) NOT NULL,
        [TipoFactor] nvarchar(16) NOT NULL,
        [TasaOCuota] decimal(18,6) NULL,
        [Base] decimal(18,6) NOT NULL,
        [Importe] decimal(18,6) NULL,
        [EsRetencion] bit NOT NULL,
        CONSTRAINT [PK_PagosDocumentosImpuestos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PagosDocumentosImpuestos_PagosDocumentos_DocumentoPagadoId] FOREIGN KEY ([DocumentoPagadoId]) REFERENCES [PagosDocumentos] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Pagos_UnoPorComprobante] ON [Pagos] ([ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PagosDocumentos_PorDocumento] ON [PagosDocumentos] ([IdDocumento]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PagosDocumentos_SinDocumentoRepetido] ON [PagosDocumentos] ([PagoId], [IdDocumento]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_PagosDocumentosImpuestos_SinImpuestoRepetido] ON [PagosDocumentosImpuestos] ([DocumentoPagadoId], [Impuesto], [EsRetencion], [TasaOCuota]) WHERE [TasaOCuota] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818195153_B9_ComplementoDePagos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260818195153_B9_ComplementoDePagos'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818211507_B9_UsoCfdiDeCuatroCaracteres'
)
BEGIN
    EXEC(N'DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[Comprobantes]'') AND [c].[name] = N''ReceptorUsoCfdi'');
    IF @var3 IS NOT NULL EXEC(N''ALTER TABLE [Comprobantes] DROP CONSTRAINT '' + @var3 + '';'');
    ALTER TABLE [Comprobantes] ALTER COLUMN [ReceptorUsoCfdi] nvarchar(4) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818211507_B9_UsoCfdiDeCuatroCaracteres'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260818211507_B9_UsoCfdiDeCuatroCaracteres'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822173916_A_UsuarioCreadoPorAdministrador'
)
BEGIN
    EXEC(N'DROP TABLE [Invitaciones];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822173916_A_UsuarioCreadoPorAdministrador'
)
BEGIN
    EXEC(N'ALTER TABLE [AspNetUsers] ADD [CreadoPorAdministrador] bit NOT NULL DEFAULT CAST(0 AS bit);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822173916_A_UsuarioCreadoPorAdministrador'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Permisos] SET [Descripcion] = N''''Dar de alta usuarios y asignar permisos''''
    WHERE [Clave] = N''''administrar_usuarios'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822173916_A_UsuarioCreadoPorAdministrador'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260822173916_A_UsuarioCreadoPorAdministrador'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824153220_20260824_B0_DoblesDePrueba'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260824153220_20260824_B0_DoblesDePrueba'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194351_A_PaquetesCorregidos'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [Orden] = 3
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000001'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194351_A_PaquetesCorregidos'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [Orden] = 5
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000002'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194351_A_PaquetesCorregidos'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''Activo'', N''CantidadTimbres'', N''Nombre'', N''Orden'', N''PrecioPorTimbre'', N''PrecioTotal'', N''VigenciaMeses'') AND [object_id] = OBJECT_ID(N''[Paquetes]''))
        SET IDENTITY_INSERT [Paquetes] ON;
    EXEC(N''INSERT INTO [Paquetes] ([Id], [Activo], [CantidadTimbres], [Nombre], [Orden], [PrecioPorTimbre], [PrecioTotal], [VigenciaMeses])
    VALUES (''''9c1f0a10-0000-4000-8000-000000000006'''', CAST(1 AS bit), 50, N''''50 timbres'''', 1, 3.5, 175.0, 12),
    (''''9c1f0a10-0000-4000-8000-000000000007'''', CAST(1 AS bit), 200, N''''200 timbres'''', 2, 2.5, 500.0, 12),
    (''''9c1f0a10-0000-4000-8000-000000000008'''', CAST(1 AS bit), 800, N''''800 timbres'''', 4, 1.9, 1520.0, 12)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''Activo'', N''CantidadTimbres'', N''Nombre'', N''Orden'', N''PrecioPorTimbre'', N''PrecioTotal'', N''VigenciaMeses'') AND [object_id] = OBJECT_ID(N''[Paquetes]''))
        SET IDENTITY_INSERT [Paquetes] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194351_A_PaquetesCorregidos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260826194351_A_PaquetesCorregidos'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'ALTER TABLE [Bitacora] ADD [OperadorId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE TABLE [OperadoresPlataforma] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(128) NOT NULL,
        [Correo] nvarchar(254) NOT NULL,
        [CorreoNormalizado] nvarchar(254) NOT NULL,
        [HashContrasena] nvarchar(256) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [UltimoAccesoUtc] datetime2 NULL,
        [AccesosFallidos] int NOT NULL,
        [BloqueadoHastaUtc] datetime2 NULL,
        [BloqueosConsecutivos] int NOT NULL,
        CONSTRAINT [PK_OperadoresPlataforma] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE TABLE [RefreshTokensOperador] (
        [Id] uniqueidentifier NOT NULL,
        [FamiliaId] uniqueidentifier NOT NULL,
        [OperadorId] uniqueidentifier NOT NULL,
        [HashToken] nvarchar(64) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [ConsumidoUtc] datetime2 NULL,
        [RevocadoUtc] datetime2 NULL,
        [MotivoRevocacion] nvarchar(128) NULL,
        [ReemplazadoPorId] uniqueidentifier NULL,
        [IpCreacion] nvarchar(45) NULL,
        [AgenteUsuario] nvarchar(256) NULL,
        CONSTRAINT [PK_RefreshTokensOperador] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokensOperador_OperadoresPlataforma_OperadorId] FOREIGN KEY ([OperadorId]) REFERENCES [OperadoresPlataforma] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'EXEC(N''CREATE INDEX [IX_Bitacora_PorOperador] ON [Bitacora] ([OperadorId], [MomentoUtc]) WHERE [OperadorId] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OperadoresPlataforma_Correo] ON [OperadoresPlataforma] ([CorreoNormalizado]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_RefreshTokensOperador_FamiliaId] ON [RefreshTokensOperador] ([FamiliaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RefreshTokensOperador_HashToken] ON [RefreshTokensOperador] ([HashToken]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_RefreshTokensOperador_OperadorId] ON [RefreshTokensOperador] ([OperadorId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826194853_A_OperadorDePlataforma'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260826194853_A_OperadorDePlataforma'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826205845_A_RechazoDeCompra'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [CanceladaUtc] datetime2 NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826205845_A_RechazoDeCompra'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [MotivoCancelacion] nvarchar(300) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826205845_A_RechazoDeCompra'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260826205845_A_RechazoDeCompra'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828174950_A_AltaConVerificacion'
)
BEGIN
    EXEC(N'CREATE TABLE [AltasPendientes] (
        [Id] uniqueidentifier NOT NULL,
        [Correo] nvarchar(254) NOT NULL,
        [Nombre] nvarchar(128) NOT NULL,
        [NombreCuenta] nvarchar(200) NOT NULL,
        [HashCodigo] nvarchar(256) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [Intentos] int NOT NULL,
        [ConsumidoUtc] datetime2 NULL,
        [IpCreacion] nvarchar(45) NULL,
        CONSTRAINT [PK_AltasPendientes] PRIMARY KEY ([Id])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828174950_A_AltaConVerificacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AltasPendientes_Correo] ON [AltasPendientes] ([Correo]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828174950_A_AltaConVerificacion'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AltasPendientes_Expira] ON [AltasPendientes] ([ExpiraUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828174950_A_AltaConVerificacion'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260828174950_A_AltaConVerificacion'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902152616_A_OperadoresPermisos'
)
BEGIN
    EXEC(N'CREATE TABLE [OperadoresPermisos] (
        [OperadorId] uniqueidentifier NOT NULL,
        [Permiso] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_OperadoresPermisos] PRIMARY KEY ([OperadorId], [Permiso]),
        CONSTRAINT [FK_OperadoresPermisos_OperadoresPlataforma_OperadorId] FOREIGN KEY ([OperadorId]) REFERENCES [OperadoresPlataforma] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902152616_A_OperadoresPermisos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OperadoresPermisos_Permiso] ON [OperadoresPermisos] ([Permiso]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902152616_A_OperadoresPermisos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260902152616_A_OperadoresPermisos'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908202757_A_OperadorEsPrincipal'
)
BEGIN
    EXEC(N'ALTER TABLE [OperadoresPlataforma] ADD [EsPrincipal] bit NOT NULL DEFAULT CAST(0 AS bit);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908202757_A_OperadorEsPrincipal'
)
BEGIN
    EXEC(N'UPDATE OperadoresPlataforma
    SET EsPrincipal = 1
    WHERE Id = (SELECT TOP (1) Id FROM OperadoresPlataforma ORDER BY FechaAltaUtc, Id);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908202757_A_OperadorEsPrincipal'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260908202757_A_OperadorEsPrincipal'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908215330_A_PermisosDeOperadorDePanel'
)
BEGIN
    EXEC(N'DECLARE @conPermisos TABLE (OperadorId uniqueidentifier PRIMARY KEY);

    INSERT INTO @conPermisos (OperadorId)
    SELECT DISTINCT OperadorId FROM OperadoresPermisos;

    DELETE FROM OperadoresPermisos;

    INSERT INTO OperadoresPermisos (OperadorId, Permiso)
    SELECT o.OperadorId, p.Permiso
    FROM @conPermisos o
    CROSS APPLY (VALUES
        (N''panel_ver_paquetes''),
        (N''panel_administrar_paquetes''),
        (N''panel_ver_compras''),
        (N''panel_acreditar_compras''),
        (N''panel_ver_clientes''),
        (N''panel_administrar_clientes''),
        (N''panel_asignar_timbres''),
        (N''panel_ver_usuarios''),
        (N''panel_administrar_usuarios''),
        (N''panel_ver_operadores''),
        (N''panel_administrar_operadores''),
        (N''panel_ver_configuracion''),
        (N''panel_administrar_configuracion'')) AS p(Permiso);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908215330_A_PermisosDeOperadorDePanel'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260908215330_A_PermisosDeOperadorDePanel'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225821_A_ConfiguracionDelSistemaPersistente'
)
BEGIN
    EXEC(N'CREATE TABLE [ConfiguracionDelSistema] (
        [Id] tinyint NOT NULL,
        [ServidorSmtp] nvarchar(253) NOT NULL,
        [PuertoSmtp] int NOT NULL,
        [UsuarioSmtp] nvarchar(254) NOT NULL,
        [ContrasenaSmtpCifrada] nvarchar(2048) NULL,
        [RemitenteCorreo] nvarchar(254) NOT NULL,
        [RemitenteNombre] nvarchar(128) NOT NULL,
        [UsarTls] bit NOT NULL,
        [NombreDelSistema] nvarchar(128) NOT NULL,
        [AsuntoVerificacion] nvarchar(128) NOT NULL,
        [CuerpoVerificacion] nvarchar(max) NOT NULL,
        [AsuntoContrasena] nvarchar(128) NOT NULL,
        [CuerpoContrasena] nvarchar(max) NOT NULL,
        [ActualizadaUtc] datetime2 NOT NULL,
        [ActualizadaPorOperadorId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ConfiguracionDelSistema] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ConfiguracionDelSistema_Unica] CHECK ([Id] = 1)
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225821_A_ConfiguracionDelSistemaPersistente'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260909225821_A_ConfiguracionDelSistemaPersistente'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914164057_A_ComprobanteDeCompra'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [EmpresaNombreAlComprar] nvarchar(254) NOT NULL DEFAULT N'''';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914164057_A_ComprobanteDeCompra'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [EmpresaRfcAlComprar] nvarchar(13) NOT NULL DEFAULT N'''';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914164057_A_ComprobanteDeCompra'
)
BEGIN
    EXEC(N'UPDATE c
       SET c.EmpresaNombreAlComprar = e.NombreFiscal,
           c.EmpresaRfcAlComprar = e.Rfc
      FROM ComprasTimbres AS c
      INNER JOIN Empresas AS e ON e.Id = c.EmpresaId;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914164057_A_ComprobanteDeCompra'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260914164057_A_ComprobanteDeCompra'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Paquetes_Activo_Orden] ON [Paquetes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] ADD [EmpresaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [Iva] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [Subtotal] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [TasaIva] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'UPDATE ComprasTimbres
    SET TasaIva = 0.16,
        Subtotal = ROUND(PrecioTotal / 1.16, 6),
        Iva = PrecioTotal - ROUND(PrecioTotal / 1.16, 6)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [EmpresaId] = NULL
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000001'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [EmpresaId] = NULL
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000002'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [EmpresaId] = NULL
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000006'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [EmpresaId] = NULL
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000007'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Paquetes] SET [EmpresaId] = NULL
    WHERE [Id] = ''''9c1f0a10-0000-4000-8000-000000000008'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Paquetes_EmpresaId_Activo_Orden] ON [Paquetes] ([EmpresaId], [Activo], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] ADD CONSTRAINT [FK_Paquetes_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914200621_A_PaquetesPersonalizadosEIva'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260914200621_A_PaquetesPersonalizadosEIva'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918221238_20260918_A_CatalogosCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [SatConfiguracionAutotransporte] (
        [Clave] nvarchar(10) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatConfiguracionAutotransporte] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918221238_20260918_A_CatalogosCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [SatFiguraTransporte] (
        [Clave] nvarchar(10) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatFiguraTransporte] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918221238_20260918_A_CatalogosCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [SatTipoPermiso] (
        [Clave] nvarchar(10) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatTipoPermiso] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918221238_20260918_A_CatalogosCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260918221238_20260918_A_CatalogosCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE TABLE [FigurasTransporte] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Clave] nvarchar(30) NOT NULL,
        [TipoFigura] nvarchar(10) NOT NULL,
        [Rfc] nvarchar(13) NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [NumeroLicencia] nvarchar(50) NULL,
        [Calle] nvarchar(150) NOT NULL,
        [NumeroExterior] nvarchar(55) NOT NULL,
        [NumeroInterior] nvarchar(55) NULL,
        [Estado] nvarchar(3) NOT NULL,
        [Municipio] nvarchar(3) NOT NULL,
        [CodigoPostal] nvarchar(5) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [FechaModificacionUtc] datetime2 NULL,
        CONSTRAINT [PK_FigurasTransporte] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FigurasTransporte_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE TABLE [Vehiculos] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [Clave] nvarchar(30) NOT NULL,
        [Descripcion] nvarchar(250) NOT NULL,
        [ConfiguracionAutotransporte] nvarchar(10) NOT NULL,
        [Placa] nvarchar(20) NOT NULL,
        [AnioModelo] int NOT NULL,
        [Aseguradora] nvarchar(150) NOT NULL,
        [Poliza] nvarchar(50) NOT NULL,
        [TipoPermiso] nvarchar(10) NOT NULL,
        [NumeroPermiso] nvarchar(50) NOT NULL,
        [PesoBrutoVehicular] decimal(18,6) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaAltaUtc] datetime2 NOT NULL,
        [FechaModificacionUtc] datetime2 NULL,
        CONSTRAINT [PK_Vehiculos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vehiculos_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_FigurasTransporte_EmpresaId_Activo_Nombre] ON [FigurasTransporte] ([EmpresaId], [Activo], [Nombre]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FigurasTransporte_EmpresaId_Clave] ON [FigurasTransporte] ([EmpresaId], [Clave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Vehiculos_EmpresaId_Activo_Descripcion] ON [Vehiculos] ([EmpresaId], [Activo], [Descripcion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Vehiculos_EmpresaId_Clave] ON [Vehiculos] ([EmpresaId], [Clave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919155318_20260919_A_CatalogosDeTransporte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260919155318_20260919_A_CatalogosDeTransporte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [TrasladosCartaPorte] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [VehiculoId] uniqueidentifier NOT NULL,
        [FiguraTransporteId] uniqueidentifier NOT NULL,
        [FechaSalidaUtc] datetime2 NOT NULL,
        [FechaLlegadaUtc] datetime2 NOT NULL,
        [DistanciaRecorridaKm] decimal(18,6) NOT NULL,
        [PesoBrutoTotalKg] decimal(18,6) NOT NULL,
        [TotalMercancias] decimal(18,6) NOT NULL,
        CONSTRAINT [PK_TrasladosCartaPorte] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrasladosCartaPorte_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [MercanciasCartaPorte] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [TrasladoCartaPorteId] uniqueidentifier NOT NULL,
        [Orden] int NOT NULL,
        [ClaveProdServ] nvarchar(8) NOT NULL,
        [Descripcion] nvarchar(1000) NOT NULL,
        [Cantidad] decimal(18,6) NOT NULL,
        [ClaveUnidad] nvarchar(20) NOT NULL,
        [PesoEnKg] decimal(18,6) NOT NULL,
        CONSTRAINT [PK_MercanciasCartaPorte] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MercanciasCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId] FOREIGN KEY ([TrasladoCartaPorteId]) REFERENCES [TrasladosCartaPorte] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [UbicacionesCartaPorte] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [TrasladoCartaPorteId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(8) NOT NULL,
        [Orden] int NOT NULL,
        [RfcRemitenteDestinatario] nvarchar(13) NULL,
        [Calle] nvarchar(150) NOT NULL,
        [NumeroExterior] nvarchar(55) NOT NULL,
        [NumeroInterior] nvarchar(55) NULL,
        [Estado] nvarchar(3) NOT NULL,
        [Municipio] nvarchar(3) NOT NULL,
        [CodigoPostal] nvarchar(5) NOT NULL,
        CONSTRAINT [PK_UbicacionesCartaPorte] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UbicacionesCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId] FOREIGN KEY ([TrasladoCartaPorteId]) REFERENCES [TrasladosCartaPorte] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MercanciasCartaPorte_TrasladoCartaPorteId_Orden] ON [MercanciasCartaPorte] ([TrasladoCartaPorteId], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TrasladosCartaPorte_ComprobanteId] ON [TrasladosCartaPorte] ([ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UbicacionesCartaPorte_TrasladoCartaPorteId_Orden] ON [UbicacionesCartaPorte] ([TrasladoCartaPorteId], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919162552_20260919_B_TrasladoCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260919162552_20260919_B_TrasladoCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TrasladosCartaPorte_FiguraTransporteId] ON [TrasladosCartaPorte] ([FiguraTransporteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TrasladosCartaPorte_VehiculoId] ON [TrasladosCartaPorte] ([VehiculoId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD CONSTRAINT [FK_TrasladosCartaPorte_FigurasTransporte_FiguraTransporteId] FOREIGN KEY ([FiguraTransporteId]) REFERENCES [FigurasTransporte] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD CONSTRAINT [FK_TrasladosCartaPorte_Vehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [Vehiculos] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [FiguraNombre] nvarchar(254) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [FiguraNumeroLicencia] nvarchar(50) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [FiguraRfc] nvarchar(13) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [FiguraTipo] nvarchar(10) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [IdCcp] nvarchar(36) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoAnioModelo] int NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoAseguradora] nvarchar(150) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoConfiguracionAutotransporte] nvarchar(10) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoNumeroPermiso] nvarchar(50) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoPlaca] nvarchar(20) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoPoliza] nvarchar(50) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoTipoPermiso] nvarchar(10) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_TrasladosCartaPorte_IdCcp] ON [TrasladosCartaPorte] ([IdCcp]) WHERE [IdCcp] IS NOT NULL'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922150726__20260922_C_DatosInmutablesCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922150726__20260922_C_DatosInmutablesCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922151905__20260922_D_PesoVehiculoCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [VehiculoPesoBruto] decimal(18,6) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922151905__20260922_D_PesoVehiculoCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922151905__20260922_D_PesoVehiculoCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922152717__20260922_E_ClaveProdServCartaPorte'
)
BEGIN
    EXEC(N'CREATE TABLE [SatClaveProdServCartaPorte] (
        [Clave] nvarchar(8) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatClaveProdServCartaPorte] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922152717__20260922_E_ClaveProdServCartaPorte'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SatClaveProdServCartaPorte_Vigente_Clave] ON [SatClaveProdServCartaPorte] ([Vigente], [Clave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922152717__20260922_E_ClaveProdServCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922152717__20260922_E_ClaveProdServCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162355_20260922_F_ConfiguracionNotaria'
)
BEGIN
    EXEC(N'CREATE TABLE [ConfiguracionesNotario] (
        [EmpresaId] uniqueidentifier NOT NULL,
        [Curp] nvarchar(18) NOT NULL,
        [NumeroNotaria] int NOT NULL,
        [Estado] nvarchar(3) NOT NULL,
        [Adscripcion] nvarchar(255) NULL,
        [ModificadoUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ConfiguracionesNotario] PRIMARY KEY ([EmpresaId]),
        CONSTRAINT [FK_ConfiguracionesNotario_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162355_20260922_F_ConfiguracionNotaria'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922162355_20260922_F_ConfiguracionNotaria'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922163942__20260922_G_DatosOperacionNotaria'
)
BEGIN
    EXEC(N'CREATE TABLE [DatosNotaria] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [NumeroInstrumentoNotarial] int NOT NULL,
        [FechaInstrumentoNotarial] date NOT NULL,
        [MontoOperacion] decimal(18,6) NOT NULL,
        [SubtotalOperacion] decimal(18,6) NOT NULL,
        [IvaOperacion] decimal(18,6) NOT NULL,
        CONSTRAINT [PK_DatosNotaria] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DatosNotaria_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922163942__20260922_G_DatosOperacionNotaria'
)
BEGIN
    EXEC(N'CREATE TABLE [InmueblesNotariales] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [DatosNotariaId] uniqueidentifier NOT NULL,
        [Orden] int NOT NULL,
        [TipoInmueble] nvarchar(2) NOT NULL,
        [Calle] nvarchar(150) NOT NULL,
        [NumeroExterior] nvarchar(55) NULL,
        [NumeroInterior] nvarchar(30) NULL,
        [Colonia] nvarchar(100) NULL,
        [Localidad] nvarchar(100) NULL,
        [Referencia] nvarchar(100) NULL,
        [Municipio] nvarchar(100) NOT NULL,
        [Estado] nvarchar(2) NOT NULL,
        [Pais] nvarchar(3) NOT NULL,
        [CodigoPostal] nvarchar(5) NOT NULL,
        CONSTRAINT [PK_InmueblesNotariales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InmueblesNotariales_DatosNotaria_DatosNotariaId] FOREIGN KEY ([DatosNotariaId]) REFERENCES [DatosNotaria] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922163942__20260922_G_DatosOperacionNotaria'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DatosNotaria_ComprobanteId] ON [DatosNotaria] ([ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922163942__20260922_G_DatosOperacionNotaria'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InmueblesNotariales_DatosNotariaId_Orden] ON [InmueblesNotariales] ([DatosNotariaId], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922163942__20260922_G_DatosOperacionNotaria'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922163942__20260922_G_DatosOperacionNotaria'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922193744__20260922_H_PartesNotariales'
)
BEGIN
    EXEC(N'ALTER TABLE [DatosNotaria] ADD [AdquirentesEnCopropiedad] bit NOT NULL DEFAULT CAST(0 AS bit);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922193744__20260922_H_PartesNotariales'
)
BEGIN
    EXEC(N'ALTER TABLE [DatosNotaria] ADD [EnajenantesEnCopropiedad] bit NOT NULL DEFAULT CAST(0 AS bit);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922193744__20260922_H_PartesNotariales'
)
BEGIN
    EXEC(N'CREATE TABLE [PartesNotariales] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [DatosNotariaId] uniqueidentifier NOT NULL,
        [Rol] nvarchar(12) NOT NULL,
        [Orden] int NOT NULL,
        [Nombre] nvarchar(254) NOT NULL,
        [ApellidoPaterno] nvarchar(200) NULL,
        [ApellidoMaterno] nvarchar(200) NULL,
        [Rfc] nvarchar(13) NOT NULL,
        [Curp] nvarchar(18) NULL,
        [Porcentaje] decimal(5,2) NULL,
        CONSTRAINT [PK_PartesNotariales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartesNotariales_DatosNotaria_DatosNotariaId] FOREIGN KEY ([DatosNotariaId]) REFERENCES [DatosNotaria] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922193744__20260922_H_PartesNotariales'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PartesNotariales_DatosNotariaId_Rol_Orden] ON [PartesNotariales] ([DatosNotariaId], [Rol], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922193744__20260922_H_PartesNotariales'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922193744__20260922_H_PartesNotariales'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922214425_20260922_I_DatosObra'
)
BEGIN
    EXEC(N'CREATE TABLE [DatosObra] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [PorcentajeAmortizacion] decimal(8,4) NOT NULL,
        [Retenciones] decimal(18,6) NOT NULL,
        [Devoluciones] decimal(18,6) NOT NULL,
        [PorcentajeIva] decimal(8,4) NOT NULL,
        [NombreDeduccion1] nvarchar(60) NOT NULL,
        [PorcentajeDeduccion1] decimal(8,4) NOT NULL,
        [NombreDeduccion2] nvarchar(60) NOT NULL,
        [PorcentajeDeduccion2] decimal(8,4) NOT NULL,
        [NombreDeduccion3] nvarchar(60) NOT NULL,
        [PorcentajeDeduccion3] decimal(8,4) NOT NULL,
        [NombreDeduccion4] nvarchar(60) NOT NULL,
        [PorcentajeDeduccion4] decimal(8,4) NOT NULL,
        CONSTRAINT [PK_DatosObra] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DatosObra_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922214425_20260922_I_DatosObra'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DatosObra_ComprobanteId] ON [DatosObra] ([ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922214425_20260922_I_DatosObra'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922214425_20260922_I_DatosObra'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922222934_20260922_J_PorcentajesRetencionesObra'
)
BEGIN
    EXEC(N'ALTER TABLE [DatosObra] ADD [PorcentajeDevoluciones] decimal(8,4) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922222934_20260922_J_PorcentajesRetencionesObra'
)
BEGIN
    EXEC(N'ALTER TABLE [DatosObra] ADD [PorcentajeRetenciones] decimal(8,4) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922222934_20260922_J_PorcentajesRetencionesObra'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922222934_20260922_J_PorcentajesRetencionesObra'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922231307_20260922_K_DatosComercioExterior'
)
BEGIN
    EXEC(N'CREATE TABLE [DatosComercioExterior] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Contenido] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_DatosComercioExterior] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DatosComercioExterior_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922231307_20260922_K_DatosComercioExterior'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DatosComercioExterior_ComprobanteId] ON [DatosComercioExterior] ([ComprobanteId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922231307_20260922_K_DatosComercioExterior'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260922231307_20260922_K_DatosComercioExterior'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923152505_20260923_L_CatalogosComercioExterior'
)
BEGIN
    EXEC(N'CREATE TABLE [SatFraccionArancelaria] (
        [Clave] nvarchar(12) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatFraccionArancelaria] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923152505_20260923_L_CatalogosComercioExterior'
)
BEGIN
    EXEC(N'CREATE TABLE [SatIncoterm] (
        [Clave] nvarchar(10) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatIncoterm] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923152505_20260923_L_CatalogosComercioExterior'
)
BEGIN
    EXEC(N'CREATE TABLE [SatUnidadAduana] (
        [Clave] nvarchar(10) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [FechaInicioVigencia] date NOT NULL,
        [FechaFinVigencia] date NULL,
        [Vigente] bit NOT NULL,
        CONSTRAINT [PK_SatUnidadAduana] PRIMARY KEY ([Clave])
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923152505_20260923_L_CatalogosComercioExterior'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SatFraccionArancelaria_Vigente_Clave] ON [SatFraccionArancelaria] ([Vigente], [Clave]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923152505_20260923_L_CatalogosComercioExterior'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260923152505_20260923_L_CatalogosComercioExterior'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923163809_20260923_M_LongitudFraccionArancelaria'
)
BEGIN
    EXEC(N'DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[SatFraccionArancelaria]'') AND [c].[name] = N''Descripcion'');
    IF @var4 IS NOT NULL EXEC(N''ALTER TABLE [SatFraccionArancelaria] DROP CONSTRAINT '' + @var4 + '';'');
    ALTER TABLE [SatFraccionArancelaria] ALTER COLUMN [Descripcion] nvarchar(2000) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923163809_20260923_M_LongitudFraccionArancelaria'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260923163809_20260923_M_LongitudFraccionArancelaria'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924150447_20260924_N_TipoObra'
)
BEGIN
    EXEC(N'ALTER TABLE [DatosObra] ADD [TipoObra] nvarchar(10) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924150447_20260924_N_TipoObra'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260924150447_20260924_N_TipoObra'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924190228_20260924_O_UnidadDimensionesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [MercanciasCartaPorte] ADD [Dimensiones] nvarchar(14) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924190228_20260924_O_UnidadDimensionesCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [MercanciasCartaPorte] ADD [Unidad] nvarchar(20) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924190228_20260924_O_UnidadDimensionesCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260924190228_20260924_O_UnidadDimensionesCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924191512_20260924_P_PesoUnitarioCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [MercanciasCartaPorte] ADD [PesoUnitarioKg] decimal(18,6) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924191512_20260924_P_PesoUnitarioCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260924191512_20260924_P_PesoUnitarioCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924194704_20260924_Q_DestinatarioCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [UbicacionesCartaPorte] ADD [NombreRemitenteDestinatario] nvarchar(254) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924194704_20260924_Q_DestinatarioCartaPorte'
)
BEGIN
    EXEC(N'ALTER TABLE [TrasladosCartaPorte] ADD [ClienteDestinoId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924194704_20260924_Q_DestinatarioCartaPorte'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260924194704_20260924_Q_DestinatarioCartaPorte'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteCalle] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteCodigoPostal] nvarchar(5) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteEstado] nvarchar(3) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteMunicipio] nvarchar(3) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteNumeroExterior] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [Empresas] ADD [CartaPorteNumeroInterior] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260924223133_20260924_R_OrigenCartaPorteEmpresa'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926152430_20260926_B_EnviosDeCorreo'
)
BEGIN
    EXEC(N'CREATE TABLE [EnviosDeCorreo] (
        [Id] uniqueidentifier NOT NULL,
        [EmpresaId] uniqueidentifier NOT NULL,
        [ComprobanteId] uniqueidentifier NOT NULL,
        [Destinatarios] nvarchar(2600) NOT NULL,
        [CopiaOculta] nvarchar(254) NULL,
        [Asunto] nvarchar(200) NOT NULL,
        [IncluyoXml] bit NOT NULL,
        [Exitoso] bit NOT NULL,
        [Error] nvarchar(64) NULL,
        [EnviadoPorUsuarioId] uniqueidentifier NOT NULL,
        [EnviadoUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EnviosDeCorreo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnviosDeCorreo_Comprobantes_ComprobanteId] FOREIGN KEY ([ComprobanteId]) REFERENCES [Comprobantes] ([Id]) ON DELETE CASCADE
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926152430_20260926_B_EnviosDeCorreo'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_EnviosDeCorreo_PorComprobante] ON [EnviosDeCorreo] ([ComprobanteId], [EnviadoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926152430_20260926_B_EnviosDeCorreo'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260926152430_20260926_B_EnviosDeCorreo'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926172053_20260926_B_ConsultaSatEnSolicitud'
)
BEGIN
    EXEC(N'ALTER TABLE [SolicitudesCancelacion] ADD [ConsultadaUtc] datetime2 NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926172053_20260926_B_ConsultaSatEnSolicitud'
)
BEGIN
    EXEC(N'ALTER TABLE [SolicitudesCancelacion] ADD [EsCancelableSat] nvarchar(64) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926172053_20260926_B_ConsultaSatEnSolicitud'
)
BEGIN
    EXEC(N'ALTER TABLE [SolicitudesCancelacion] ADD [EstadoCfdiSat] nvarchar(32) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926172053_20260926_B_ConsultaSatEnSolicitud'
)
BEGIN
    EXEC(N'ALTER TABLE [SolicitudesCancelacion] ADD [EstatusCancelacionSat] nvarchar(64) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926172053_20260926_B_ConsultaSatEnSolicitud'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20260926172053_20260926_B_ConsultaSatEnSolicitud'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001193227_20261001_B_VarianteDeFactura'
)
BEGIN
    EXEC(N'ALTER TABLE [Comprobantes] ADD [Variante] nvarchar(20) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001193227_20261001_B_VarianteDeFactura'
)
BEGIN
    EXEC(N'EXEC(N''
    UPDATE c SET Variante = CASE
        WHEN EXISTS (SELECT 1 FROM DatosObra o WHERE o.ComprobanteId = c.Id) THEN ''''obra''''
        WHEN c.Exportacion = ''''02''''
          OR EXISTS (SELECT 1 FROM DatosComercioExterior ce WHERE ce.ComprobanteId = c.Id) THEN ''''comercio_exterior''''
        WHEN EXISTS (SELECT 1 FROM DatosNotaria n WHERE n.ComprobanteId = c.Id) THEN ''''notaria''''
        ELSE ''''basica''''
    END
    FROM Comprobantes c
    WHERE c.TipoDeComprobante = ''''I'''';
    '');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001193227_20261001_B_VarianteDeFactura'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261001193227_20261001_B_VarianteDeFactura'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003165439_20261003_A_ClavesAsignadasPorElCodigo'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261003165439_20261003_A_ClavesAsignadasPorElCodigo'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005200758_20261005_A_CorreoDeEmpresa'
)
BEGIN
    EXEC(N'ALTER TABLE [ConfiguracionesEmpresa] ADD [TasaRetencionIepsPorDefecto] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005200758_20261005_A_CorreoDeEmpresa'
)
BEGIN
    EXEC(N'CREATE TABLE [CorreosDeEmpresa] (
        [EmpresaId] uniqueidentifier NOT NULL,
        [Habilitado] bit NOT NULL,
        [Servidor] nvarchar(253) NULL,
        [Puerto] int NOT NULL,
        [Usuario] nvarchar(254) NULL,
        [ContrasenaCifrada] nvarchar(2048) NULL,
        [RemitenteNombre] nvarchar(128) NULL,
        [RemitenteCorreo] nvarchar(254) NULL,
        CONSTRAINT [PK_CorreosDeEmpresa] PRIMARY KEY ([EmpresaId]),
        CONSTRAINT [FK_CorreosDeEmpresa_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005200758_20261005_A_CorreoDeEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261005200758_20261005_A_CorreoDeEmpresa'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006173844_20261006_A_RemitenteDelSistema'
)
BEGIN
    EXEC(N'ALTER TABLE [CorreosDeEmpresa] ADD [NombreRemitenteSistema] nvarchar(128) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006173844_20261006_A_RemitenteDelSistema'
)
BEGIN
    EXEC(N'ALTER TABLE [CorreosDeEmpresa] ADD [ResponderA] nvarchar(254) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006173844_20261006_A_RemitenteDelSistema'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261006173844_20261006_A_RemitenteDelSistema'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006175837_20261006_A_ImpuestosPorOmision'
)
BEGIN
    EXEC(N'ALTER TABLE [ConfiguracionesEmpresa] ADD [TasaIepsPorDefecto] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006175837_20261006_A_ImpuestosPorOmision'
)
BEGIN
    EXEC(N'ALTER TABLE [ConfiguracionesEmpresa] ADD [TasaIshPorDefecto] decimal(18,6) NOT NULL DEFAULT 0.0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006175837_20261006_A_ImpuestosPorOmision'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261006175837_20261006_A_ImpuestosPorOmision'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007212052_20261007_B_EstadoInmuebleNotarial'
)
BEGIN
    EXEC(N'DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N''[InmueblesNotariales]'') AND [c].[name] = N''Estado'');
    IF @var5 IS NOT NULL EXEC(N''ALTER TABLE [InmueblesNotariales] DROP CONSTRAINT '' + @var5 + '';'');
    ALTER TABLE [InmueblesNotariales] ALTER COLUMN [Estado] nvarchar(3) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007212052_20261007_B_EstadoInmuebleNotarial'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261007212052_20261007_B_EstadoInmuebleNotarial'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007215004_20261007_B_ReceptorExtranjero'
)
BEGIN
    EXEC(N'ALTER TABLE [Comprobantes] ADD [ReceptorNumRegIdTrib] nvarchar(40) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007215004_20261007_B_ReceptorExtranjero'
)
BEGIN
    EXEC(N'ALTER TABLE [Comprobantes] ADD [ReceptorResidenciaFiscal] nvarchar(3) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007215004_20261007_B_ReceptorExtranjero'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261007215004_20261007_B_ReceptorExtranjero'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'ALTER TABLE [AspNetUsers] ADD [EsTitular] bit NOT NULL DEFAULT CAST(0 AS bit);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Permisos] SET [Descripcion] = N''''Consultar el saldo y comprar paquetes de timbres''''
    WHERE [Clave] = N''''comprar_timbres'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''administrar_clientes'''', N''''Dar de alta, editar y dar de baja clientes''''),
    (N''''administrar_figuras'''', N''''Dar de alta, editar y dar de baja figuras de transporte''''),
    (N''''administrar_productos'''', N''''Dar de alta, editar y dar de baja productos''''),
    (N''''administrar_vehiculos'''', N''''Dar de alta, editar y dar de baja vehículos''''),
    (N''''configuracion'''', N''''Configurar correo, impuestos, logo, Notaría y series de la empresa''''),
    (N''''emitir_carta_porte'''', N''''Emitir traslados con Carta Porte''''),
    (N''''emitir_comercio_exterior'''', N''''Emitir facturas con Comercio Exterior''''),
    (N''''emitir_factura'''', N''''Emitir facturas básicas''''),
    (N''''emitir_notaria'''', N''''Emitir facturas con complemento de Notarios Públicos''''),
    (N''''emitir_obra'''', N''''Emitir facturas de obra''''),
    (N''''emitir_pago'''', N''''Emitir complementos de pago''''),
    (N''''enviar_correo'''', N''''Enviar comprobantes por correo''''),
    (N''''mi_empresa'''', N''''Editar los datos fiscales y los certificados de la empresa''''),
    (N''''ver_documentos'''', N''''Consultar y descargar comprobantes'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'WITH Candidatos AS (
        SELECT Id, ROW_NUMBER() OVER (
            PARTITION BY CuentaId
            ORDER BY CreadoPorAdministrador, FechaAltaUtc, Id) AS Orden
        FROM AspNetUsers)
    UPDATE u SET EsTitular = 1
    FROM AspNetUsers u
    JOIN Candidatos c ON c.Id = u.Id
    WHERE c.Orden = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'INSERT INTO UsuariosEmpresas (UsuarioId, EmpresaId, Activo, FechaAltaUtc)
    SELECT u.Id, e.Id, 1, SYSUTCDATETIME()
    FROM AspNetUsers u
    JOIN Empresas e ON e.CuentaId = u.CuentaId
    WHERE u.EsTitular = 1
      AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresas ue
                      WHERE ue.UsuarioId = u.Id AND ue.EmpresaId = e.Id);

    UPDATE ue SET Activo = 1
    FROM UsuariosEmpresas ue
    JOIN AspNetUsers u ON u.Id = ue.UsuarioId
    WHERE u.EsTitular = 1 AND ue.Activo = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'WITH Equivalencias (Vieja, Nueva) AS (
        SELECT * FROM (VALUES
            (''timbrar'', ''ver_documentos''),
            (''timbrar'', ''emitir_factura''),
            (''timbrar'', ''emitir_notaria''),
            (''timbrar'', ''emitir_carta_porte''),
            (''timbrar'', ''emitir_comercio_exterior''),
            (''timbrar'', ''emitir_obra''),
            (''timbrar'', ''emitir_pago''),
            (''timbrar'', ''enviar_correo''),
            (''configurar_empresa'', ''mi_empresa''),
            (''configurar_empresa'', ''configuracion''),
            (''configurar_empresa'', ''administrar_clientes''),
            (''configurar_empresa'', ''administrar_productos''),
            (''configurar_empresa'', ''administrar_vehiculos''),
            (''configurar_empresa'', ''administrar_figuras'')) AS v (Vieja, Nueva)),
    Nuevos AS (
        SELECT p.UsuarioId, p.EmpresaId, e.Nueva, p.OtorgadoUtc, p.OtorgadoPorUsuarioId,
               ROW_NUMBER() OVER (PARTITION BY p.UsuarioId, p.EmpresaId, e.Nueva
                                  ORDER BY p.OtorgadoUtc) AS Orden
        FROM UsuariosEmpresasPermisos p
        JOIN Equivalencias e ON e.Vieja = p.PermisoClave)
    INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT n.UsuarioId, n.EmpresaId, n.Nueva, n.OtorgadoUtc, n.OtorgadoPorUsuarioId
    FROM Nuevos n
    WHERE n.Orden = 1
      AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                      WHERE x.UsuarioId = n.UsuarioId AND x.EmpresaId = n.EmpresaId
                        AND x.PermisoClave = n.Nueva);

    DELETE FROM UsuariosEmpresasPermisos
    WHERE PermisoClave IN (''timbrar'', ''configurar_empresa'', ''administrar_usuarios'', ''ver_reportes'');

    -- El titular no guarda permisos: el token le pone todos.
    DELETE p
    FROM UsuariosEmpresasPermisos p
    JOIN AspNetUsers u ON u.Id = p.UsuarioId
    WHERE u.EsTitular = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''timbrar'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''configurar_empresa'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_usuarios'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''ver_reportes'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_AspNetUsers_CuentaId_Titular] ON [AspNetUsers] ([CuentaId]) WHERE [EsTitular] = 1'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009161413_20261009_A_PermisosPorSeccionYTitular'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261009161413_20261009_A_PermisosPorSeccionYTitular'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Permisos] SET [Descripcion] = N''''Timbres''''
    WHERE [Clave] = N''''comprar_timbres'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''carta_porte_cancelar'''', N''''Carta Porte: Cancelar''''),
    (N''''carta_porte_crear'''', N''''Carta Porte: Crear''''),
    (N''''carta_porte_eliminar'''', N''''Carta Porte: Eliminar borrador''''),
    (N''''carta_porte_enviar'''', N''''Carta Porte: Enviar por correo''''),
    (N''''carta_porte_modificar'''', N''''Carta Porte: Modificar''''),
    (N''''carta_porte_timbrar'''', N''''Carta Porte: Timbrar''''),
    (N''''clientes_crear'''', N''''Clientes: Crear''''),
    (N''''clientes_desactivar'''', N''''Clientes: Desactivar y reactivar''''),
    (N''''clientes_eliminar'''', N''''Clientes: Eliminar''''),
    (N''''clientes_modificar'''', N''''Clientes: Modificar''''),
    (N''''comercio_exterior_cancelar'''', N''''Comercio Exterior: Cancelar''''),
    (N''''comercio_exterior_crear'''', N''''Comercio Exterior: Crear''''),
    (N''''comercio_exterior_eliminar'''', N''''Comercio Exterior: Eliminar borrador''''),
    (N''''comercio_exterior_enviar'''', N''''Comercio Exterior: Enviar por correo''''),
    (N''''comercio_exterior_modificar'''', N''''Comercio Exterior: Modificar''''),
    (N''''comercio_exterior_timbrar'''', N''''Comercio Exterior: Timbrar''''),
    (N''''configuracion_correo'''', N''''Configuración: Correo''''),
    (N''''configuracion_general'''', N''''Configuración: General''''),
    (N''''configuracion_impuestos'''', N''''Configuración: Impuestos''''),
    (N''''documentos_descarga'''', N''''Documentos: Descarga''''),
    (N''''documentos_eliminar'''', N''''Documentos: Eliminar borradores''''),
    (N''''documentos_listado'''', N''''Documentos: Listado''''),
    (N''''empresa_certificados'''', N''''Mi empresa: Certificados''''),
    (N''''empresa_datos'''', N''''Mi empresa: Datos de la empresa''''),
    (N''''factura_cancelar'''', N''''Factura básica: Cancelar''''),
    (N''''factura_crear'''', N''''Factura básica: Crear''''),
    (N''''factura_eliminar'''', N''''Factura básica: Eliminar borrador''''),
    (N''''factura_enviar'''', N''''Factura básica: Enviar por correo''''),
    (N''''factura_modificar'''', N''''Factura básica: Modificar''''),
    (N''''factura_timbrar'''', N''''Factura básica: Timbrar''''),
    (N''''figuras_crear'''', N''''Figuras de transporte: Crear''''),
    (N''''figuras_desactivar'''', N''''Figuras de transporte: Desactivar y reactivar''''),
    (N''''figuras_eliminar'''', N''''Figuras de transporte: Eliminar''''),
    (N''''figuras_modificar'''', N''''Figuras de transporte: Modificar''''),
    (N''''notaria_cancelar'''', N''''Factura con Notaría: Cancelar''''),
    (N''''notaria_crear'''', N''''Factura con Notaría: Crear''''),
    (N''''notaria_eliminar'''', N''''Factura con Notaría: Eliminar borrador''''),
    (N''''notaria_enviar'''', N''''Factura con Notaría: Enviar por correo''''),
    (N''''notaria_modificar'''', N''''Factura con Notaría: Modificar''''),
    (N''''notaria_timbrar'''', N''''Factura con Notaría: Timbrar''''),
    (N''''obra_cancelar'''', N''''Factura de obra: Cancelar''''),
    (N''''obra_crear'''', N''''Factura de obra: Crear'''');
    INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''obra_eliminar'''', N''''Factura de obra: Eliminar borrador''''),
    (N''''obra_enviar'''', N''''Factura de obra: Enviar por correo''''),
    (N''''obra_modificar'''', N''''Factura de obra: Modificar''''),
    (N''''obra_timbrar'''', N''''Factura de obra: Timbrar''''),
    (N''''pago_cancelar'''', N''''Complemento de pago: Cancelar''''),
    (N''''pago_crear'''', N''''Complemento de pago: Crear''''),
    (N''''pago_eliminar'''', N''''Complemento de pago: Eliminar borrador''''),
    (N''''pago_enviar'''', N''''Complemento de pago: Enviar por correo''''),
    (N''''pago_modificar'''', N''''Complemento de pago: Modificar''''),
    (N''''pago_timbrar'''', N''''Complemento de pago: Timbrar''''),
    (N''''productos_crear'''', N''''Productos: Crear''''),
    (N''''productos_desactivar'''', N''''Productos: Desactivar y reactivar''''),
    (N''''productos_eliminar'''', N''''Productos: Eliminar''''),
    (N''''productos_modificar'''', N''''Productos: Modificar''''),
    (N''''series_crear'''', N''''Series y folios: Crear''''),
    (N''''series_desactivar'''', N''''Series y folios: Desactivar y reactivar''''),
    (N''''series_eliminar'''', N''''Series y folios: Eliminar''''),
    (N''''series_modificar'''', N''''Series y folios: Modificar''''),
    (N''''vehiculos_crear'''', N''''Vehículos: Crear''''),
    (N''''vehiculos_desactivar'''', N''''Vehículos: Desactivar y reactivar''''),
    (N''''vehiculos_eliminar'''', N''''Vehículos: Eliminar''''),
    (N''''vehiculos_modificar'''', N''''Vehículos: Modificar'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'WITH Simples (Vieja, Nueva) AS (
        SELECT * FROM (VALUES
            (''ver_documentos'', ''documentos_listado''), (''ver_documentos'', ''documentos_descarga''),
            (''emitir_factura'', ''factura_crear''), (''emitir_factura'', ''factura_modificar''),
            (''emitir_factura'', ''factura_timbrar''), (''emitir_factura'', ''factura_eliminar''),
            (''emitir_notaria'', ''notaria_crear''), (''emitir_notaria'', ''notaria_modificar''),
            (''emitir_notaria'', ''notaria_timbrar''), (''emitir_notaria'', ''notaria_eliminar''),
            (''emitir_carta_porte'', ''carta_porte_crear''), (''emitir_carta_porte'', ''carta_porte_modificar''),
            (''emitir_carta_porte'', ''carta_porte_timbrar''), (''emitir_carta_porte'', ''carta_porte_eliminar''),
            (''emitir_comercio_exterior'', ''comercio_exterior_crear''), (''emitir_comercio_exterior'', ''comercio_exterior_modificar''),
            (''emitir_comercio_exterior'', ''comercio_exterior_timbrar''), (''emitir_comercio_exterior'', ''comercio_exterior_eliminar''),
            (''emitir_obra'', ''obra_crear''), (''emitir_obra'', ''obra_modificar''),
            (''emitir_obra'', ''obra_timbrar''), (''emitir_obra'', ''obra_eliminar''),
            (''emitir_pago'', ''pago_crear''), (''emitir_pago'', ''pago_modificar''),
            (''emitir_pago'', ''pago_timbrar''), (''emitir_pago'', ''pago_eliminar''),
            (''administrar_clientes'', ''clientes_crear''), (''administrar_clientes'', ''clientes_modificar''),
            (''administrar_clientes'', ''clientes_desactivar''),
            (''administrar_productos'', ''productos_crear''), (''administrar_productos'', ''productos_modificar''),
            (''administrar_productos'', ''productos_desactivar''),
            (''administrar_vehiculos'', ''vehiculos_crear''), (''administrar_vehiculos'', ''vehiculos_modificar''),
            (''administrar_vehiculos'', ''vehiculos_desactivar''),
            (''administrar_figuras'', ''figuras_crear''), (''administrar_figuras'', ''figuras_modificar''),
            (''administrar_figuras'', ''figuras_desactivar''),
            (''mi_empresa'', ''empresa_datos''), (''mi_empresa'', ''empresa_certificados''),
            (''configuracion'', ''configuracion_general''), (''configuracion'', ''configuracion_correo''),
            (''configuracion'', ''configuracion_impuestos''),
            (''configuracion'', ''series_crear''), (''configuracion'', ''series_modificar''),
            (''configuracion'', ''series_desactivar'')) AS v (Vieja, Nueva)),
    Tipos (Tipo) AS (
        SELECT * FROM (VALUES (''factura''), (''notaria''), (''carta_porte''), (''comercio_exterior''),
                              (''obra''), (''pago'')) AS v (Tipo)),
    Emitidos AS (
        SELECT UsuarioId, EmpresaId, SUBSTRING(PermisoClave, 8, 32) AS Tipo
        FROM UsuariosEmpresasPermisos
        WHERE PermisoClave LIKE ''emitir[_]%''),
    PorTipo AS (
        SELECT p.UsuarioId, p.EmpresaId,
               t.Tipo + CASE p.PermisoClave WHEN ''cancelar'' THEN ''_cancelar'' ELSE ''_enviar'' END AS Nueva,
               p.OtorgadoUtc, p.OtorgadoPorUsuarioId
        FROM UsuariosEmpresasPermisos p
        CROSS JOIN Tipos t
        WHERE p.PermisoClave IN (''cancelar'', ''enviar_correo'')
          AND (EXISTS (SELECT 1 FROM Emitidos e
                       WHERE e.UsuarioId = p.UsuarioId AND e.EmpresaId = p.EmpresaId AND e.Tipo = t.Tipo)
               OR NOT EXISTS (SELECT 1 FROM Emitidos e
                              WHERE e.UsuarioId = p.UsuarioId AND e.EmpresaId = p.EmpresaId))),
    Todos AS (
        SELECT p.UsuarioId, p.EmpresaId, s.Nueva, p.OtorgadoUtc, p.OtorgadoPorUsuarioId
        FROM UsuariosEmpresasPermisos p
        JOIN Simples s ON s.Vieja = p.PermisoClave
        UNION ALL
        SELECT UsuarioId, EmpresaId, Nueva, OtorgadoUtc, OtorgadoPorUsuarioId FROM PorTipo),
    Nuevos AS (
        SELECT UsuarioId, EmpresaId, Nueva, OtorgadoUtc, OtorgadoPorUsuarioId,
               ROW_NUMBER() OVER (PARTITION BY UsuarioId, EmpresaId, Nueva ORDER BY OtorgadoUtc) AS Orden
        FROM Todos)
    INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT n.UsuarioId, n.EmpresaId, n.Nueva, n.OtorgadoUtc, n.OtorgadoPorUsuarioId
    FROM Nuevos n
    WHERE n.Orden = 1
      AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                      WHERE x.UsuarioId = n.UsuarioId AND x.EmpresaId = n.EmpresaId
                        AND x.PermisoClave = n.Nueva);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave IN (''administrar_clientes'', ''administrar_figuras'', ''administrar_productos'', ''administrar_vehiculos'', ''cancelar'', ''configuracion'', ''emitir_carta_porte'', ''emitir_comercio_exterior'', ''emitir_factura'', ''emitir_notaria'', ''emitir_obra'', ''emitir_pago'', ''enviar_correo'', ''mi_empresa'', ''ver_documentos'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_clientes'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_figuras'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_productos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_vehiculos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''cancelar'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''configuracion'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_carta_porte'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_comercio_exterior'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_factura'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_notaria'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_obra'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_pago'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''enviar_correo'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''mi_empresa'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''ver_documentos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027180755_20261027_A_SubpermisosPorTipo'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeEmpresas] int NOT NULL DEFAULT 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeUsuarios] int NOT NULL DEFAULT 3;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'UPDATE c SET
        LimiteDeUsuarios = CASE WHEN u.Total > 3 THEN u.Total ELSE 3 END,
        LimiteDeEmpresas = CASE WHEN e.Total > 1 THEN e.Total ELSE 1 END
    FROM Cuentas c
    CROSS APPLY (
        SELECT COUNT(*) AS Total FROM AspNetUsers us
        WHERE us.CuentaId = c.Id AND us.Activo = 1
          AND EXISTS (SELECT 1 FROM UsuariosEmpresas ue WHERE ue.UsuarioId = us.Id AND ue.Activo = 1)
    ) u
    CROSS APPLY (SELECT COUNT(*) AS Total FROM Empresas em WHERE em.CuentaId = c.Id) e;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027215128_20261027_B_LimitesPorCuenta'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027222851_20261027_C_PermisosIgualesPorEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT ue.UsuarioId, ue.EmpresaId, c.PermisoClave, SYSUTCDATETIME(), o.OtorgadoPorUsuarioId
    FROM UsuariosEmpresas ue
    JOIN AspNetUsers u ON u.Id = ue.UsuarioId AND u.EsTitular = 0
    CROSS APPLY (
        SELECT DISTINCT p.PermisoClave FROM UsuariosEmpresasPermisos p WHERE p.UsuarioId = ue.UsuarioId
    ) c
    CROSS APPLY (
        SELECT TOP 1 p.OtorgadoPorUsuarioId FROM UsuariosEmpresasPermisos p
        WHERE p.UsuarioId = ue.UsuarioId AND p.PermisoClave = c.PermisoClave
        ORDER BY p.OtorgadoUtc
    ) o
    WHERE NOT EXISTS (
        SELECT 1 FROM UsuariosEmpresasPermisos x
        WHERE x.UsuarioId = ue.UsuarioId AND x.EmpresaId = ue.EmpresaId AND x.PermisoClave = c.PermisoClave);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027222851_20261027_C_PermisosIgualesPorEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027222851_20261027_C_PermisosIgualesPorEmpresa'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [BolsasTimbres] DROP CONSTRAINT [FK_BolsasTimbres_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] DROP CONSTRAINT [FK_Paquetes_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_BolsasTimbres_EmpresaId] ON [BolsasTimbres];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Paquetes_EmpresaId_Activo_Orden] ON [Paquetes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_MovimientosTimbre_EmpresaId_MomentoUtc] ON [MovimientosTimbre];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_ComprasTimbres_EmpresaId_CreadaUtc] ON [ComprasTimbres];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[BolsasTimbres].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Paquetes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [MovimientosTimbre] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'UPDATE p SET p.CuentaId = e.CuentaId
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

    DROP TABLE #BolsasPorCuenta;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BolsasTimbres_CuentaId] ON [BolsasTimbres] ([CuentaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Paquetes_CuentaId_Activo_Orden] ON [Paquetes] ([CuentaId], [Activo], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MovimientosTimbre_CuentaId_MomentoUtc] ON [MovimientosTimbre] ([CuentaId], [MomentoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ComprasTimbres_CuentaId_CreadaUtc] ON [ComprasTimbres] ([CuentaId], [CreadaUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [BolsasTimbres] ADD CONSTRAINT [FK_BolsasTimbres_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] ADD CONSTRAINT [FK_Paquetes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ReservarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ReservarTimbre
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
            (@ReservaId, @EmpresaId, @ComprobanteId, ''reservado'', @MomentoUtc);

        INSERT INTO dbo.MovimientosTimbre
            (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
        VALUES
            (NEWID(), @CuentaId, @EmpresaId, ''reserva'', -1, 1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Reservado, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ConfirmarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ConfirmarTimbre
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
           SET r.Estado      = ''confirmado'',
               r.ResueltaUtc = @MomentoUtc
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

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
            (NEWID(), @CuentaId, @EmpresaId, ''consumo'', 0, -1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.DevolverTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.DevolverTimbre
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
           SET r.Estado           = ''devuelto'',
               r.ResueltaUtc      = @MomentoUtc,
               r.MotivoResolucion = @Motivo
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

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
            (NEWID(), @CuentaId, @EmpresaId, ''devolucion'', 1, -1,
             @Disponibles, @Reservados, @ReservaId, @Motivo, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.AcreditarCompra'', N''P'') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.AcreditarCompra
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
           SET c.Estado        = ''pagada'',
               c.AcreditadaUtc = @MomentoUtc,
               c.VenceUtc      = DATEADD(month, c.VigenciaMeses, @MomentoUtc),
               @CuentaId       = c.CuentaId,
               @EmpresaId      = c.EmpresaId,
               @Cantidad       = c.CantidadTimbres
          FROM dbo.ComprasTimbres AS c
         WHERE c.Id = @CompraId
           AND c.Estado = ''pendiente_de_pago'';

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
            (NEWID(), @CuentaId, @EmpresaId, ''compra'', @Cantidad, 0,
             @Disponibles, @Reservados, @CompraId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Acreditada, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028162231_20261028_A_TimbresPorCuenta'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] DROP CONSTRAINT [FK_Clientes_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] DROP CONSTRAINT [FK_Productos_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_ClaveInternaPorEmpresa] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_RfcPorEmpresa] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_EmpresaId_Activo_Nombre] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Productos_CodigoInternoPorEmpresa] ON [Productos];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Productos_EmpresaId_Activo_Descripcion] ON [Productos];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Clientes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Productos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[ProductosImpuestos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'SELECT c.Id, c.CuentaId AS EmpresaId INTO #ClientesOrigen FROM dbo.Clientes AS c;
    SELECT p.Id, p.CuentaId AS EmpresaId INTO #ProductosOrigen FROM dbo.Productos AS p;

    UPDATE c SET c.CuentaId = e.CuentaId
    FROM dbo.Clientes AS c JOIN dbo.Empresas AS e ON e.Id = c.CuentaId;

    UPDATE p SET p.CuentaId = e.CuentaId
    FROM dbo.Productos AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;

    UPDATE i SET i.CuentaId = p.CuentaId
    FROM dbo.ProductosImpuestos AS i JOIN dbo.Productos AS p ON p.Id = i.ProductoId;

    -- ── Fusión de clientes con el mismo RFC ─────────────────────────────────────
    -- Se queda el más antiguo. Si cualquiera de los fusionados seguía activo, el que
    -- queda también: ninguna empresa pierde de vista a un cliente con el que trabaja.
    SELECT c.Id, c.CuentaId, c.Rfc, c.ClaveInterna, o.EmpresaId,
           FIRST_VALUE(c.Id) OVER (PARTITION BY c.CuentaId, c.Rfc ORDER BY c.FechaAltaUtc, c.Id) AS Conservar,
           MAX(CAST(c.Activo AS int)) OVER (PARTITION BY c.CuentaId, c.Rfc) AS AlgunoActivo
    INTO #PorRfc
    FROM dbo.Clientes AS c JOIN #ClientesOrigen AS o ON o.Id = c.Id
    WHERE c.Rfc <> ''XAXX010101000'' AND c.Rfc <> ''XEXX010101000'';

    UPDATE x SET x.ClienteId = d.Conservar
    FROM dbo.Comprobantes AS x JOIN #PorRfc AS d ON d.Id = x.ClienteId
    WHERE d.Id <> d.Conservar;

    UPDATE t SET t.ClienteDestinoId = d.Conservar
    FROM dbo.TrasladosCartaPorte AS t JOIN #PorRfc AS d ON d.Id = t.ClienteDestinoId
    WHERE d.Id <> d.Conservar;

    UPDATE c SET c.Activo = 1
    FROM dbo.Clientes AS c JOIN #PorRfc AS d ON d.Id = c.Id
    WHERE d.Id = d.Conservar AND d.AlgunoActivo = 1 AND c.Activo = 0;

    INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
    SELECT d.EmpresaId, d.CuentaId, ''Cliente'', LOWER(CONVERT(nvarchar(36), d.Id)), ''cliente_fusionado'',
           N''{"Rfc":"'' + d.Rfc + N''","ClaveInterna":'' + CAST(d.ClaveInterna AS nvarchar(11)) +
               N'',"EmpresaId":"'' + LOWER(CONVERT(nvarchar(36), d.EmpresaId)) + N''"}'',
           N''{"FusionadoEn":"'' + LOWER(CONVERT(nvarchar(36), d.Conservar)) + N''"}'',
           SYSUTCDATETIME()
    FROM #PorRfc AS d
    WHERE d.Id <> d.Conservar;

    DELETE c FROM dbo.Clientes AS c JOIN #PorRfc AS d ON d.Id = c.Id
    WHERE d.Id <> d.Conservar;

    -- ── Claves de cliente repetidas en la cuenta ────────────────────────────────
    -- Conserva la suya el cliente de la empresa más antigua; los demás van después
    -- de la clave mayor de la cuenta.
    SELECT c.Id, c.CuentaId, c.ClaveInterna, o.EmpresaId, e.FechaAltaUtc AS EmpresaAlta,
           ROW_NUMBER() OVER (PARTITION BY c.CuentaId, c.ClaveInterna ORDER BY e.FechaAltaUtc, e.Id, c.Id) AS N
    INTO #ClavesDeClientes
    FROM dbo.Clientes AS c
    JOIN #ClientesOrigen AS o ON o.Id = c.Id
    JOIN dbo.Empresas AS e ON e.Id = o.EmpresaId;

    SELECT k.Id, k.CuentaId, k.EmpresaId, k.ClaveInterna AS Anterior,
           m.Mayor + ROW_NUMBER() OVER (PARTITION BY k.CuentaId ORDER BY k.EmpresaAlta, k.EmpresaId, k.ClaveInterna, k.Id) AS Nueva
    INTO #ClientesRenumerados
    FROM #ClavesDeClientes AS k
    JOIN (SELECT CuentaId, MAX(ClaveInterna) AS Mayor FROM dbo.Clientes GROUP BY CuentaId) AS m ON m.CuentaId = k.CuentaId
    WHERE k.N > 1;

    UPDATE c SET c.ClaveInterna = r.Nueva
    FROM dbo.Clientes AS c JOIN #ClientesRenumerados AS r ON r.Id = c.Id;

    INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
    SELECT r.EmpresaId, r.CuentaId, ''Cliente'', LOWER(CONVERT(nvarchar(36), r.Id)), ''cliente_renumerado'',
           N''{"ClaveInterna":'' + CAST(r.Anterior AS nvarchar(11)) + N''}'',
           N''{"ClaveInterna":'' + CAST(r.Nueva AS nvarchar(11)) + N''}'',
           SYSUTCDATETIME()
    FROM #ClientesRenumerados AS r;

    -- ── Códigos de producto repetidos en la cuenta ──────────────────────────────
    SELECT p.Id, p.CuentaId, p.CodigoInterno, o.EmpresaId, e.FechaAltaUtc AS EmpresaAlta,
           ROW_NUMBER() OVER (PARTITION BY p.CuentaId, p.CodigoInterno ORDER BY e.FechaAltaUtc, e.Id, p.Id) AS N
    INTO #CodigosDeProductos
    FROM dbo.Productos AS p
    JOIN #ProductosOrigen AS o ON o.Id = p.Id
    JOIN dbo.Empresas AS e ON e.Id = o.EmpresaId;

    SELECT k.Id, k.CuentaId, k.EmpresaId, k.CodigoInterno AS Anterior,
           m.Mayor + ROW_NUMBER() OVER (PARTITION BY k.CuentaId ORDER BY k.EmpresaAlta, k.EmpresaId, k.CodigoInterno, k.Id) AS Nueva
    INTO #ProductosRenumerados
    FROM #CodigosDeProductos AS k
    JOIN (SELECT CuentaId, MAX(CodigoInterno) AS Mayor FROM dbo.Productos GROUP BY CuentaId) AS m ON m.CuentaId = k.CuentaId
    WHERE k.N > 1;

    UPDATE p SET p.CodigoInterno = r.Nueva
    FROM dbo.Productos AS p JOIN #ProductosRenumerados AS r ON r.Id = p.Id;

    INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
    SELECT r.EmpresaId, r.CuentaId, ''Producto'', LOWER(CONVERT(nvarchar(36), r.Id)), ''producto_renumerado'',
           N''{"CodigoInterno":'' + CAST(r.Anterior AS nvarchar(11)) + N''}'',
           N''{"CodigoInterno":'' + CAST(r.Nueva AS nvarchar(11)) + N''}'',
           SYSUTCDATETIME()
    FROM #ProductosRenumerados AS r;

    DROP TABLE #ClientesOrigen, #ProductosOrigen, #PorRfc, #ClavesDeClientes,
               #ClientesRenumerados, #CodigosDeProductos, #ProductosRenumerados;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_ClaveInternaPorCuenta] ON [Clientes] ([CuentaId], [ClaveInterna]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Clientes_RfcPorCuenta] ON [Clientes] ([CuentaId], [Rfc]) WHERE [Rfc] <> ''''XAXX010101000'''' AND [Rfc] <> ''''XEXX010101000'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Clientes_CuentaId_Activo_Nombre] ON [Clientes] ([CuentaId], [Activo], [Nombre]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Productos_CodigoInternoPorCuenta] ON [Productos] ([CuentaId], [CodigoInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_CuentaId_Activo_Descripcion] ON [Productos] ([CuentaId], [Activo], [Descripcion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028164233_20261028_B_CatalogosPorCuenta'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [EmpresaAltaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD [EmpresaAltaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'SELECT x.Entidad, x.EntidadId, x.EmpresaId,
           ROW_NUMBER() OVER (PARTITION BY x.Entidad, x.EntidadId
                              ORDER BY CASE WHEN x.Accion LIKE ''%[_]creado'' THEN 0 ELSE 1 END, x.MomentoUtc) AS N
    INTO #Altas
    FROM dbo.Bitacora AS x
    WHERE x.Accion IN (''cliente_creado'', ''cliente_renumerado'', ''producto_creado'', ''producto_renumerado'')
      AND x.EmpresaId IS NOT NULL;

    UPDATE c SET c.EmpresaAltaId = a.EmpresaId
    FROM dbo.Clientes AS c
    JOIN #Altas AS a ON a.Entidad = ''Cliente'' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), c.Id))
    JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = c.CuentaId;

    UPDATE p SET p.EmpresaAltaId = a.EmpresaId
    FROM dbo.Productos AS p
    JOIN #Altas AS a ON a.Entidad = ''Producto'' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), p.Id))
    JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = p.CuentaId;

    UPDATE c SET c.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId)
    FROM dbo.Clientes AS c
    WHERE c.EmpresaAltaId IS NULL
      AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId) = 1;

    UPDATE p SET p.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId)
    FROM dbo.Productos AS p
    WHERE p.EmpresaAltaId IS NULL
      AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId) = 1;

    DROP TABLE #Altas;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_EmpresaAltaId] ON [Productos] ([EmpresaAltaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Clientes_EmpresaAltaId] ON [Clientes] ([EmpresaAltaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'', N''10.0.10'');');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [Detalle] nvarchar(1000) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [IdInterno] nvarchar(100) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_CuentaId_IdInterno] ON [Productos] ([CuentaId], [IdInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028213928_20261029_A_IdInternoYDetalleDeProducto'', N''10.0.10'');');
END;

COMMIT;
GO

