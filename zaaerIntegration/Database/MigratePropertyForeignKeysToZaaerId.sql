/*
  Rewrite integration FK columns to store parent zaaer_id where available.
  Run AddPmsPropertySettings.sql first (drops FK_Floors_Buildings).
*/

SET NOCOUNT ON;

-- floors.building_id: internal PK -> building zaaer_id
UPDATE f
SET f.building_id = b.zaaer_id
FROM dbo.floors f
INNER JOIN dbo.buildings b ON f.building_id = b.building_id
WHERE b.zaaer_id IS NOT NULL
  AND f.building_id = b.building_id
  AND f.building_id <> b.zaaer_id;

PRINT CONCAT('Floors building_id migrated: ', @@ROWCOUNT, ' row(s)');

-- apartments.roomtype_id
UPDATE a
SET a.roomtype_id = rt.zaaer_id
FROM dbo.apartments a
INNER JOIN dbo.room_types rt ON a.roomtype_id = rt.roomtype_id
WHERE rt.zaaer_id IS NOT NULL
  AND a.roomtype_id = rt.roomtype_id
  AND a.roomtype_id <> rt.zaaer_id;

PRINT CONCAT('Apartments roomtype_id migrated: ', @@ROWCOUNT, ' row(s)');

-- apartments.building_id
UPDATE a
SET a.building_id = b.zaaer_id
FROM dbo.apartments a
INNER JOIN dbo.buildings b ON a.building_id = b.building_id
WHERE b.zaaer_id IS NOT NULL
  AND a.building_id = b.building_id
  AND a.building_id <> b.zaaer_id;

PRINT CONCAT('Apartments building_id migrated: ', @@ROWCOUNT, ' row(s)');

-- apartments.floor_id
UPDATE a
SET a.floor_id = f.zaaer_id
FROM dbo.apartments a
INNER JOIN dbo.floors f ON a.floor_id = f.floor_id
WHERE f.zaaer_id IS NOT NULL
  AND a.floor_id = f.floor_id
  AND a.floor_id <> f.zaaer_id;

PRINT CONCAT('Apartments floor_id migrated: ', @@ROWCOUNT, ' row(s)');

-- facilities (if table exists) — building_id / floor_id
IF OBJECT_ID('dbo.facilities', 'U') IS NOT NULL
BEGIN
    UPDATE fac
    SET fac.building_id = b.zaaer_id
    FROM dbo.facilities fac
    INNER JOIN dbo.buildings b ON fac.building_id = b.building_id
    WHERE b.zaaer_id IS NOT NULL
      AND fac.building_id = b.building_id
      AND fac.building_id <> b.zaaer_id;

    UPDATE fac
    SET fac.floor_id = f.zaaer_id
    FROM dbo.facilities fac
    INNER JOIN dbo.floors f ON fac.floor_id = f.floor_id
    WHERE f.zaaer_id IS NOT NULL
      AND fac.floor_id = f.floor_id
      AND fac.floor_id <> f.zaaer_id;
END

PRINT 'MigratePropertyForeignKeysToZaaerId.sql completed.';
