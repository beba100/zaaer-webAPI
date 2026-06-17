/*
    Master DB — global document counters + booking coupon type (CUP).

    Run on Master after CreateCentralNumbering.sql + PerEntityZaaerCounters.sql.

    Changes:
    - customer (GUS)  → scope_level = global (hotel_zaaer_id = 0)
    - corporate (COR) → scope_level = global
    - booking_coupon (CUP) → new, global prefix, no zaaer_id
    - SeedDocumentCounter respects global scope (hotel_zaaer_id = 0)
*/

SET NOCOUNT ON;
GO

MERGE dbo.DocumentTypes AS target
USING (VALUES
    (N'customer',        N'GUS', 4, N'global', 0, 1, N'-', N'customer'),
    (N'corporate',        N'COR', 4, N'global', 0, 1, N'-', N'corporate'),
    (N'booking_coupon',   N'CUP', 4, N'global', 0, 0, N'-', NULL)
) AS source(doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator, zaaer_entity_code)
ON target.doc_code = source.doc_code
WHEN MATCHED THEN
    UPDATE SET
        prefix = source.prefix,
        padding = source.padding,
        scope_level = source.scope_level,
        include_hotel_in_number = source.include_hotel_in_number,
        uses_global_zaaer_id = source.uses_global_zaaer_id,
        separator = source.separator,
        zaaer_entity_code = source.zaaer_entity_code,
        is_active = 1,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator, zaaer_entity_code)
    VALUES (source.doc_code, source.prefix, source.padding, source.scope_level, source.include_hotel_in_number, source.uses_global_zaaer_id, source.separator, source.zaaer_entity_code);
GO

-- Bootstrap global counter row for CUP (and ensure customer/corporate use hotel 0)
INSERT INTO dbo.DocumentCounters (tenant_id, hotel_zaaer_id, local_hotel_id, doc_code, current_value)
SELECT NULL, 0, NULL, dt.doc_code, 0
FROM dbo.DocumentTypes AS dt
WHERE dt.doc_code IN (N'customer', N'corporate', N'booking_coupon')
  AND dt.scope_level = N'global'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.DocumentCounters AS c
      WHERE c.hotel_zaaer_id = 0
        AND c.doc_code = dt.doc_code
  );
GO

CREATE OR ALTER PROCEDURE dbo.SeedDocumentCounter
(
    @HotelZaaerId INT,
    @DocCode NVARCHAR(50),
    @CurrentValue BIGINT,
    @TenantId INT = NULL,
    @LocalHotelId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ScopeLevel NVARCHAR(20);
    DECLARE @CounterHotelZaaerId INT;

    SELECT @ScopeLevel = scope_level
    FROM dbo.DocumentTypes
    WHERE doc_code = @DocCode;

    IF @ScopeLevel IS NULL
        THROW 50020, 'Cannot seed unknown document type.', 1;

    SET @CounterHotelZaaerId = CASE WHEN @ScopeLevel = N'global' THEN 0 ELSE @HotelZaaerId END;

    MERGE dbo.DocumentCounters AS target
    USING
    (
        SELECT @CounterHotelZaaerId AS hotel_zaaer_id,
               @DocCode AS doc_code,
               @CurrentValue AS current_value
    ) AS source
    ON target.hotel_zaaer_id = source.hotel_zaaer_id
       AND target.doc_code = source.doc_code
    WHEN MATCHED THEN
        UPDATE SET
            current_value = CASE WHEN target.current_value < source.current_value THEN source.current_value ELSE target.current_value END,
            tenant_id = COALESCE(@TenantId, target.tenant_id),
            local_hotel_id = COALESCE(@LocalHotelId, target.local_hotel_id),
            updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (tenant_id, hotel_zaaer_id, local_hotel_id, doc_code, current_value)
        VALUES (@TenantId, @CounterHotelZaaerId, @LocalHotelId, @DocCode, @CurrentValue);
END;
GO

PRINT 'AlterCentralNumberingGlobalCustomerCorporateCoupon.sql completed.';
GO
