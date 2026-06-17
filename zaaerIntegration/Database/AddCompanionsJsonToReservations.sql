-- DEPRECATED: companions are stored in dbo.reservation_companions.
-- Use Database/CreateReservationCompanionsTable.sql instead (creates table and drops companions_json if present).
--
-- Legacy script (kept for reference only):
-- Persists PMS companion rows (JSON array) on the reservation for save/load.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.objects t ON c.object_id = t.object_id
    WHERE t.schema_id = SCHEMA_ID('dbo')
      AND t.name = 'reservations'
      AND c.name = 'companions_json'
)
BEGIN
    ALTER TABLE [dbo].[reservations] ADD [companions_json] NVARCHAR(MAX) NULL;
END
GO
