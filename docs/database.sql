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
GO

CREATE TABLE [AppUsers] (
    [Id] int NOT NULL IDENTITY,
    [UserName] nvarchar(256) NOT NULL,
    [DisplayName] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(512) NOT NULL,
    [PasswordSalt] nvarchar(512) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [UserName] nvarchar(256) NOT NULL,
    [IpAddress] nvarchar(64) NOT NULL,
    [Action] nvarchar(50) NOT NULL,
    [FilePath] nvarchar(1024) NOT NULL,
    [Details] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Files] (
    [Id] int NOT NULL IDENTITY,
    [FileName] nvarchar(260) NOT NULL,
    [OriginalFileName] nvarchar(260) NOT NULL,
    [Extension] nvarchar(20) NOT NULL,
    [MimeType] nvarchar(127) NOT NULL,
    [Size] bigint NOT NULL,
    [PhysicalPath] nvarchar(1024) NOT NULL,
    [RelativePath] nvarchar(1024) NOT NULL,
    [Folder] nvarchar(100) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [UpdatedDate] datetime2 NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Files] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(256) NOT NULL,
    [ClaimValue] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserClaims_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [FileVersions] (
    [Id] int NOT NULL IDENTITY,
    [FileAssetId] int NOT NULL,
    [VersionNumber] int NOT NULL,
    [PhysicalPath] nvarchar(1024) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Size] bigint NOT NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsCurrent] bit NOT NULL,
    CONSTRAINT [PK_FileVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FileVersions_Files_FileAssetId] FOREIGN KEY ([FileAssetId]) REFERENCES [Files] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_AppUsers_UserName] ON [AppUsers] ([UserName]);
GO

CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
GO

CREATE INDEX [IX_Files_Hash] ON [Files] ([Hash]);
GO

CREATE INDEX [IX_Files_IsDeleted] ON [Files] ([IsDeleted]);
GO

CREATE INDEX [IX_Files_RelativePath] ON [Files] ([RelativePath]);
GO

CREATE UNIQUE INDEX [IX_FileVersions_FileAssetId_VersionNumber] ON [FileVersions] ([FileAssetId], [VersionNumber]);
GO

CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260617110505_InitialCreate', N'8.0.11');
GO

COMMIT;
GO

