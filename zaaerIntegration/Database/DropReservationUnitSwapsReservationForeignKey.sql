/*
  reservation_unit_swaps.reservation_id stores the integration id (reservations.zaaer_id) when set,
  not necessarily reservations.reservation_id (PK). Drop FK to reservations if it enforces PK-only values.
*/
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.reservation_unit_swaps')
      AND name = N'FK_RUS_Reservation'
)
BEGIN
    ALTER TABLE [dbo].[reservation_unit_swaps] DROP CONSTRAINT [FK_RUS_Reservation];
    PRINT 'Dropped FK_RUS_Reservation on reservation_unit_swaps';
END

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.reservation_unit_swaps')
      AND name = N'FK_RUSwitch_Reservation'
)
BEGIN
    ALTER TABLE [dbo].[reservation_unit_swaps] DROP CONSTRAINT [FK_RUSwitch_Reservation];
    PRINT 'Dropped FK_RUSwitch_Reservation on reservation_unit_swaps';
END
GO
