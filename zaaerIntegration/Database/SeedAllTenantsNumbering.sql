/*

    Seed central numbering for every tenant registered in Master DB.



    Run on Master DB after:

    - CreateCentralNumbering.sql

    - HardenCentralNumbering.sql (recommended)

    - PerEntityZaaerCounters.sql

    - AlterCentralNumberingGlobalCustomerCorporateCoupon.sql (GUS/COR/CUP global)

    - SeedCentralNumberingFromTenant.sql (creates dbo.SeedCentralNumberingForTenant)



    Per tenant DB (before seed picks up CUP max):

    - CreateBookingEngineCouponsAndPromo.sql on each tenant that uses booking coupons



    This script only loops tenants and EXEC dbo.SeedCentralNumberingForTenant — no extra logic here.

    CUP / GUS / COR counters are merged inside the procedure (global hotel_zaaer_id = 0).



    Skips tenants with missing DatabaseName or databases that do not exist on the server.

    EntityZaaerCounters are raised to the maximum seen per entity_code across all processed tenants.

*/



SET NOCOUNT ON;

GO



IF OBJECT_ID(N'dbo.SeedCentralNumberingForTenant', N'P') IS NULL

    THROW 51010, 'Run SeedCentralNumberingFromTenant.sql first to create dbo.SeedCentralNumberingForTenant.', 1;

GO



DECLARE

    @TenantId INT,

    @TenantCode NVARCHAR(100),

    @TenantDatabase SYSNAME,

    @Processed INT = 0,

    @Skipped INT = 0,

    @Failed INT = 0,

    @Err NVARCHAR(4000);



DECLARE tenant_cursor CURSOR LOCAL FAST_FORWARD FOR

    SELECT

        t.Id,

        t.Code,

        t.DatabaseName

    FROM dbo.Tenants AS t

    WHERE t.DatabaseName IS NOT NULL

      AND LTRIM(RTRIM(t.DatabaseName)) <> N''

    ORDER BY t.Id;



OPEN tenant_cursor;

FETCH NEXT FROM tenant_cursor INTO @TenantId, @TenantCode, @TenantDatabase;



WHILE @@FETCH_STATUS = 0

BEGIN

    IF DB_ID(@TenantDatabase) IS NULL

    BEGIN

        SET @Skipped += 1;

        PRINT N'[SKIP] Tenant ' + CAST(@TenantId AS NVARCHAR(20)) + N' (' + @TenantCode + N'): database not found -> ' + @TenantDatabase;

    END

    ELSE

    BEGIN

        BEGIN TRY

            PRINT N'[SEED] Tenant ' + CAST(@TenantId AS NVARCHAR(20)) + N' (' + @TenantCode + N') -> ' + @TenantDatabase;



            EXEC dbo.SeedCentralNumberingForTenant

                @TenantId = @TenantId,

                @TenantDatabase = @TenantDatabase;



            SET @Processed += 1;

        END TRY

        BEGIN CATCH

            SET @Failed += 1;

            SET @Err = ERROR_MESSAGE();

            PRINT N'[FAIL] Tenant ' + CAST(@TenantId AS NVARCHAR(20)) + N' (' + @TenantCode + N'): ' + @Err;

        END CATCH

    END



    FETCH NEXT FROM tenant_cursor INTO @TenantId, @TenantCode, @TenantDatabase;

END



CLOSE tenant_cursor;

DEALLOCATE tenant_cursor;



SELECT

    @Processed AS TenantsProcessed,

    @Skipped AS TenantsSkipped,

    @Failed AS TenantsFailed;



IF OBJECT_ID(N'dbo.EntityZaaerCounters', N'U') IS NOT NULL

BEGIN

    SELECT entity_code, current_value, updated_at

    FROM dbo.EntityZaaerCounters

    ORDER BY entity_code;

END



IF OBJECT_ID(N'dbo.DocumentCounters', N'U') IS NOT NULL

BEGIN

    SELECT doc_code, hotel_zaaer_id, current_value, updated_at

    FROM dbo.DocumentCounters

    WHERE doc_code IN (N'customer', N'corporate', N'booking_coupon')

    ORDER BY doc_code, hotel_zaaer_id;

END

GO

