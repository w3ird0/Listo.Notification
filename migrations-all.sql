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
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLog] (
        [AuditId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(450) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NULL,
        [ServiceOrigin] nvarchar(max) NULL,
        [ActorType] nvarchar(max) NOT NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [OccurredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLog] PRIMARY KEY ([AuditId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Conversations] (
        [ConversationId] uniqueidentifier NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [ParticipantsJson] nvarchar(max) NOT NULL,
        [ServiceOrigin] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([ConversationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [CostTracking] (
        [CostId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ServiceOrigin] nvarchar(450) NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [Provider] nvarchar(max) NOT NULL,
        [UnitCostMicros] bigint NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [MessageId] uniqueidentifier NULL,
        [UsageUnits] int NOT NULL,
        [TotalCostMicros] bigint NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CostTracking] PRIMARY KEY ([CostId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Devices] (
        [DeviceId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [DeviceToken] nvarchar(450) NOT NULL,
        [Platform] nvarchar(max) NOT NULL,
        [DeviceInfo] nvarchar(max) NULL,
        [LastSeen] datetime2 NOT NULL,
        [Active] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([DeviceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [NotificationQueue] (
        [QueueId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(max) NULL,
        [ServiceOrigin] nvarchar(max) NOT NULL,
        [Channel] nvarchar(max) NOT NULL,
        [TemplateKey] nvarchar(max) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [EncryptedEmail] nvarchar(max) NULL,
        [EncryptedPhoneNumber] nvarchar(max) NULL,
        [EncryptedFirebaseToken] nvarchar(max) NULL,
        [EmailHash] nvarchar(450) NULL,
        [PhoneHash] nvarchar(max) NULL,
        [PreferredLocale] nvarchar(max) NOT NULL,
        [ScheduledAt] datetime2 NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAt] datetime2 NULL,
        [LastErrorCode] nvarchar(max) NULL,
        [LastErrorMessage] nvarchar(max) NULL,
        [CorrelationId] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationQueue] PRIMARY KEY ([QueueId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ServiceOrigin] nvarchar(450) NOT NULL,
        [Channel] nvarchar(max) NOT NULL,
        [TemplateKey] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [Recipient] nvarchar(max) NOT NULL,
        [Subject] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Metadata] nvarchar(max) NULL,
        [ScheduledFor] datetime2 NULL,
        [ScheduledAt] datetime2 NULL,
        [SentAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        [ReadAt] datetime2 NULL,
        [ProviderMessageId] nvarchar(max) NULL,
        [ErrorCode] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [CorrelationId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Preferences] (
        [PreferenceId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [QuietHours] nvarchar(max) NULL,
        [Topics] nvarchar(max) NULL,
        [Locale] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Preferences] PRIMARY KEY ([PreferenceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Templates] (
        [TemplateId] uniqueidentifier NOT NULL,
        [TemplateKey] nvarchar(450) NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [Locale] nvarchar(450) NOT NULL,
        [Subject] nvarchar(max) NULL,
        [Body] nvarchar(max) NOT NULL,
        [Variables] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([TemplateId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [MessageId] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [SenderUserId] nvarchar(max) NOT NULL,
        [RecipientUserId] nvarchar(450) NULL,
        [Body] nvarchar(max) NOT NULL,
        [AttachmentsJson] nvarchar(max) NULL,
        [Status] nvarchar(450) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [ReadAt] datetime2 NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([MessageId]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([ConversationId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_EntityType_EntityId_OccurredAt] ON [AuditLog] ([EntityType], [EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_OccurredAt] ON [AuditLog] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AuditLog_UserId_OccurredAt] ON [AuditLog] ([UserId], [OccurredAt]) WHERE UserId IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_LastMessageAt] ON [Conversations] ([LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CostTracking_ServiceOrigin_OccurredAt] ON [CostTracking] ([ServiceOrigin], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CostTracking_TenantId] ON [CostTracking] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CostTracking_TenantId_Channel_OccurredAt] ON [CostTracking] ([TenantId], [Channel], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CostTracking_TenantId_ServiceOrigin_OccurredAt] ON [CostTracking] ([TenantId], [ServiceOrigin], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_LastSeen] ON [Devices] ([LastSeen]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_UserId_Active] ON [Devices] ([UserId], [Active]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Devices_DeviceToken] ON [Devices] ([DeviceToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_SentAt] ON [Messages] ([ConversationId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Messages_RecipientUserId_Status] ON [Messages] ([RecipientUserId], [Status]) WHERE RecipientUserId IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NotificationQueue_CreatedAt] ON [NotificationQueue] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_NotificationQueue_EmailHash] ON [NotificationQueue] ([EmailHash]) WHERE EmailHash IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_NotificationQueue_ScheduledAt_Attempts] ON [NotificationQueue] ([ScheduledAt], [Attempts]) WHERE NextAttemptAt IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_CorrelationId] ON [Notifications] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenantId] ON [Notifications] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenantId_ServiceOrigin_CreatedAt] ON [Notifications] ([TenantId], [ServiceOrigin], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenantId_UserId_CreatedAt] ON [Notifications] ([TenantId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Preferences_TenantId] ON [Preferences] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Preferences_TenantId_UserId] ON [Preferences] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Preferences_TenantId_UserId_Channel] ON [Preferences] ([TenantId], [UserId], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Templates_TemplateKey_Channel_Locale] ON [Templates] ([TemplateKey], [Channel], [Locale]) WHERE IsActive = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Templates_TemplateKey_Channel_Locale_Version] ON [Templates] ([TemplateKey], [Channel], [Locale], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020135858_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251020135858_InitialCreate', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [Notifications] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [Notifications] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [NotificationQueue] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [NotificationQueue] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [Messages] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [Messages] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [AuditLog] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    ALTER TABLE [AuditLog] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    CREATE TABLE [RateLimiting] (
        [ConfigId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ServiceOrigin] nvarchar(450) NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [PerUserWindowSeconds] int NOT NULL,
        [PerUserMax] int NOT NULL,
        [PerUserMaxCap] int NOT NULL,
        [PerServiceWindowSeconds] int NOT NULL,
        [PerServiceMax] int NOT NULL,
        [PerServiceMaxCap] int NOT NULL,
        [BurstSize] int NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_RateLimiting] PRIMARY KEY ([ConfigId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    CREATE TABLE [RetryPolicy] (
        [PolicyId] uniqueidentifier NOT NULL,
        [ServiceOrigin] nvarchar(450) NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [MaxAttempts] int NOT NULL,
        [BaseDelaySeconds] int NOT NULL,
        [BackoffFactor] decimal(18,2) NOT NULL,
        [JitterMs] int NOT NULL,
        [TimeoutSeconds] int NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_RetryPolicy] PRIMARY KEY ([PolicyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RateLimiting_TenantId_ServiceOrigin_Channel] ON [RateLimiting] ([TenantId], [ServiceOrigin], [Channel]) WHERE [TenantId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RetryPolicy_ServiceOrigin_Channel] ON [RetryPolicy] ([ServiceOrigin], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251020184830_PostInitialModelUpdates_20251020'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251020184830_PostInitialModelUpdates_20251020', N'9.0.0');
END;

COMMIT;
GO

