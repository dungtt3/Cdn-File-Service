IF OBJECT_ID(N'[CDN.__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [CDN.__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_CDN.__EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [CDN.AppUsers] (
    [Id] int NOT NULL IDENTITY,
    [UserName] nvarchar(256) NOT NULL,
    [DisplayName] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(512) NOT NULL,
    [PasswordSalt] nvarchar(512) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_CDN.AppUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [CDN.AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [UserName] nvarchar(256) NOT NULL,
    [IpAddress] nvarchar(64) NOT NULL,
    [Action] nvarchar(50) NOT NULL,
    [FilePath] nvarchar(1024) NOT NULL,
    [Details] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_CDN.AuditLogs] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [CDN.Files] (
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
    CONSTRAINT [PK_CDN.Files] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [CDN.UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(256) NOT NULL,
    [ClaimValue] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_CDN.UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CDN.UserClaims_CDN.AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [CDN.AppUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [CDN.FileVersions] (
    [Id] int NOT NULL IDENTITY,
    [FileAssetId] int NOT NULL,
    [VersionNumber] int NOT NULL,
    [PhysicalPath] nvarchar(1024) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Size] bigint NOT NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsCurrent] bit NOT NULL,
    CONSTRAINT [PK_CDN.FileVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CDN.FileVersions_CDN.Files_FileAssetId] FOREIGN KEY ([FileAssetId]) REFERENCES [CDN.Files] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_CDN.AppUsers_UserName] ON [CDN.AppUsers] ([UserName]);
GO

CREATE INDEX [IX_CDN.AuditLogs_Timestamp] ON [CDN.AuditLogs] ([Timestamp]);
GO

CREATE INDEX [IX_CDN.Files_Hash] ON [CDN.Files] ([Hash]);
GO

CREATE INDEX [IX_CDN.Files_IsDeleted] ON [CDN.Files] ([IsDeleted]);
GO

CREATE INDEX [IX_CDN.Files_RelativePath] ON [CDN.Files] ([RelativePath]);
GO

CREATE UNIQUE INDEX [IX_CDN.FileVersions_FileAssetId_VersionNumber] ON [CDN.FileVersions] ([FileAssetId], [VersionNumber]);
GO

CREATE INDEX [IX_CDN.UserClaims_UserId] ON [CDN.UserClaims] ([UserId]);
GO

INSERT INTO [CDN.__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260618100043_InitialCreate', N'8.0.11');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [CDN.Files] ADD [CompanyId] int NULL;
GO

ALTER TABLE [CDN.AppUsers] ADD [CompanyId] int NULL;
GO

CREATE INDEX [IX_CDN.Files_CompanyId] ON [CDN.Files] ([CompanyId]);
GO

CREATE INDEX [IX_CDN.AppUsers_CompanyId] ON [CDN.AppUsers] ([CompanyId]);
GO

INSERT INTO [CDN.__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260619044717_AddCompanyId', N'8.0.11');
GO

COMMIT;
GO

