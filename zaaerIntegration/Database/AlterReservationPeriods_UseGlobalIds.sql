/*
  reservation_periods — store integration ids (same convention as reservation_unit_day_rates):
    reservation_id = reservations.zaaer_id when set, else reservations.reservation_id
    unit_id        = apartments.zaaer_id when set, else apartment_id

  Drops FK constraints (zaaer_id is not a PK on reservations).
  Run on each tenant DB after CreateReservationPeriodsTable.sql.
*/

SET NOCOUNT ON;

IF OBJECT_ID('dbo.reservation_periods', 'U') IS NULL
BEGIN
    RAISERROR(N'Table dbo.reservation_periods does not exist.', 16, 1);
    RETURN;
END

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_ReservationPeriods_Reservation'
      AND parent_object_id = OBJECT_ID('dbo.reservation_periods'))
BEGIN
    ALTER TABLE dbo.reservation_periods DROP CONSTRAINT FK_ReservationPeriods_Reservation;
    PRINT N'Dropped FK_ReservationPeriods_Reservation';
END

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_ReservationPeriods_ReservationUnit'
      AND parent_object_id = OBJECT_ID('dbo.reservation_periods'))
BEGIN
    ALTER TABLE dbo.reservation_periods DROP CONSTRAINT FK_ReservationPeriods_ReservationUnit;
    PRINT N'Dropped FK_ReservationPeriods_ReservationUnit';
END

-- Backfill reservation_id → zaaer integration id
UPDATE rp
SET rp.reservation_id = r.zaaer_id
FROM dbo.reservation_periods AS rp
INNER JOIN dbo.reservations AS r ON r.reservation_id = rp.reservation_id
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND rp.reservation_id <> r.zaaer_id;

PRINT CONCAT(N'reservation_periods reservation_id backfill rows: ', @@ROWCOUNT);

-- Backfill unit_id → apartment zaaer id (via reservation_units PK)
UPDATE rp
SET rp.unit_id = COALESCE(NULLIF(a.zaaer_id, 0), a.apartment_id)
FROM dbo.reservation_periods AS rp
INNER JOIN dbo.reservation_units AS ru ON ru.unit_id = rp.unit_id
INNER JOIN dbo.apartments AS a ON a.apartment_id = ru.apartment_id
WHERE rp.unit_id IS NOT NULL
  AND COALESCE(NULLIF(a.zaaer_id, 0), a.apartment_id) <> rp.unit_id;

PRINT CONCAT(N'reservation_periods unit_id backfill rows: ', @@ROWCOUNT);
