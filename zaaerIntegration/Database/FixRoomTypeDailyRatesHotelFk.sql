-- room_type_daily_rates.hotel_id stores Zaaer hotel id (hotel_settings.zaaer_id), not hotel_settings.hotel_id PK.
-- Drop the incorrect FK if the table was created with FK_RoomTypeDailyRates_HotelSettings.

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_RoomTypeDailyRates_HotelSettings'
      AND parent_object_id = OBJECT_ID('dbo.room_type_daily_rates')
)
BEGIN
    ALTER TABLE dbo.room_type_daily_rates
        DROP CONSTRAINT FK_RoomTypeDailyRates_HotelSettings;

    PRINT 'Dropped FK_RoomTypeDailyRates_HotelSettings (hotel_id is Zaaer scope id, not local hotel_settings PK).';
END
ELSE
BEGIN
    PRINT 'FK_RoomTypeDailyRates_HotelSettings not found; nothing to drop.';
END

GO
