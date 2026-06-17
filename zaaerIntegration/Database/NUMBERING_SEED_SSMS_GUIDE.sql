/*
    دليل تشغيل الترقيم المركزي من SSMS (بدون شاشة numbering-admin)
    Master DB فقط — لا تعدّل tenant DB يدوياً من هنا إلا للقراءة/التحقق.

    ═══ إعادة من الصفر (حذف ثم بناء) ═══
    انظر: NUMBERING_FRESH_INSTALL.md

    0) DropCentralNumbering.sql          ← حذف كل الجداول والإجراءات
    1) CreateCentralNumbering.sql
    2) HardenCentralNumbering.sql
    3) PerEntityZaaerCounters.sql
    4) SeedCentralNumberingFromTenant.sql
    5) SeedAllTenantsNumbering.sql

    ═══ تحديث بدون حذف (الوضع العادي) ═══
    شغّل فقط (3)(4) إن لم تُنفَّذ، ثم (5) للـ seed.

    الخطة B:
    - zaaer_id  → EntityZaaerCounters (عالمي لكل entity_code)
    - document_no → DocumentCounters (لكل hotel_zaaer_id + doc_code)
*/

SET NOCOUNT ON;
GO

/* =============================================================================
   المرحلة 0 — تجهيز Master (مرة واحدة، بالترتيب)
   ============================================================================= */

/*
1) CreateCentralNumbering.sql
2) HardenCentralNumbering.sql
3) PerEntityZaaerCounters.sql
4) SeedCentralNumberingFromTenant.sql   ← ينشئ الإجراء SeedCentralNumberingForTenant

تأكد أن الإجراءات موجودة:
*/
SELECT name FROM sys.procedures WHERE name IN (
    N'SeedCentralNumberingForTenant',
    N'SeedDocumentCounter',
    N'SeedEntityZaaerCounter'
);
GO

/* =============================================================================
   المرحلة 1 — خريطة سريعة: من أين نقرأ MAX في tenant؟
   ============================================================================= */

/*
| doc_code (Master)     | جدول tenant              | عمود zaaer_id | رقم العرض (prefix)     |
|-----------------------|--------------------------|---------------|-------------------------|
| customer              | customers                | zaaer_id      | GUS (global)            |
| reservation           | reservations             | zaaer_id      | REV                     |
| payment_receipt       | payment_receipts         | zaaer_id      | REC (+ PAY للاسترداد)   |
| invoice               | invoices                 | zaaer_id      | INVO / INVO-hotel-####  |
| order                 | orders                   | zaaer_id      | ORD                     |
| credit_note           | credit_notes             | zaaer_id      | CRED                    |
| debit_note            | debit_notes              | zaaer_id      | DEBT (إن وُجد debit_note_no) |
| corporate             | corporate_customers        | zaaer_id      | COR (global)            |
| booking_coupon        | booking_engine_coupons   | —             | CUP (global)            |
| promissory_note       | promissory_notes         | zaaer_id      | DRAF                    |
| expense               | expenses                 | expense_id    | EXP / EXP_              |
| building,floor,...      | buildings,floors,...     | zaaer_id      | (بدون prefix عرض)       |

payment_refund يشارك entity_code = payment_receipt في Master (zaaer_entity_code).
*/

/* =============================================================================
   المرحلة 2 — فحص اختياري على tenant واحد (قبل الـ seed)
   غيّر @TenantDb لاسم قاعدة الفندق
   ============================================================================= */

DECLARE @TenantDb SYSNAME = N'db32440_Hassa1';  -- مثال

IF DB_ID(@TenantDb) IS NULL
    PRINT N'Tenant DB not found on this server: ' + @TenantDb;
ELSE
BEGIN
    SELECT TOP (1)
        zaaer_id AS hotel_zaaer_id,
        hotel_id AS local_hotel_id
    FROM dbo.hotel_settings
    WHERE zaaer_id IS NOT NULL
    ORDER BY hotel_id;

    SELECT N'--- zaaer_id (entity) ---' AS section;
    SELECT MAX(TRY_CAST(zaaer_id AS BIGINT)) AS max_zaaer_id FROM dbo.reservations;
    SELECT MAX(TRY_CAST(zaaer_id AS BIGINT)) AS max_zaaer_id FROM dbo.invoices;
    SELECT MAX(TRY_CAST(zaaer_id AS BIGINT)) AS max_zaaer_id FROM dbo.customers;

    SELECT N'--- document_no (عرض) ---' AS section;
    SELECT MAX(TRY_CAST(REPLACE(reservation_no, 'REV', '') AS BIGINT)) AS max_rev
    FROM dbo.reservations WHERE reservation_no LIKE 'REV%';

    SELECT MAX(TRY_CAST(REPLACE(customer_no, 'GUS', '') AS BIGINT)) AS max_gus
    FROM dbo.customers WHERE customer_no LIKE 'GUS%';
END;
GO

/* =============================================================================
   المرحلة 3 — الطريقة الرسمية: Seed كل الـ tenants من Master
   الملف: SeedAllTenantsNumbering.sql
   ============================================================================= */

/*
-- على Master DB:
:r SeedAllTenantsNumbering.sql

أو tenant واحد:
*/
-- EXEC dbo.SeedCentralNumberingForTenant
--     @TenantId = 48,
--     @TenantDatabase = N'db32440_Hassa1';

/* =============================================================================
   المرحلة 4 — تحقق بعد الـ seed
   ============================================================================= */

-- أ) عدادات zaaer_id العالمية (الأهم لحل مشكلة "يبدأ من 1")
SELECT entity_code, current_value, updated_at
FROM dbo.EntityZaaerCounters
ORDER BY entity_code;

-- ب) أرقام العرض لكل فندق
SELECT hotel_zaaer_id, doc_code, current_value, tenant_id, local_hotel_id, updated_at
FROM dbo.DocumentCounters
ORDER BY hotel_zaaer_id, doc_code;

-- ج) مقارنة: هل reservation counter أقل من أكبر zaaer_id في Audit؟
SELECT
    (SELECT current_value FROM dbo.EntityZaaerCounters WHERE entity_code = N'reservation') AS entity_counter,
    (SELECT MAX(TRY_CAST(zaaer_id AS BIGINT))
    FROM dbo.NumberGenerationAudit
    WHERE doc_code = N'reservation'
      AND status IN (N'reserved', N'committed') AS audit_max_zaaer_id;

-- د) قائمة الفنادق المسجلة
SELECT Id, Code, DatabaseName, ZaaerId
FROM dbo.Tenants
WHERE DatabaseName IS NOT NULL AND LTRIM(DatabaseName) <> N''
ORDER BY Id;

GO

/* =============================================================================
   المرحلة 5 — إصلاح سريع من Audit (بعد Plan B بدون seed كامل)
   يرفع EntityZaaerCounters من MAX(zaaer_id) في NumberGenerationAudit
   ============================================================================= */

/*
-- لكل doc_code (مثال reservation):
DECLARE @max BIGINT;
SELECT @max = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM dbo.NumberGenerationAudit
WHERE doc_code = N'reservation'
  AND status IN (N'reserved', N'committed');

EXEC dbo.SeedEntityZaaerCounter
    @EntityCode = N'reservation',
    @CurrentValue = @max;

-- أو لكل الأنواع النشطة التي تستخدم zaaer_id:
DECLARE @doc NVARCHAR(50), @max BIGINT;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT doc_code FROM dbo.DocumentTypes WHERE is_active = 1 AND uses_global_zaaer_id = 1;
OPEN c;
FETCH NEXT FROM c INTO @doc;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @max = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
    FROM dbo.NumberGenerationAudit
    WHERE doc_code = @doc AND status IN (N'reserved', N'committed');

    EXEC dbo.SeedEntityZaaerCounter @EntityCode = @doc, @CurrentValue = @max;
    FETCH NEXT FROM c INTO @doc;
END
CLOSE c; DEALLOCATE c;
*/

GO

/* =============================================================================
   تحديث يدوي لعداد واحد (بدون tenant كامل)
   ============================================================================= */

/*
-- رفع عداد zaaer_id لكيان reservation:
EXEC dbo.SeedEntityZaaerCounter
    @EntityCode = N'reservation',
    @CurrentValue = 111132;   -- ضع MAX من tenant أو Audit

-- رفع عداد رقم عرض لفندق + نوع مستند:
EXEC dbo.SeedDocumentCounter
    @HotelZaaerId = 21,           -- hotel_settings.zaaer_id
    @DocCode = N'reservation',
    @CurrentValue = 494,            -- آخر رقم بعد REV (بدون prefix)
    @TenantId = 48,
    @LocalHotelId = 21;
*/

GO
