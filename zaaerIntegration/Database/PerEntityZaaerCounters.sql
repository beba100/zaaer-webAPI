/*
    Plan B: per-entity-type global zaaer_id counters (Master DB).

    Run on Master DB after:
    - CreateCentralNumbering.sql
    - HardenCentralNumbering.sql (recommended)

    Replaces dbo.GlobalZaaerSeq for new allocations. document_no counters
    (DocumentCounters per hotel + doc_code) are unchanged.

    Idempotent — safe to re-run.
*/

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.EntityZaaerCounters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntityZaaerCounters
    (
        entity_code NVARCHAR(50) NOT NULL CONSTRAINT PK_EntityZaaerCounters PRIMARY KEY,
        current_value BIGINT NOT NULL CONSTRAINT DF_EntityZaaerCounters_CurrentValue DEFAULT (0),
        row_version ROWVERSION NOT NULL,
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_EntityZaaerCounters_CreatedAt DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(3) NOT NULL CONSTRAINT DF_EntityZaaerCounters_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF COL_LENGTH(N'dbo.DocumentTypes', N'zaaer_entity_code') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTypes
        ADD zaaer_entity_code NVARCHAR(50) NULL;
END;
GO

MERGE dbo.DocumentTypes AS target
USING (VALUES
    (N'customer',        N'GUS',  4, N'global', 0, 1, N'-', N'customer'),
    (N'corporate',        N'COR',  4, N'global', 0, 1, N'-', N'corporate'),
    (N'booking_coupon',   N'CUP',  4, N'global', 0, 0, N'-', NULL),
    (N'reservation',     N'REV',  4, N'hotel', 0, 1, N'-', NULL),
    (N'payment_receipt', N'REC',  4, N'hotel', 0, 1, N'-', NULL),
    (N'payment_refund',  N'PAY',  4, N'hotel', 0, 1, N'-', N'payment_receipt'),
    (N'invoice',         N'INVO', 4, N'hotel', 1, 1, N'-', NULL),
    (N'order',           N'ORD',  4, N'hotel', 0, 1, N'-', NULL),
    (N'credit_note',     N'CRED', 4, N'hotel', 0, 1, N'-', NULL),
    (N'debit_note',      N'DEBT', 4, N'hotel', 0, 1, N'-', NULL),
    (N'promissory_note', N'DRAF', 4, N'hotel', 0, 1, N'-', NULL),
    (N'expense',         N'EXP',  4, N'hotel', 0, 1, N'-', NULL),
    (N'building',        N'',     4, N'hotel', 0, 1, N'-', NULL),
    (N'floor',           N'',     4, N'hotel', 0, 1, N'-', NULL),
    (N'apartment',       N'',     4, N'hotel', 0, 1, N'-', NULL),
    (N'room_type',       N'',     4, N'hotel', 0, 1, N'-', NULL),
    (N'facility',        N'',     4, N'hotel', 0, 1, N'-', NULL)
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

-- Bootstrap entity counters for every active doc type that allocates zaaer_id.
INSERT INTO dbo.EntityZaaerCounters(entity_code, current_value)
SELECT DISTINCT
    COALESCE(dt.zaaer_entity_code, dt.doc_code),
    0
FROM dbo.DocumentTypes AS dt
WHERE dt.is_active = 1
  AND dt.uses_global_zaaer_id = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.EntityZaaerCounters AS ez
      WHERE ez.entity_code = COALESCE(dt.zaaer_entity_code, dt.doc_code)
  );
GO

CREATE OR ALTER PROCEDURE dbo.SeedEntityZaaerCounter
(
    @EntityCode NVARCHAR(50),
    @CurrentValue BIGINT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EntityCode IS NULL OR LTRIM(RTRIM(@EntityCode)) = N''
        THROW 50030, 'EntityCode is required.', 1;

    MERGE dbo.EntityZaaerCounters AS target
    USING (SELECT @EntityCode AS entity_code, @CurrentValue AS current_value) AS source
    ON target.entity_code = source.entity_code
    WHEN MATCHED THEN
        UPDATE SET
            current_value = CASE WHEN target.current_value < source.current_value THEN source.current_value ELSE target.current_value END,
            updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (entity_code, current_value)
        VALUES (source.entity_code, source.current_value);
END;
GO

CREATE OR ALTER PROCEDURE dbo.AllocateEntityZaaerIdFromDocCode
(
    @DocCode NVARCHAR(50),
    @ZaaerId BIGINT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @EntityCode NVARCHAR(50),
        @UsesEntityZaaerId BIT;

    SELECT
        @EntityCode = COALESCE(zaaer_entity_code, doc_code),
        @UsesEntityZaaerId = uses_global_zaaer_id
    FROM dbo.DocumentTypes WITH (UPDLOCK, HOLDLOCK)
    WHERE doc_code = @DocCode
      AND is_active = 1;

    IF @EntityCode IS NULL
        THROW 50010, 'Invalid or inactive document type.', 1;

    IF @UsesEntityZaaerId = 0
    BEGIN
        SET @ZaaerId = NULL;
        RETURN;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.EntityZaaerCounters WITH (UPDLOCK, HOLDLOCK)
        WHERE entity_code = @EntityCode
    )
    BEGIN
        INSERT INTO dbo.EntityZaaerCounters(entity_code, current_value)
        VALUES (@EntityCode, 0);
    END;

    DECLARE @Updated TABLE (current_value BIGINT NOT NULL);

    UPDATE dbo.EntityZaaerCounters
    SET current_value = current_value + 1,
        updated_at = SYSUTCDATETIME()
    OUTPUT inserted.current_value INTO @Updated(current_value)
    WHERE entity_code = @EntityCode;

    SELECT @ZaaerId = current_value FROM @Updated;
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetNextEntityZaaerId
(
    @DocCode NVARCHAR(50),
    @GeneratedBy NVARCHAR(100) = NULL,
    @RequestRef NVARCHAR(150) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ZaaerId BIGINT;
    DECLARE @AuditId BIGINT;

    IF @DocCode IS NULL OR LTRIM(RTRIM(@DocCode)) = N''
        THROW 50031, 'DocCode is required for entity Zaaer ID allocation.', 1;

    IF @RequestRef IS NOT NULL
    BEGIN
        SELECT TOP (1)
            @ZaaerId = zaaer_id,
            @AuditId = audit_id
        FROM dbo.NumberGenerationAudit WITH (UPDLOCK, HOLDLOCK)
        WHERE request_ref = @RequestRef
          AND status IN (N'reserved', N'committed')
        ORDER BY audit_id DESC;

        IF @AuditId IS NOT NULL
        BEGIN
            SELECT @ZaaerId AS ZaaerId,
                   @AuditId AS AuditId;
            RETURN;
        END
    END;

    EXEC dbo.AllocateEntityZaaerIdFromDocCode
        @DocCode = @DocCode,
        @ZaaerId = @ZaaerId OUTPUT;

    INSERT INTO dbo.NumberGenerationAudit
    (
        doc_code,
        zaaer_id,
        status,
        generated_by,
        request_ref
    )
    VALUES
    (
        @DocCode,
        @ZaaerId,
        N'reserved',
        @GeneratedBy,
        @RequestRef
    );

    SET @AuditId = SCOPE_IDENTITY();

    SELECT @ZaaerId AS ZaaerId,
           @AuditId AS AuditId;
END;
GO

-- Backward-compatible name: requires @DocCode (no longer a single global sequence).
CREATE OR ALTER PROCEDURE dbo.GetNextGlobalZaaerId
(
    @DocCode NVARCHAR(50),
    @GeneratedBy NVARCHAR(100) = NULL,
    @RequestRef NVARCHAR(150) = NULL
)
AS
BEGIN
    EXEC dbo.GetNextEntityZaaerId
        @DocCode = @DocCode,
        @GeneratedBy = @GeneratedBy,
        @RequestRef = @RequestRef;
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetNextBusinessIdentity
(
    @HotelZaaerId INT,
    @DocCode NVARCHAR(50),
    @TenantId INT = NULL,
    @LocalHotelId INT = NULL,
    @GeneratedBy NVARCHAR(100) = NULL,
    @RequestRef NVARCHAR(150) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @Prefix NVARCHAR(20),
        @Padding INT,
        @ScopeLevel NVARCHAR(20),
        @IncludeHotelInNumber BIT,
        @UsesGlobalZaaerId BIT,
        @Separator NVARCHAR(5),
        @CounterHotelZaaerId INT,
        @NextValue BIGINT,
        @DocumentNo NVARCHAR(100),
        @ZaaerId BIGINT = NULL,
        @AuditId BIGINT;

    BEGIN TRAN;

    BEGIN TRY
        IF @RequestRef IS NOT NULL
        BEGIN
            SELECT TOP (1)
                @ZaaerId = zaaer_id,
                @NextValue = numeric_value,
                @DocumentNo = document_no,
                @AuditId = audit_id
            FROM dbo.NumberGenerationAudit WITH (UPDLOCK, HOLDLOCK)
            WHERE request_ref = @RequestRef
              AND status IN (N'reserved', N'committed')
            ORDER BY audit_id DESC;

            IF @AuditId IS NOT NULL
            BEGIN
                COMMIT;

                SELECT @ZaaerId AS ZaaerId,
                       @NextValue AS NumericValue,
                       @DocumentNo AS DocumentNo,
                       @AuditId AS AuditId;
                RETURN;
            END
        END

        SELECT
            @Prefix = prefix,
            @Padding = padding,
            @ScopeLevel = scope_level,
            @IncludeHotelInNumber = include_hotel_in_number,
            @UsesGlobalZaaerId = uses_global_zaaer_id,
            @Separator = separator
        FROM dbo.DocumentTypes WITH (UPDLOCK, HOLDLOCK)
        WHERE doc_code = @DocCode
          AND is_active = 1;

        IF @Prefix IS NULL
            THROW 50010, 'Invalid or inactive document type.', 1;

        IF @UsesGlobalZaaerId = 1
            EXEC dbo.AllocateEntityZaaerIdFromDocCode
                @DocCode = @DocCode,
                @ZaaerId = @ZaaerId OUTPUT;

        SET @CounterHotelZaaerId = CASE WHEN @ScopeLevel = N'global' THEN 0 ELSE @HotelZaaerId END;

        IF @CounterHotelZaaerId IS NULL
            THROW 50011, 'HotelZaaerId is required for hotel-scoped document types.', 1;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.DocumentCounters WITH (UPDLOCK, HOLDLOCK)
            WHERE hotel_zaaer_id = @CounterHotelZaaerId
              AND doc_code = @DocCode
        )
        BEGIN
            INSERT INTO dbo.DocumentCounters
            (
                tenant_id,
                hotel_zaaer_id,
                local_hotel_id,
                doc_code,
                current_value
            )
            VALUES
            (
                @TenantId,
                @CounterHotelZaaerId,
                @LocalHotelId,
                @DocCode,
                0
            );
        END;

        DECLARE @Updated TABLE (current_value BIGINT NOT NULL);

        UPDATE dbo.DocumentCounters
        SET current_value = current_value + 1,
            tenant_id = COALESCE(@TenantId, tenant_id),
            local_hotel_id = COALESCE(@LocalHotelId, local_hotel_id),
            updated_at = SYSUTCDATETIME()
        OUTPUT inserted.current_value INTO @Updated(current_value)
        WHERE hotel_zaaer_id = @CounterHotelZaaerId
          AND doc_code = @DocCode;

        SELECT @NextValue = current_value FROM @Updated;

        SET @DocumentNo =
            CASE
                WHEN @IncludeHotelInNumber = 1
                THEN @Prefix + @Separator + CAST(@HotelZaaerId AS NVARCHAR(20)) + @Separator +
                     RIGHT(REPLICATE(N'0', @Padding) + CAST(@NextValue AS NVARCHAR(30)), @Padding)
                ELSE @Prefix +
                     RIGHT(REPLICATE(N'0', @Padding) + CAST(@NextValue AS NVARCHAR(30)), @Padding)
            END;

        INSERT INTO dbo.NumberGenerationAudit
        (
            tenant_id,
            hotel_zaaer_id,
            local_hotel_id,
            doc_code,
            numeric_value,
            document_no,
            zaaer_id,
            status,
            generated_by,
            request_ref
        )
        VALUES
        (
            @TenantId,
            @HotelZaaerId,
            @LocalHotelId,
            @DocCode,
            @NextValue,
            @DocumentNo,
            @ZaaerId,
            N'reserved',
            @GeneratedBy,
            @RequestRef
        );

        SET @AuditId = SCOPE_IDENTITY();

        COMMIT;

        SELECT @ZaaerId AS ZaaerId,
               @NextValue AS NumericValue,
               @DocumentNo AS DocumentNo,
               @AuditId AS AuditId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

PRINT N'PerEntityZaaerCounters migration applied. Run SeedCentralNumberingFromTenant / SeedAllTenantsNumbering to seed EntityZaaerCounters from tenant data.';
GO
