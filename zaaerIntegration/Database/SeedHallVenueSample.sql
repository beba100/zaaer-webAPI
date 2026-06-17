-- Seed sample hall categories (men / women) and hall units for venue properties.
-- Run on the TENANT database where hotel_settings.property_type = 'hall'.
-- Safe to re-run: skips rows that already exist.

SET NOCOUNT ON;

DECLARE @HotelId INT;

SELECT TOP 1 @HotelId = hotel_id
FROM dbo.hotel_settings
WHERE LOWER(LTRIM(RTRIM(ISNULL(property_type, '')))) = N'hall'
ORDER BY hotel_id;

IF @HotelId IS NULL
BEGIN
    RAISERROR(N'No hall property found in hotel_settings (property_type = hall).', 16, 1);
    RETURN;
END;

PRINT N'Using hotel_id = ' + CAST(@HotelId AS NVARCHAR(20));

IF NOT EXISTS (
    SELECT 1 FROM dbo.room_types
    WHERE hotel_id = @HotelId AND hall_gender_type = N'men'
)
BEGIN
    INSERT INTO dbo.room_types (
        hotel_id,
        roomtype_name,
        roomtype_name_en,
        roomtype_desc,
        hall_gender_type,
        hall_capacity,
        venue_kind,
        allow_split,
        minimum_booking_hours,
        room_count,
        sort_order,
        is_active
    )
    VALUES (
        @HotelId,
        N'قاعة رجال',
        N'Men Hall',
        N'فئة قاعات الرجال',
        N'men',
        500,
        N'indoor',
        0,
        4,
        0,
        1,
        1
    );
    PRINT N'Inserted men hall category.';
END
ELSE
    PRINT N'Men hall category already exists — skipped.';

IF NOT EXISTS (
    SELECT 1 FROM dbo.room_types
    WHERE hotel_id = @HotelId AND hall_gender_type = N'women'
)
BEGIN
    INSERT INTO dbo.room_types (
        hotel_id,
        roomtype_name,
        roomtype_name_en,
        roomtype_desc,
        hall_gender_type,
        hall_capacity,
        venue_kind,
        allow_split,
        minimum_booking_hours,
        room_count,
        sort_order,
        is_active
    )
    VALUES (
        @HotelId,
        N'قاعة نساء',
        N'Women Hall',
        N'فئة قاعات النساء',
        N'women',
        400,
        N'indoor',
        0,
        4,
        0,
        2,
        1
    );
    PRINT N'Inserted women hall category.';
END
ELSE
    PRINT N'Women hall category already exists — skipped.';

DECLARE @MenTypeId INT = (
    SELECT TOP 1 roomtype_id FROM dbo.room_types
    WHERE hotel_id = @HotelId AND hall_gender_type = N'men'
    ORDER BY roomtype_id
);
DECLARE @WomenTypeId INT = (
    SELECT TOP 1 roomtype_id FROM dbo.room_types
    WHERE hotel_id = @HotelId AND hall_gender_type = N'women'
    ORDER BY roomtype_id
);

IF @MenTypeId IS NULL OR @WomenTypeId IS NULL
BEGIN
    RAISERROR(N'Hall categories were not created. Check room_types inserts.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.apartments
    WHERE hotel_id = @HotelId AND apartment_code = N'H-M01'
)
BEGIN
    INSERT INTO dbo.apartments (
        hotel_id,
        roomtype_id,
        apartment_code,
        apartment_name,
        status,
        bathrooms_count,
        single_beds_count,
        double_beds_count,
        is_active,
        hall_preparation_status
    )
    VALUES (
        @HotelId,
        @MenTypeId,
        N'H-M01',
        N'قاعة المعالي - رجال',
        N'vacant',
        0,
        0,
        0,
        1,
        N'ready'
    );
    PRINT N'Inserted men hall unit H-M01.';
END
ELSE
    PRINT N'Hall H-M01 already exists — skipped.';

IF NOT EXISTS (
    SELECT 1 FROM dbo.apartments
    WHERE hotel_id = @HotelId AND apartment_code = N'H-W01'
)
BEGIN
    INSERT INTO dbo.apartments (
        hotel_id,
        roomtype_id,
        apartment_code,
        apartment_name,
        status,
        bathrooms_count,
        single_beds_count,
        double_beds_count,
        is_active,
        hall_preparation_status
    )
    VALUES (
        @HotelId,
        @WomenTypeId,
        N'H-W01',
        N'قاعة المعالي - نساء',
        N'vacant',
        0,
        0,
        0,
        1,
        N'ready'
    );
    PRINT N'Inserted women hall unit H-W01.';
END
ELSE
    PRINT N'Hall H-W01 already exists — skipped.';

SELECT
    rt.roomtype_id,
    rt.roomtype_name,
    rt.hall_gender_type,
    rt.hall_capacity,
    a.apartment_id,
    a.apartment_code,
    a.apartment_name,
    a.is_active
FROM dbo.room_types rt
LEFT JOIN dbo.apartments a ON a.roomtype_id = rt.roomtype_id AND a.hotel_id = rt.hotel_id
WHERE rt.hotel_id = @HotelId
  AND rt.hall_gender_type IN (N'men', N'women')
ORDER BY rt.sort_order, a.apartment_code;

PRINT N'Hall venue seed complete.';
