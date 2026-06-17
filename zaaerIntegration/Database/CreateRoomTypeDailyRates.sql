-- Per-day room type rate overrides for PMS rates calendar.
-- hotel_id  = hotel_settings.zaaer_id (Zaaer hotel scope id)
-- roomtype_id = room_types.zaaer_id (Zaaer room type id), not room_types.roomtype_id PK
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'room_type_daily_rates' AND xtype = 'U')
BEGIN
    CREATE TABLE dbo.room_type_daily_rates (
        daily_rate_id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        hotel_id INT NOT NULL,
        roomtype_id INT NOT NULL,
        rate_date DATE NOT NULL,
        gross_rate DECIMAL(12,2) NOT NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_RoomTypeDailyRates_CreatedAt DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        CONSTRAINT UX_RoomTypeDailyRates_Hotel_RoomType_Date
            UNIQUE (hotel_id, roomtype_id, rate_date)
    );

    CREATE INDEX IX_RoomTypeDailyRates_Hotel_Date
        ON dbo.room_type_daily_rates (hotel_id, rate_date);

    PRINT 'room_type_daily_rates table created.';
END
ELSE
BEGIN
    PRINT 'room_type_daily_rates table already exists.';
END

GO
