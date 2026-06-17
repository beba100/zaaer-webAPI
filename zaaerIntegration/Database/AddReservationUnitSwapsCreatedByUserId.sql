-- Master user id (central DB) who performed the unit switch from PMS or partner payload; nullable for legacy rows.
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.reservation_unit_swaps')
      AND name = N'created_by_user_id'
)
BEGIN
    ALTER TABLE [dbo].[reservation_unit_swaps]
    ADD [created_by_user_id] INT NULL;
END
GO
