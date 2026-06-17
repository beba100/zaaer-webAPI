-- =============================================================================
-- Run on: TENANT database (each resort property DB — NOT MasterDb)
-- Example: the database linked to hotel code "resort" in Master tenants table
-- =============================================================================
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.resort_ticket_config', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.resort_ticket_config (
        hotel_id INT NOT NULL PRIMARY KEY,
        issue_start_time TIME(0) NOT NULL CONSTRAINT DF_resort_ticket_config_issue_start DEFAULT ('16:00:00'),
        ticket_validity_end_time TIME(0) NOT NULL CONSTRAINT DF_resort_ticket_config_ticket_end DEFAULT ('04:00:00'),
        games_validity_end_time TIME(0) NULL,
        daily_close_time TIME(0) NOT NULL CONSTRAINT DF_resort_ticket_config_daily_close DEFAULT ('04:00:00'),
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_resort_ticket_config_created DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(3) NULL
    );
END;

PRINT N'resort_ticket_config table synced on tenant database.';
