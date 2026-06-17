-- Debug: corporate picker empty while SSMS shows rows
-- Run on the SAME database the API uses for tenant X-Hotel-Code (e.g. Jizan3).
-- Compare hotel_id from reservation vs hotel_settings vs corporate_customers.
--
-- Architecture (this codebase):
--   hotel_settings.hotel_id = internal surrogate PK (FK from reservations, corporate_customers, …).
--   hotel_settings.zaaer_id = Zaaer integration id for the property (do NOT put zaaer id into hotel_id).
--   If (2) and (3) disagree, fix data: align hotel_settings row for @HotelCode with the hotel_id that owns corporates,
--   or set zaaer_id on the correct row and keep hotel_id as the FK anchor.

DECLARE @HotelCode NVARCHAR(50) = N'Jizan3';          -- from URL / header
DECLARE @ReservationZaaerId BIGINT = 103306;          -- optional: reservation zaaer_id from URL ?id=

-- 1) All hotel rows (internal PK + integration id + code)
SELECT hotel_id, zaaer_id, hotel_code, LEN(hotel_code) AS code_len, hotel_name
FROM dbo.hotel_settings
ORDER BY hotel_id;

-- 2) Which hotel_id matches this code (case-insensitive, trimmed — same idea as API)
SELECT hotel_id, hotel_code
FROM dbo.hotel_settings
WHERE hotel_code IS NOT NULL
  AND LOWER(LTRIM(RTRIM(hotel_code))) = LOWER(LTRIM(RTRIM(@HotelCode)));

-- 3) Corporate rows per hotel_id
SELECT hotel_id, COUNT(*) AS corporate_count
FROM dbo.corporate_customers
GROUP BY hotel_id
ORDER BY hotel_id;

-- 4) Sample corporates for the hotel_id that has your data (replace 21 if needed)
SELECT TOP (50) corporate_id, hotel_id, corporate_name, cor_no, zaaer_id, is_active
FROM dbo.corporate_customers
WHERE hotel_id = 21
ORDER BY corporate_id;

-- 5) If you know reservation zaaer_id: which hotel_id does that reservation use?
SELECT r.zaaer_id, r.hotel_id, r.corporate_id, hs.hotel_code
FROM dbo.reservations r
LEFT JOIN dbo.hotel_settings hs ON hs.hotel_id = r.hotel_id
WHERE r.zaaer_id = @ReservationZaaerId;

-- EXPECTED for a healthy picker:
-- (2) hotel_id for @HotelCode should equal r.hotel_id from reservations for that property
--     AND corporate_customers.hotel_id for the same property.
-- If (2) returns 1 but (3) shows corporates only on 21, fix hotel_settings (or split mis-linked rows) — do not repurpose PK semantics.
