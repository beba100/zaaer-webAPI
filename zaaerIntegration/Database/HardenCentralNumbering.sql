/*
    Hardening patch for existing Master DB numbering (run after CreateCentralNumbering.sql).

    - Idempotency: reuse reserved/committed rows for the same request_ref
    - Unique filtered index on request_ref (active allocations only)
    - Stored procedure updates

    For per-entity zaaer_id (Plan B), also run PerEntityZaaerCounters.sql (creates
    EntityZaaerCounters and dbo.AllocateEntityZaaerIdFromDocCode).

    Safe to re-run (CREATE OR ALTER / IF NOT EXISTS).
*/

SET NOCOUNT ON;
GO

MERGE dbo.DocumentTypes AS target
USING (VALUES
    (N'expense', N'EXP', 4, N'hotel', 0, 1, N'-')
) AS source(doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator)
ON target.doc_code = source.doc_code
WHEN MATCHED THEN
    UPDATE SET
        prefix = source.prefix,
        padding = source.padding,
        scope_level = source.scope_level,
        include_hotel_in_number = source.include_hotel_in_number,
        uses_global_zaaer_id = source.uses_global_zaaer_id,
        separator = source.separator,
        is_active = 1,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator)
    VALUES (source.doc_code, source.prefix, source.padding, source.scope_level, source.include_hotel_in_number, source.uses_global_zaaer_id, source.separator);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_NumberGenerationAudit_RequestRef_Active'
      AND object_id = OBJECT_ID(N'dbo.NumberGenerationAudit')
)
BEGIN
    CREATE UNIQUE INDEX UX_NumberGenerationAudit_RequestRef_Active
        ON dbo.NumberGenerationAudit(request_ref)
        WHERE request_ref IS NOT NULL
          AND status IN (N'reserved', N'committed');
END;
GO

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

CREATE OR ALTER PROCEDURE dbo.GetNextDocumentNumber
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
        @Separator NVARCHAR(5),
        @CounterHotelZaaerId INT,
        @NextValue BIGINT,
        @DocumentNo NVARCHAR(100),
        @AuditId BIGINT;

    BEGIN TRAN;

    BEGIN TRY
        IF @RequestRef IS NOT NULL
        BEGIN
            SELECT TOP (1)
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

                SELECT @NextValue AS NumericValue,
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
            @Separator = separator
        FROM dbo.DocumentTypes WITH (UPDLOCK, HOLDLOCK)
        WHERE doc_code = @DocCode
          AND is_active = 1;

        IF @Prefix IS NULL
            THROW 50010, 'Invalid or inactive document type.', 1;

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
            N'reserved',
            @GeneratedBy,
            @RequestRef
        );

        SET @AuditId = SCOPE_IDENTITY();

        COMMIT;

        SELECT @NextValue AS NumericValue,
               @DocumentNo AS DocumentNo,
               @AuditId AS AuditId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
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
