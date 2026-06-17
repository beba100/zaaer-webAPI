/*
    ═══════════════════════════════════════════════════════════════════════════
    حذف كامل لبنية الترقيم المركزية على Master DB
    ═══════════════════════════════════════════════════════════════════════════

    ⚠ قبل التشغيل:
    - نفّذ على قاعدة Master فقط (ليس tenant DB).
    - أوقف API / workers التي تستدعي GetNextBusinessIdentity أثناء الحذف.
    - سيُحذف كل بيانات العدادات والـ Audit — لا يمكن التراجع إلا بـ backup.

    بعد هذا الملف شغّل بالترتيب (انظر NUMBERING_FRESH_INSTALL.md):
      1) CreateCentralNumbering.sql
      2) HardenCentralNumbering.sql
      3) PerEntityZaaerCounters.sql
      4) SeedCentralNumberingFromTenant.sql
      5) SeedAllTenantsNumbering.sql
*/

SET NOCOUNT ON;
GO

PRINT N'=== Drop central numbering (Master DB) ===';
GO

/* ── 1) Stored procedures (كل ما يخص الترقيم) ── */

DECLARE @sql NVARCHAR(MAX);
DECLARE @procs TABLE (proc_name SYSNAME NOT NULL PRIMARY KEY);

INSERT INTO @procs(proc_name) VALUES
    (N'AllocateEntityZaaerIdFromDocCode'),
    (N'GetNextBusinessIdentity'),
    (N'GetNextDocumentNumber'),
    (N'GetNextEntityZaaerId'),
    (N'GetNextGlobalZaaerId'),
    (N'MarkNumberGenerationCommitted'),
    (N'MarkNumberGenerationVoided'),
    (N'SeedCentralNumberingForTenant'),
    (N'SeedDocumentCounter'),
    (N'SeedEntityZaaerCounter');

DECLARE @name SYSNAME;

DECLARE proc_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT p.proc_name
    FROM @procs AS p
    WHERE OBJECT_ID(QUOTENAME(N'dbo') + N'.' + QUOTENAME(p.proc_name), N'P') IS NOT NULL
    ORDER BY p.proc_name;

OPEN proc_cursor;
FETCH NEXT FROM proc_cursor INTO @name;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP PROCEDURE dbo.' + QUOTENAME(@name) + N';';
    PRINT @sql;
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM proc_cursor INTO @name;
END;

CLOSE proc_cursor;
DEALLOCATE proc_cursor;
GO

/* ── 2) Tables (ترتيب FK: DocumentCounters → DocumentTypes) ── */

IF OBJECT_ID(N'dbo.DocumentCounters', N'U') IS NOT NULL
BEGIN
    PRINT N'DROP TABLE dbo.DocumentCounters';
    DROP TABLE dbo.DocumentCounters;
END;
GO

IF OBJECT_ID(N'dbo.NumberGenerationAudit', N'U') IS NOT NULL
BEGIN
    PRINT N'DROP TABLE dbo.NumberGenerationAudit';
    DROP TABLE dbo.NumberGenerationAudit;
END;
GO

IF OBJECT_ID(N'dbo.EntityZaaerCounters', N'U') IS NOT NULL
BEGIN
    PRINT N'DROP TABLE dbo.EntityZaaerCounters';
    DROP TABLE dbo.EntityZaaerCounters;
END;
GO

IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NOT NULL
BEGIN
    PRINT N'DROP TABLE dbo.DocumentTypes';
    DROP TABLE dbo.DocumentTypes;
END;
GO

/* ── 3) Legacy sequence (Plan A — اختياري؛ Plan B لا يحتاجه) ── */

IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'GlobalZaaerSeq' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    PRINT N'DROP SEQUENCE dbo.GlobalZaaerSeq';
    DROP SEQUENCE dbo.GlobalZaaerSeq;
END;
GO

/* ── 4) تحقق: يجب ألا يبقى شيء ── */

SELECT
    o.type_desc,
    o.name
FROM sys.objects AS o
WHERE o.schema_id = SCHEMA_ID(N'dbo')
  AND (
        o.name IN (
            N'DocumentTypes',
            N'DocumentCounters',
            N'NumberGenerationAudit',
            N'EntityZaaerCounters',
            N'GlobalZaaerSeq'
        )
        OR o.name IN (
            N'AllocateEntityZaaerIdFromDocCode',
            N'GetNextBusinessIdentity',
            N'GetNextDocumentNumber',
            N'GetNextEntityZaaerId',
            N'GetNextGlobalZaaerId',
            N'MarkNumberGenerationCommitted',
            N'MarkNumberGenerationVoided',
            N'SeedCentralNumberingForTenant',
            N'SeedDocumentCounter',
            N'SeedEntityZaaerCounter'
        )
  );

IF @@ROWCOUNT = 0
    PRINT N'OK — central numbering objects removed. Proceed with fresh install scripts.';
ELSE
    PRINT N'WARNING — some objects still exist (see result set above).';
GO
