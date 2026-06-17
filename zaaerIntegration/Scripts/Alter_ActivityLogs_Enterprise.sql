-- Extend activity_logs for PMS enterprise activity timeline (event_key + payload_json + actor)

IF COL_LENGTH('dbo.activity_logs', 'actor_user_id') IS NULL
BEGIN
    ALTER TABLE [dbo].[activity_logs] ADD [actor_user_id] INT NULL;
    PRINT 'Added activity_logs.actor_user_id';
END
GO

IF COL_LENGTH('dbo.activity_logs', 'payload_json') IS NULL
BEGIN
    ALTER TABLE [dbo].[activity_logs] ADD [payload_json] NVARCHAR(MAX) NULL;
    PRINT 'Added activity_logs.payload_json';
END
GO

IF COL_LENGTH('dbo.activity_logs', 'icon_key') IS NULL
BEGIN
    ALTER TABLE [dbo].[activity_logs] ADD [icon_key] NVARCHAR(50) NULL;
    PRINT 'Added activity_logs.icon_key';
END
GO

IF COL_LENGTH('dbo.activity_logs', 'reservation_no') IS NULL
BEGIN
    ALTER TABLE [dbo].[activity_logs] ADD [reservation_no] NVARCHAR(50) NULL;
    PRINT 'Added activity_logs.reservation_no';
END
GO

IF COL_LENGTH('dbo.activity_logs', 'source') IS NULL
BEGIN
    ALTER TABLE [dbo].[activity_logs] ADD [source] NVARCHAR(30) NOT NULL
        CONSTRAINT [DF_activity_logs_source] DEFAULT ('pms');
    PRINT 'Added activity_logs.source';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLogs_HotelReservationCreatedAt' AND object_id = OBJECT_ID('dbo.activity_logs'))
BEGIN
    CREATE INDEX [IX_ActivityLogs_HotelReservationCreatedAt]
        ON [dbo].[activity_logs]([hotel_id], [reservation_id], [created_at] DESC)
        WHERE [reservation_id] IS NOT NULL;
    PRINT 'Created IX_ActivityLogs_HotelReservationCreatedAt';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLogs_HotelCreatedAt' AND object_id = OBJECT_ID('dbo.activity_logs'))
BEGIN
    CREATE INDEX [IX_ActivityLogs_HotelCreatedAt]
        ON [dbo].[activity_logs]([hotel_id], [created_at] DESC);
    PRINT 'Created IX_ActivityLogs_HotelCreatedAt';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLogs_HotelEventCreatedAt' AND object_id = OBJECT_ID('dbo.activity_logs'))
BEGIN
    CREATE INDEX [IX_ActivityLogs_HotelEventCreatedAt]
        ON [dbo].[activity_logs]([hotel_id], [event_key], [created_at] DESC);
    PRINT 'Created IX_ActivityLogs_HotelEventCreatedAt';
END
GO

PRINT 'Alter_ActivityLogs_Enterprise completed.';
GO
