/*
  Read-only diagnostics for room/unit duplication in tenant Madinah3.

  Step 1: Run the Master DB query to confirm the tenant database name.
  Step 2: Switch to that tenant database and run the tenant diagnostics.

  This script does not update or delete data.
*/

/* Step 1 - Master DB */
USE [db32357_MasterDB];
GO

SELECT
    [Id],
    [Code],
    [Name],
    [ZaaerId],
    [DatabaseName]
FROM [dbo].[Tenants]
WHERE [Code] = N'Madinah3';
GO

/*
  Step 2 - Tenant DB
  Replace the database name below with the DatabaseName returned above.
*/
-- USE [PUT_MADINAH3_DATABASE_NAME_HERE];
-- GO

/* 2.1 - Hotel identity inside the tenant DB */
SELECT
    [hotel_id],
    [hotel_code],
    [hotel_name],
    [zaaer_id]
FROM [dbo].[hotel_settings];
GO

/* 2.2 - Does the old OR-join expand one apartment into multiple room-board rows? */
SELECT
    COUNT_BIG(*) AS raw_join_rows,
    COUNT_BIG(DISTINCT COALESCE(a.[zaaer_id], a.[apartment_id])) AS distinct_board_rooms,
    COUNT_BIG(*) - COUNT_BIG(DISTINCT COALESCE(a.[zaaer_id], a.[apartment_id])) AS extra_rows_from_lookup_joins
FROM [dbo].[apartments] a
LEFT JOIN [dbo].[buildings] b
    ON a.[building_id] = b.[building_id]
    OR a.[building_id] = b.[zaaer_id]
LEFT JOIN [dbo].[floors] f
    ON a.[floor_id] = f.[floor_id]
    OR a.[floor_id] = f.[zaaer_id]
LEFT JOIN [dbo].[room_types] rt
    ON a.[roomtype_id] = rt.[roomtype_id]
    OR a.[roomtype_id] = rt.[zaaer_id];
GO

/* 2.3 - Exact apartments affected by ambiguous Building/Floor/RoomType matches */
SELECT TOP (200)
    a.[apartment_id],
    a.[zaaer_id] AS apartment_zaaer_id,
    a.[apartment_code],
    a.[apartment_name],
    a.[building_id],
    COUNT(DISTINCT b.[building_id]) AS building_matches,
    a.[floor_id],
    COUNT(DISTINCT f.[floor_id]) AS floor_matches,
    a.[roomtype_id],
    COUNT(DISTINCT rt.[roomtype_id]) AS room_type_matches
FROM [dbo].[apartments] a
LEFT JOIN [dbo].[buildings] b
    ON a.[building_id] = b.[building_id]
    OR a.[building_id] = b.[zaaer_id]
LEFT JOIN [dbo].[floors] f
    ON a.[floor_id] = f.[floor_id]
    OR a.[floor_id] = f.[zaaer_id]
LEFT JOIN [dbo].[room_types] rt
    ON a.[roomtype_id] = rt.[roomtype_id]
    OR a.[roomtype_id] = rt.[zaaer_id]
GROUP BY
    a.[apartment_id],
    a.[zaaer_id],
    a.[apartment_code],
    a.[apartment_name],
    a.[building_id],
    a.[floor_id],
    a.[roomtype_id]
HAVING
    COUNT(DISTINCT b.[building_id]) > 1
    OR COUNT(DISTINCT f.[floor_id]) > 1
    OR COUNT(DISTINCT rt.[roomtype_id]) > 1
ORDER BY a.[apartment_code];
GO

/* 2.4 - Which lookup table has an internal-id / zaaer-id collision? */
SELECT
    N'buildings' AS table_name,
    b1.[building_id] AS internal_row_id,
    b1.[zaaer_id] AS internal_row_zaaer_id,
    b1.[building_name] AS internal_row_name,
    b2.[building_id] AS zaaer_match_row_id,
    b2.[zaaer_id] AS zaaer_match_row_zaaer_id,
    b2.[building_name] AS zaaer_match_row_name,
    b1.[building_id] AS collided_value
FROM [dbo].[buildings] b1
JOIN [dbo].[buildings] b2
    ON b1.[building_id] = b2.[zaaer_id]
WHERE b1.[building_id] <> b2.[building_id]

UNION ALL

SELECT
    N'floors' AS table_name,
    f1.[floor_id] AS internal_row_id,
    f1.[zaaer_id] AS internal_row_zaaer_id,
    f1.[floor_name] AS internal_row_name,
    f2.[floor_id] AS zaaer_match_row_id,
    f2.[zaaer_id] AS zaaer_match_row_zaaer_id,
    f2.[floor_name] AS zaaer_match_row_name,
    f1.[floor_id] AS collided_value
FROM [dbo].[floors] f1
JOIN [dbo].[floors] f2
    ON f1.[floor_id] = f2.[zaaer_id]
WHERE f1.[floor_id] <> f2.[floor_id]

UNION ALL

SELECT
    N'room_types' AS table_name,
    rt1.[roomtype_id] AS internal_row_id,
    rt1.[zaaer_id] AS internal_row_zaaer_id,
    rt1.[roomtype_name] AS internal_row_name,
    rt2.[roomtype_id] AS zaaer_match_row_id,
    rt2.[zaaer_id] AS zaaer_match_row_zaaer_id,
    rt2.[roomtype_name] AS zaaer_match_row_name,
    rt1.[roomtype_id] AS collided_value
FROM [dbo].[room_types] rt1
JOIN [dbo].[room_types] rt2
    ON rt1.[roomtype_id] = rt2.[zaaer_id]
WHERE rt1.[roomtype_id] <> rt2.[roomtype_id]
ORDER BY table_name, collided_value;
GO

/* 2.5 - Does reservation_units.apartment_id match two different apartments? */
SELECT TOP (200)
    ru.[unit_id],
    ru.[zaaer_id] AS unit_zaaer_id,
    ru.[reservation_id],
    ru.[apartment_id] AS stored_apartment_id,
    a_internal.[apartment_id] AS internal_match_apartment_id,
    a_internal.[zaaer_id] AS internal_match_zaaer_id,
    a_internal.[apartment_code] AS internal_match_code,
    a_zaaer.[apartment_id] AS zaaer_match_apartment_id,
    a_zaaer.[zaaer_id] AS zaaer_match_zaaer_id,
    a_zaaer.[apartment_code] AS zaaer_match_code,
    ru.[check_in_date],
    ru.[check_out_date],
    ru.[status]
FROM [dbo].[reservation_units] ru
LEFT JOIN [dbo].[apartments] a_internal
    ON ru.[apartment_id] = a_internal.[apartment_id]
LEFT JOIN [dbo].[apartments] a_zaaer
    ON ru.[apartment_id] = a_zaaer.[zaaer_id]
WHERE
    a_internal.[apartment_id] IS NOT NULL
    AND a_zaaer.[apartment_id] IS NOT NULL
    AND a_internal.[apartment_id] <> a_zaaer.[apartment_id]
ORDER BY ru.[unit_id] DESC;
GO

/* 2.6 - Does reservation_units.reservation_id match two different reservations? */
SELECT TOP (200)
    ru.[unit_id],
    ru.[reservation_id] AS stored_reservation_id,
    r_internal.[reservation_id] AS internal_match_reservation_id,
    r_internal.[zaaer_id] AS internal_match_zaaer_id,
    r_internal.[reservation_no] AS internal_match_no,
    r_zaaer.[reservation_id] AS zaaer_match_reservation_id,
    r_zaaer.[zaaer_id] AS zaaer_match_zaaer_id,
    r_zaaer.[reservation_no] AS zaaer_match_no
FROM [dbo].[reservation_units] ru
LEFT JOIN [dbo].[reservations] r_internal
    ON ru.[reservation_id] = r_internal.[reservation_id]
LEFT JOIN [dbo].[reservations] r_zaaer
    ON ru.[reservation_id] = r_zaaer.[zaaer_id]
WHERE
    r_internal.[reservation_id] IS NOT NULL
    AND r_zaaer.[reservation_id] IS NOT NULL
    AND r_internal.[reservation_id] <> r_zaaer.[reservation_id]
ORDER BY ru.[unit_id] DESC;
GO

/* 2.7 - Physical duplicate unit rows: same reservation, room, dates. */
SELECT TOP (200)
    [reservation_id],
    [apartment_id],
    [check_in_date],
    [check_out_date],
    COUNT(*) AS duplicate_count,
    STRING_AGG(CONVERT(varchar(20), [unit_id]), ',') AS unit_ids
FROM [dbo].[reservation_units]
GROUP BY
    [reservation_id],
    [apartment_id],
    [check_in_date],
    [check_out_date]
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC, [reservation_id] DESC;
GO

/* 2.8 - Non-unique zaaer_id values that make fallback matching unsafe. */
SELECT N'apartments' AS table_name, [zaaer_id], COUNT(*) AS row_count
FROM [dbo].[apartments]
WHERE [zaaer_id] IS NOT NULL
GROUP BY [zaaer_id]
HAVING COUNT(*) > 1

UNION ALL

SELECT N'buildings' AS table_name, [zaaer_id], COUNT(*) AS row_count
FROM [dbo].[buildings]
WHERE [zaaer_id] IS NOT NULL
GROUP BY [zaaer_id]
HAVING COUNT(*) > 1

UNION ALL

SELECT N'floors' AS table_name, [zaaer_id], COUNT(*) AS row_count
FROM [dbo].[floors]
WHERE [zaaer_id] IS NOT NULL
GROUP BY [zaaer_id]
HAVING COUNT(*) > 1

UNION ALL

SELECT N'room_types' AS table_name, [zaaer_id], COUNT(*) AS row_count
FROM [dbo].[room_types]
WHERE [zaaer_id] IS NOT NULL
GROUP BY [zaaer_id]
HAVING COUNT(*) > 1

UNION ALL

SELECT N'reservations' AS table_name, [zaaer_id], COUNT(*) AS row_count
FROM [dbo].[reservations]
WHERE [zaaer_id] IS NOT NULL
GROUP BY [zaaer_id]
HAVING COUNT(*) > 1
ORDER BY table_name, row_count DESC;
GO
