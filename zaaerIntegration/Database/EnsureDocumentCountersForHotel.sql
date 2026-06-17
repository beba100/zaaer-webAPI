/*
    Ensure every active DocumentTypes row exists in DocumentCounters for one hotel.

    Where to get the three IDs (run on Master DB + tenant DB):

    ┌─────────────────┬──────────────────────────────────────────────────────────────┐
    │ Column          │ Source                                                       │
    ├─────────────────┼──────────────────────────────────────────────────────────────┤
    │ tenant_id       │ Master.dbo.Tenants.Id  (row for this hotel / X-Hotel-Code)   │
    │ hotel_zaaer_id  │ Master.dbo.Tenants.ZaaerId  = tenant hotel_settings.zaaer_id │
    │ local_hotel_id  │ Tenant DB hotel_settings.hotel_id (PK, often 1 per property)  │
    └─────────────────┴──────────────────────────────────────────────────────────────┘

    Quick verify (Master):
        SELECT Id AS tenant_id, Code, ZaaerId AS hotel_zaaer_id, DatabaseName
        FROM dbo.Tenants
        WHERE ZaaerId = 16;   -- or Code = N'YourHotelCode'

    Quick verify (tenant DB):
        SELECT hotel_id AS local_hotel_id, zaaer_id AS hotel_zaaer_id, hotel_code
        FROM dbo.hotel_settings;

    Preferred: re-seed from tenant data (sets current_value from MAX in tenant DB):
        EXEC dbo.SeedCentralNumberingForTenant
            @TenantId = 1,
            @TenantDatabase = N'YourTenantDbName';

    Manual backfill missing rows only (example hotel_zaaer_id = 16):
        Run section B below after setting @TenantId / @HotelZaaerId / @LocalHotelId.
*/

SET NOCOUNT ON;

-- ========== A) Lookup helper (edit filter) ==========
SELECT
    t.Id AS tenant_id,
    t.Code AS hotel_code,
    t.ZaaerId AS hotel_zaaer_id,
    t.DatabaseName AS tenant_database
FROM dbo.Tenants AS t
WHERE t.ZaaerId = 16;   -- change to your hotel

-- ========== B) Backfill missing doc_code rows (edit variables) ==========
DECLARE
    @TenantId INT = 1,
    @HotelZaaerId INT = 16,
    @LocalHotelId INT = 1;

DECLARE @DocCode NVARCHAR(50);

DECLARE missing CURSOR LOCAL FAST_FORWARD FOR
    SELECT dt.doc_code
    FROM dbo.DocumentTypes AS dt
    WHERE dt.is_active = 1
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.DocumentCounters AS c
          WHERE c.hotel_zaaer_id = @HotelZaaerId
            AND c.doc_code = dt.doc_code);

OPEN missing;
FETCH NEXT FROM missing INTO @DocCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC dbo.SeedDocumentCounter
        @HotelZaaerId = @HotelZaaerId,
        @DocCode = @DocCode,
        @CurrentValue = 0,
        @TenantId = @TenantId,
        @LocalHotelId = @LocalHotelId;

    PRINT CONCAT(N'Inserted counter: hotel_zaaer_id=', @HotelZaaerId, N', doc_code=', @DocCode);

    FETCH NEXT FROM missing INTO @DocCode;
END;

CLOSE missing;
DEALLOCATE missing;

-- ========== C) Audit: expected 10 rows for hotel-scoped types ==========
SELECT c.*
FROM dbo.DocumentCounters AS c
WHERE c.hotel_zaaer_id = @HotelZaaerId
ORDER BY c.doc_code;

SELECT dt.doc_code AS missing_in_counters
FROM dbo.DocumentTypes AS dt
WHERE dt.is_active = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.DocumentCounters AS c
      WHERE c.hotel_zaaer_id = @HotelZaaerId
        AND c.doc_code = dt.doc_code);
