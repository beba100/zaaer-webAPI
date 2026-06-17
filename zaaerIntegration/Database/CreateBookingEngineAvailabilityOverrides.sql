IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'booking_engine_availability_override' AND xtype = 'U')
BEGIN
    CREATE TABLE dbo.booking_engine_availability_override (
        override_id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        hotel_id INT NOT NULL,
        roomtype_id INT NOT NULL,
        rate_date DATE NOT NULL,
        display_units INT NOT NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_BeAvailOverride_CreatedAt DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        CONSTRAINT UX_BeAvailOverride_Hotel_RoomType_Date
            UNIQUE (hotel_id, roomtype_id, rate_date)
    );

    CREATE INDEX IX_BeAvailOverride_Hotel_Date
        ON dbo.booking_engine_availability_override (hotel_id, rate_date);

    PRINT 'booking_engine_availability_override created.';
END
ELSE
    PRINT 'booking_engine_availability_override already exists.';

GO
