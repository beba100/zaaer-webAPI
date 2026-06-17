/*
  Hall events: Hijri event date + migrate satellite reservation_id to reservations.zaaer_id.
  Run on each hall tenant DB (e.g. db55765_maali_hall).
*/

IF COL_LENGTH('dbo.reservation_event_profiles', 'event_date_hijri') IS NULL
BEGIN
    ALTER TABLE dbo.reservation_event_profiles
        ADD event_date_hijri NVARCHAR(20) NULL;
END;
GO

IF COL_LENGTH('dbo.event_function_sheets', 'event_date_hijri') IS NULL
BEGIN
    ALTER TABLE dbo.event_function_sheets
        ADD event_date_hijri NVARCHAR(20) NULL;
END;
GO

UPDATE rep
SET event_date_hijri = NULL
FROM dbo.reservation_event_profiles rep
WHERE rep.event_date_hijri IS NOT NULL AND LTRIM(RTRIM(rep.event_date_hijri)) = '';
GO

UPDATE rep
SET rep.reservation_id = r.zaaer_id
FROM dbo.reservation_event_profiles rep
INNER JOIN dbo.reservations r ON r.reservation_id = rep.reservation_id
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND rep.reservation_id <> r.zaaer_id;
GO

UPDATE efs
SET efs.reservation_id = r.zaaer_id
FROM dbo.event_function_sheets efs
INNER JOIN dbo.reservations r ON r.reservation_id = efs.reservation_id
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND efs.reservation_id <> r.zaaer_id;
GO

UPDATE hea
SET hea.reservation_id = r.zaaer_id
FROM dbo.hall_event_alerts hea
INNER JOIN dbo.reservations r ON r.reservation_id = hea.reservation_id
WHERE r.zaaer_id IS NOT NULL
  AND r.zaaer_id > 0
  AND hea.reservation_id <> r.zaaer_id;
GO

/*
  event_date_hijri is computed by the application (Um Al-Qura) on create/save.
  Existing rows without event_date_hijri are filled on the next schedule save
  or when the API reads the event (ResolveEventHijri).
*/
