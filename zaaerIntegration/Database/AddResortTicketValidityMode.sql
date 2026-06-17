-- Idempotent: scan-activated game validity + minute durations.
-- Run on tenant DBs that already have resort_ticket_types / resort_tickets.

IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.resort_ticket_types', 'validity_mode') IS NULL
        ALTER TABLE dbo.resort_ticket_types ADD validity_mode NVARCHAR(30) NOT NULL
            CONSTRAINT DF_resort_ticket_types_validity_mode DEFAULT('business_day');

    IF COL_LENGTH('dbo.resort_ticket_types', 'valid_for_minutes') IS NULL
        ALTER TABLE dbo.resort_ticket_types ADD valid_for_minutes INT NOT NULL
            CONSTRAINT DF_resort_ticket_types_valid_min DEFAULT(0);
END;
GO

IF OBJECT_ID('dbo.resort_tickets', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.resort_tickets', 'session_started_at') IS NULL
        ALTER TABLE dbo.resort_tickets ADD session_started_at DATETIME NULL;
END;
GO

-- Backfill minutes from hours
IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.resort_ticket_types', 'valid_for_minutes') IS NOT NULL
BEGIN
    UPDATE dbo.resort_ticket_types
    SET valid_for_minutes = valid_for_hours * 60
    WHERE valid_for_minutes = 0 AND valid_for_hours > 0;
END;
GO

-- Games (non-generic) start validity on first scan at the attraction
IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.resort_ticket_types', 'validity_mode') IS NOT NULL
BEGIN
    UPDATE dbo.resort_ticket_types
    SET validity_mode = 'from_first_scan'
    WHERE ticket_category = 'games'
      AND is_generic = 0
      AND validity_mode = 'business_day';
END;
GO
