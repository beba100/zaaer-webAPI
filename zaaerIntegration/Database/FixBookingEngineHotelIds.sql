-- Normalize booking_engine_* hotel_id to integration id (hotel_settings.zaaer_id).
-- Run once per tenant DB where rows were saved with hotel_settings.hotel_id (e.g. 1) instead of zaaer_id (e.g. 21).

DECLARE @scopeHotelId INT = (
    SELECT TOP 1 COALESCE(NULLIF(zaaer_id, 0), hotel_id)
    FROM dbo.hotel_settings
    ORDER BY hotel_id
);

IF @scopeHotelId IS NOT NULL
BEGIN
    UPDATE dbo.booking_engine_settings
    SET hotel_id = @scopeHotelId
    WHERE hotel_id <> @scopeHotelId;

    UPDATE dbo.booking_engine_media
    SET hotel_id = @scopeHotelId
    WHERE hotel_id <> @scopeHotelId;
END
GO

PRINT 'FixBookingEngineHotelIds.sql completed.';
