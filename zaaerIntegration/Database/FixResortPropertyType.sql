-- =============================================================================
-- Run on: TENANT database for the water park / resort property
-- Fixes: "Resort tickets are available only for resort properties."
-- Adjust the WHERE clause to match your resort hotel row.
-- =============================================================================
SET NOCOUNT ON;

UPDATE dbo.hotel_settings
SET property_type = N'resort'
WHERE property_type IS NULL
   OR LTRIM(RTRIM(property_type)) = N''
   OR LOWER(LTRIM(RTRIM(property_type))) = N'hotel';

-- Optional: target a specific hotel code only (uncomment and edit):
-- UPDATE dbo.hotel_settings
-- SET property_type = N'resort'
-- WHERE LOWER(LTRIM(RTRIM(hotel_code))) = N'resort';

PRINT N'hotel_settings.property_type set to resort where applicable.';
