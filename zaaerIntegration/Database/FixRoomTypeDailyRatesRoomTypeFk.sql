-- room_type_daily_rates.roomtype_id stores Zaaer room type id (room_types.zaaer_id), not room_types.roomtype_id PK.
-- Drop the incorrect FK if the table was created with FK_RoomTypeDailyRates_RoomTypes.

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_RoomTypeDailyRates_RoomTypes'
      AND parent_object_id = OBJECT_ID('dbo.room_type_daily_rates')
)
BEGIN
    ALTER TABLE dbo.room_type_daily_rates
        DROP CONSTRAINT FK_RoomTypeDailyRates_RoomTypes;

    PRINT 'Dropped FK_RoomTypeDailyRates_RoomTypes (roomtype_id is Zaaer scope id, not internal room_types PK).';
END
ELSE
BEGIN
    PRINT 'FK_RoomTypeDailyRates_RoomTypes not found; nothing to drop.';
END

GO
