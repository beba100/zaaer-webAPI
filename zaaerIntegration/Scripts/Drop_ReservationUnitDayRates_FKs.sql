-- reservation_unit_day_rates.reservation_id / unit_id store global (Zaaer) keys when set.
-- Drop FK constraints so rows can reference zaaer_id values (e.g. reservation 22081) instead of internal PK only.

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_RUDR_Reservation'
      AND parent_object_id = OBJECT_ID(N'dbo.reservation_unit_day_rates')
)
BEGIN
    ALTER TABLE [dbo].[reservation_unit_day_rates] DROP CONSTRAINT [FK_RUDR_Reservation];
    PRINT 'Dropped FK_RUDR_Reservation';
END;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_RUDR_ReservationUnit'
      AND parent_object_id = OBJECT_ID(N'dbo.reservation_unit_day_rates')
)
BEGIN
    ALTER TABLE [dbo].[reservation_unit_day_rates] DROP CONSTRAINT [FK_RUDR_ReservationUnit];
    PRINT 'Dropped FK_RUDR_ReservationUnit';
END;

GO

-- Optional: rewrite existing rows to global reservation_id (zaaer_id) and unit/apartment zaaer_id.
UPDATE rudr
SET rudr.reservation_id = r.zaaer_id
FROM dbo.reservation_unit_day_rates AS rudr
INNER JOIN dbo.reservations AS r
    ON r.reservation_id = rudr.reservation_id
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND rudr.reservation_id = r.reservation_id
  AND rudr.reservation_id <> r.zaaer_id;

-- unit_id = global apartment key (apartments.zaaer_id, else apartments.apartment_id) via reservation_units.apartment_id
UPDATE rudr
SET rudr.unit_id = COALESCE(apt.zaaer_id, apt.apartment_id)
FROM dbo.reservation_unit_day_rates AS rudr
INNER JOIN dbo.reservation_units AS ru
    ON ru.unit_id = rudr.unit_id
    OR ru.apartment_id = rudr.unit_id
    OR (ru.zaaer_id IS NOT NULL AND ru.zaaer_id = rudr.unit_id)
INNER JOIN dbo.reservations AS r
    ON r.reservation_id = ru.reservation_id
    OR (r.zaaer_id IS NOT NULL AND r.zaaer_id = ru.reservation_id)
INNER JOIN dbo.apartments AS apt
    ON apt.hotel_id = r.hotel_id
   AND (apt.apartment_id = ru.apartment_id OR apt.zaaer_id = ru.apartment_id)
WHERE COALESCE(apt.zaaer_id, apt.apartment_id) IS NOT NULL
  AND rudr.unit_id <> COALESCE(apt.zaaer_id, apt.apartment_id);

PRINT 'Reservation unit day rates global id migration complete.';
