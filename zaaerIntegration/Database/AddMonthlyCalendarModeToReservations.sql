-- Monthly rental calendar mode on reservations (PMS reservation detail).
-- ThirtyDay = each month is 30 days (default); Actual = calendar months.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE Name = N'monthly_calendar_mode'
      AND Object_ID = Object_ID(N'dbo.reservations')
)
BEGIN
    ALTER TABLE dbo.reservations
        ADD monthly_calendar_mode NVARCHAR(20) NULL;

    PRINT N'Column monthly_calendar_mode added to reservations.';
END
ELSE
BEGIN
    PRINT N'Column monthly_calendar_mode already exists on reservations.';
END

GO
