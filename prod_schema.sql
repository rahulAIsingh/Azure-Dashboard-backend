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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [Alerts] (
        [Id] uniqueidentifier NOT NULL,
        [AlertType] nvarchar(100) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Severity] nvarchar(50) NOT NULL,
        [ResourceGroup] nvarchar(255) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Alerts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [AzureCostUsage] (
        [Id] uniqueidentifier NOT NULL,
        [UsageDate] datetime2 NOT NULL,
        [SubscriptionName] nvarchar(255) NOT NULL,
        [ResourceGroup] nvarchar(255) NOT NULL,
        [ResourceName] nvarchar(255) NULL,
        [ResourceType] nvarchar(255) NULL,
        [ServiceName] nvarchar(255) NULL,
        [MeterCategory] nvarchar(255) NULL,
        [Location] nvarchar(100) NULL,
        [Cost] decimal(18,4) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_AzureCostUsage] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [Budgets] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceGroup] nvarchar(255) NOT NULL,
        [MonthlyBudget] decimal(18,4) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Budgets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [ProcessedFiles] (
        [Id] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ProcessedDate] datetime2 NOT NULL,
        [RecordsImported] int NOT NULL,
        CONSTRAINT [PK_ProcessedFiles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] nvarchar(100) NOT NULL,
        [SubscriptionName] nvarchar(255) NOT NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [ResourceGroups] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceGroupName] nvarchar(255) NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ResourceGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceGroups_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE TABLE [UserScopes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(50) NOT NULL,
        [ScopeValue] nvarchar(255) NOT NULL,
        CONSTRAINT [PK_UserScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserScopes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_ResourceGroup] ON [Alerts] ([ResourceGroup]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AzureCostUsage_ResourceGroup] ON [AzureCostUsage] ([ResourceGroup]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AzureCostUsage_SubscriptionName] ON [AzureCostUsage] ([SubscriptionName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AzureCostUsage_UsageDate] ON [AzureCostUsage] ([UsageDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Budgets_ResourceGroup] ON [Budgets] ([ResourceGroup]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResourceGroups_SubscriptionId] ON [ResourceGroups] ([SubscriptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Subscriptions_SubscriptionId] ON [Subscriptions] ([SubscriptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserScopes_UserId] ON [UserScopes] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308203754_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260308203754_InitialCreate', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407114424_AddUserColumns'
)
BEGIN
    ALTER TABLE [Users] ADD [Department] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407114424_AddUserColumns'
)
BEGIN
    ALTER TABLE [Users] ADD [LastActive] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407114424_AddUserColumns'
)
BEGIN
    ALTER TABLE [AzureCostUsage] ADD [ResourcePlan] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407114424_AddUserColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407114424_AddUserColumns', N'8.0.0');
END;
GO

COMMIT;
GO

