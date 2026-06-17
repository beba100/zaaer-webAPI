SET NOCOUNT ON;

IF COL_LENGTH('dbo.resort_tickets', 'ticket_no') IS NOT NULL
BEGIN
    ALTER TABLE dbo.resort_tickets ALTER COLUMN ticket_no NVARCHAR(120) NOT NULL;
END;

IF COL_LENGTH('dbo.resort_tickets', 'qr_code') IS NOT NULL
BEGIN
    ALTER TABLE dbo.resort_tickets ALTER COLUMN qr_code NVARCHAR(256) NOT NULL;
END;

PRINT N'Resort ticket ticket_no / qr_code columns widened.';
