/*
    Central numbering infrastructure for the Master DB.

    Scope rules:
    - hotel_zaaer_id is Tenants.ZaaerId in Master DB and hotel_settings.zaaer_id in each hotel DB.
    - local_hotel_id is hotel_settings.hotel_id in the tenant DB, kept only for audit/debugging.
    - Document counters are scoped by (hotel_zaaer_id, doc_code), except global document types use hotel_zaaer_id = 0.
*/

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes
    (
        doc_type_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentTypes PRIMARY KEY,
        doc_code NVARCHAR(50) NOT NULL,
        prefix NVARCHAR(20) NOT NULL,
        padding INT NOT NULL CONSTRAINT DF_DocumentTypes_Padding DEFAULT (4),
        scope_level NVARCHAR(20) NOT NULL CONSTRAINT DF_DocumentTypes_ScopeLevel DEFAULT (N'hotel'),
        include_hotel_in_number BIT NOT NULL CONSTRAINT DF_DocumentTypes_IncludeHotel DEFAULT (0),
        uses_global_zaaer_id BIT NOT NULL CONSTRAINT DF_DocumentTypes_UsesGlobalZaaerId DEFAULT (1),
        separator NVARCHAR(5) NOT NULL CONSTRAINT DF_DocumentTypes_Separator DEFAULT (N'-'),
        is_active BIT NOT NULL CONSTRAINT DF_DocumentTypes_IsActive DEFAULT (1),
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_DocumentTypes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(3) NULL,
        CONSTRAINT UQ_DocumentTypes_DocCode UNIQUE (doc_code),
        CONSTRAINT CK_DocumentTypes_Padding CHECK (padding BETWEEN 1 AND 18),
        CONSTRAINT CK_DocumentTypes_ScopeLevel CHECK (scope_level IN (N'global', N'hotel'))
    );
END;
GO

IF OBJECT_ID(N'dbo.DocumentCounters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentCounters
    (
        counter_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentCounters PRIMARY KEY,
        tenant_id INT NULL,
        hotel_zaaer_id INT NOT NULL,
        local_hotel_id INT NULL,
        doc_code NVARCHAR(50) NOT NULL,
        current_value BIGINT NOT NULL CONSTRAINT DF_DocumentCounters_CurrentValue DEFAULT (0),
        row_version ROWVERSION NOT NULL,
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_DocumentCounters_CreatedAt DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(3) NOT NULL CONSTRAINT DF_DocumentCounters_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_DocumentCounters_Hotel_Doc UNIQUE (hotel_zaaer_id, doc_code),
        CONSTRAINT FK_DocumentCounters_DocumentTypes FOREIGN KEY (doc_code) REFERENCES dbo.DocumentTypes(doc_code)
    );
END;
GO

IF OBJECT_ID(N'dbo.NumberGenerationAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NumberGenerationAudit
    (
        audit_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NumberGenerationAudit PRIMARY KEY,
        tenant_id INT NULL,
        hotel_zaaer_id INT NULL,
        local_hotel_id INT NULL,
        doc_code NVARCHAR(50) NULL,
        numeric_value BIGINT NULL,
        document_no NVARCHAR(100) NULL,
        zaaer_id BIGINT NULL,
        status NVARCHAR(20) NOT NULL CONSTRAINT DF_NumberGenerationAudit_Status DEFAULT (N'reserved'),
        generated_by NVARCHAR(100) NULL,
        request_ref NVARCHAR(150) NULL,
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_NumberGenerationAudit_CreatedAt DEFAULT (SYSUTCDATETIME()),
        committed_at DATETIME2(3) NULL,
        voided_at DATETIME2(3) NULL,
        void_reason NVARCHAR(1000) NULL,
        CONSTRAINT CK_NumberGenerationAudit_Status CHECK (status IN (N'reserved', N'committed', N'void'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NumberGenerationAudit_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.NumberGenerationAudit'))
BEGIN
    CREATE INDEX IX_NumberGenerationAudit_Status_CreatedAt
        ON dbo.NumberGenerationAudit(status, created_at)
        INCLUDE (hotel_zaaer_id, doc_code, document_no, zaaer_id);
END;
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

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'GlobalZaaerSeq' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE SEQUENCE dbo.GlobalZaaerSeq
        AS BIGINT
        START WITH 1
        INCREMENT BY 1
        MINVALUE 1
        CACHE 500;
END;
GO

MERGE dbo.DocumentTypes AS target
USING (VALUES
    (N'customer',        N'GUS',  4, N'global', 0, 1, N'-'),
    (N'corporate',        N'COR',  4, N'global', 0, 1, N'-'),
    (N'booking_coupon',   N'CUP',  4, N'global', 0, 0, N'-'),
    (N'reservation',     N'REV',  4, N'hotel',  0, 1, N'-'),
    (N'payment_receipt', N'REC',  4, N'hotel',  0, 1, N'-'),
    (N'payment_refund',  N'PAY',  4, N'hotel',  0, 1, N'-'),
    (N'invoice',         N'INVO', 4, N'hotel',  1, 1, N'-'),
    (N'order',           N'ORD',  4, N'hotel',  0, 1, N'-'),
    (N'credit_note',     N'CRED', 4, N'hotel',  0, 1, N'-'),
    (N'promissory_note', N'DRAF', 4, N'hotel',  0, 1, N'-'),
    (N'expense',         N'EXP',  4, N'hotel', 0, 1, N'-')
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

CREATE OR ALTER PROCEDURE dbo.GetNextGlobalZaaerId
(
    @GeneratedBy NVARCHAR(100) = NULL,
    @RequestRef NVARCHAR(150) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ZaaerId BIGINT;
    DECLARE @AuditId BIGINT;

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
    END

    SET @ZaaerId = NEXT VALUE FOR dbo.GlobalZaaerSeq;

    INSERT INTO dbo.NumberGenerationAudit
    (
        zaaer_id,
        status,
        generated_by,
        request_ref
    )
    VALUES
    (
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
            SET @ZaaerId = NEXT VALUE FOR dbo.GlobalZaaerSeq;

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

CREATE OR ALTER PROCEDURE dbo.MarkNumberGenerationCommitted
(
    @AuditId BIGINT
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.NumberGenerationAudit
    SET status = N'committed',
        committed_at = SYSUTCDATETIME()
    WHERE audit_id = @AuditId
      AND status = N'reserved';
END;
GO

CREATE OR ALTER PROCEDURE dbo.MarkNumberGenerationVoided
(
    @AuditId BIGINT,
    @VoidReason NVARCHAR(1000) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.NumberGenerationAudit
    SET status = N'void',
        voided_at = SYSUTCDATETIME(),
        void_reason = @VoidReason
    WHERE audit_id = @AuditId
      AND status = N'reserved';
END;
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

    IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE doc_code = @DocCode)
        THROW 50020, 'Cannot seed unknown document type.', 1;

    DECLARE @ScopeLevel NVARCHAR(20);
    DECLARE @CounterHotelZaaerId INT;

    SELECT @ScopeLevel = scope_level
    FROM dbo.DocumentTypes
    WHERE doc_code = @DocCode;

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
        VALUES (@TenantId, @HotelZaaerId, @LocalHotelId, @DocCode, @CurrentValue);
END;
GO
