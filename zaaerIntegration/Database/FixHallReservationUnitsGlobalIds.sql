/*
  Hall events — align reservation_units / reservation_periods / reservation_extras
  with hotel integration storage (global zaaer ids):

    reservation_id = reservations.zaaer_id when set, else reservations.reservation_id
    apartment_id   = apartments.zaaer_id when set, else apartments.apartment_id
    unit_id        = apartments.zaaer_id when set, else apartments.apartment_id (reservation_periods)

  Targets rows linked to reservation_event_profiles only.
  Run on each tenant DB after deploying PmsHallEventService global-id writes.
*/

SET NOCOUNT ON;

IF OBJECT_ID('dbo.reservation_event_profiles', 'U') IS NULL
BEGIN
    PRINT N'Skipped: reservation_event_profiles not found.';
    RETURN;
END

-- reservation_units.reservation_id → zaaer integration id
UPDATE ru
SET ru.reservation_id = COALESCE(NULLIF(r.zaaer_id, 0), r.reservation_id)
FROM dbo.reservation_units AS ru
INNER JOIN dbo.reservations AS r ON r.reservation_id = ru.reservation_id
INNER JOIN dbo.reservation_event_profiles AS rep ON rep.reservation_id IN (r.reservation_id, r.zaaer_id)
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND ru.reservation_id <> r.zaaer_id;

PRINT CONCAT(N'reservation_units reservation_id backfill rows: ', @@ROWCOUNT);

-- reservation_units.apartment_id → apartment zaaer id
UPDATE ru
SET ru.apartment_id = COALESCE(NULLIF(a.zaaer_id, 0), a.apartment_id)
FROM dbo.reservation_units AS ru
INNER JOIN dbo.apartments AS a ON a.apartment_id = ru.apartment_id
INNER JOIN dbo.reservations AS r ON r.reservation_id = ru.reservation_id
    OR (r.zaaer_id IS NOT NULL AND r.zaaer_id = ru.reservation_id)
INNER JOIN dbo.reservation_event_profiles AS rep ON rep.reservation_id IN (r.reservation_id, r.zaaer_id)
WHERE a.zaaer_id IS NOT NULL
  AND a.zaaer_id > 0
  AND ru.apartment_id <> a.zaaer_id;

PRINT CONCAT(N'reservation_units apartment_id backfill rows: ', @@ROWCOUNT);

-- reservation_periods for hall events
IF OBJECT_ID('dbo.reservation_periods', 'U') IS NOT NULL
BEGIN
    UPDATE rp
    SET rp.reservation_id = COALESCE(NULLIF(r.zaaer_id, 0), r.reservation_id)
    FROM dbo.reservation_periods AS rp
    INNER JOIN dbo.reservations AS r ON r.reservation_id = rp.reservation_id
        OR (r.zaaer_id IS NOT NULL AND r.zaaer_id = rp.reservation_id)
    INNER JOIN dbo.reservation_event_profiles AS rep ON rep.reservation_id IN (r.reservation_id, r.zaaer_id)
    WHERE r.zaaer_id IS NOT NULL
      AND r.zaaer_id > 0
      AND rp.reservation_id <> r.zaaer_id;

    PRINT CONCAT(N'reservation_periods reservation_id backfill rows: ', @@ROWCOUNT);

    UPDATE rp
    SET rp.unit_id = COALESCE(NULLIF(a.zaaer_id, 0), a.apartment_id)
    FROM dbo.reservation_periods AS rp
    INNER JOIN dbo.reservation_units AS ru ON ru.unit_id = rp.unit_id
    INNER JOIN dbo.apartments AS a ON a.apartment_id = ru.apartment_id
        OR (a.zaaer_id IS NOT NULL AND a.zaaer_id = ru.apartment_id)
    INNER JOIN dbo.reservations AS r ON r.reservation_id = ru.reservation_id
        OR (r.zaaer_id IS NOT NULL AND r.zaaer_id = ru.reservation_id)
    INNER JOIN dbo.reservation_event_profiles AS rep ON rep.reservation_id IN (r.reservation_id, r.zaaer_id)
    WHERE rp.unit_id IS NOT NULL
      AND COALESCE(NULLIF(a.zaaer_id, 0), a.apartment_id) <> rp.unit_id;

    PRINT CONCAT(N'reservation_periods unit_id backfill rows: ', @@ROWCOUNT);
END

-- reservation_extras for hall events
IF OBJECT_ID('dbo.reservation_extras', 'U') IS NOT NULL
BEGIN
    UPDATE re
    SET re.reservation_id = COALESCE(NULLIF(r.zaaer_id, 0), r.reservation_id)
    FROM dbo.reservation_extras AS re
    INNER JOIN dbo.reservations AS r ON r.reservation_id = re.reservation_id
    INNER JOIN dbo.reservation_event_profiles AS rep ON rep.reservation_id IN (r.reservation_id, r.zaaer_id)
    WHERE r.zaaer_id IS NOT NULL
      AND r.zaaer_id > 0
      AND re.reservation_id <> r.zaaer_id;

    PRINT CONCAT(N'reservation_extras reservation_id backfill rows: ', @@ROWCOUNT);
END
