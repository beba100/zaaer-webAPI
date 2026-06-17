/*
    Seed central numbering counters in Master DB from one tenant/hotel database.

    Prerequisites (Master DB):
    - CreateCentralNumbering.sql
    - HardenCentralNumbering.sql (recommended)
    - PerEntityZaaerCounters.sql

    Prerequisites (tenant DB, before seeding CUP):
    - CreateBookingEngineCouponsAndPromo.sql (booking_engine_coupons)

    Document counters seeded:
    - customers.customer_no              -> customer / GUS        (global counter, hotel_zaaer_id = 0)
    - corporate_customers.cor_no         -> corporate / COR       (global)
    - booking_engine_coupons.coupon_no   -> booking_coupon / CUP (global)
    - reservations.reservation_no        -> reservation / REV     (per hotel)
    - payment_receipts.receipt_no REC    -> payment_receipt / REC
    - payment_receipts.receipt_no PAY    -> payment_refund / PAY
    - invoices.invoice_no                -> invoice / INVO[-hotel]-number
    - orders.order_no                    -> order / ORD
    - credit_notes.credit_note_no        -> credit_note / CRED
    - promissory_notes.promissory_no      -> promissory_note / DRAF
    - expenses.expense_no                -> expense / EXP

    EntityZaaerCounters (per-entity global zaaer_id) seeded from MAX per table/entity
    across the tenant DB. Requires PerEntityZaaerCounters.sql on Master DB.

    Uniqueness: (entity_type, zaaer_id) — not zaaer_id alone. See docs/NUMBERING_AND_ZAAER_ID.md.

    Single tenant (manual):
        EXEC dbo.SeedCentralNumberingForTenant
            @TenantId = 48,
            @TenantDatabase = N'db32440_Hassa1';

    All tenants:
        Run SeedAllTenantsNumbering.sql on Master DB.
*/

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.SeedCentralNumberingForTenant
(
    @TenantId INT,
    @TenantDatabase SYSNAME
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TenantId IS NULL OR @TenantDatabase IS NULL
        THROW 51000, 'TenantId and TenantDatabase are required.', 1;

    IF DB_ID(@TenantDatabase) IS NULL
        THROW 51002, 'Tenant database does not exist on this server.', 1;

    DECLARE
        @Sql NVARCHAR(MAX),
        @HotelZaaerId INT,
        @LocalHotelId INT,
        @MaxBuilding BIGINT = 0,
        @MaxFloor BIGINT = 0,
        @MaxApartment BIGINT = 0,
        @MaxRoomType BIGINT = 0,
        @MaxFacility BIGINT = 0,
        @MaxDebitNote BIGINT = 0,
        @MaxCustomerZaaer BIGINT = 0,
        @MaxReservationZaaer BIGINT = 0,
        @MaxPaymentReceiptZaaer BIGINT = 0,
        @MaxInvoiceZaaer BIGINT = 0,
        @MaxOrderZaaer BIGINT = 0,
        @MaxCreditNoteZaaer BIGINT = 0,
        @MaxCorporateZaaer BIGINT = 0,
        @MaxPromissoryZaaer BIGINT = 0,
        @MaxExpenseZaaer BIGINT = 0,
        @MaxBuildingZaaer BIGINT = 0,
        @MaxFloorZaaer BIGINT = 0,
        @MaxApartmentZaaer BIGINT = 0,
        @MaxRoomTypeZaaer BIGINT = 0,
        @MaxFacilityZaaer BIGINT = 0,
        @MaxDebitNoteZaaer BIGINT = 0,
        @EntityCode NVARCHAR(50),
        @EntityMax BIGINT,
        @MaxCustomer BIGINT,
        @MaxReservation BIGINT,
        @MaxReceipt BIGINT,
        @MaxPaymentRefund BIGINT,
        @MaxInvoice BIGINT,
        @MaxOrder BIGINT,
        @MaxCreditNote BIGINT,
        @MaxCorporate BIGINT,
        @MaxPromissory BIGINT,
        @MaxExpense BIGINT,
        @MaxBookingCoupon BIGINT = 0,
        @DocCode NVARCHAR(50),
        @CurrentValue BIGINT,
        @HasExpenseNo BIT = 0,
        @HasExpenseId BIT = 0,
        @HasExpenseSeq BIT = 0;

    SET @Sql = N'
SELECT TOP (1)
    @HotelZaaerIdOut = TRY_CAST(zaaer_id AS INT),
    @LocalHotelIdOut = TRY_CAST(hotel_id AS INT)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.hotel_settings
WHERE zaaer_id IS NOT NULL
ORDER BY hotel_id;';

    EXEC sp_executesql
        @Sql,
        N'@HotelZaaerIdOut INT OUTPUT, @LocalHotelIdOut INT OUTPUT',
        @HotelZaaerIdOut = @HotelZaaerId OUTPUT,
        @LocalHotelIdOut = @LocalHotelId OUTPUT;

    IF @HotelZaaerId IS NULL
        THROW 51001, 'Cannot seed numbering because tenant hotel_settings.zaaer_id is missing.', 1;

    SET @HasExpenseNo = CASE
        WHEN COL_LENGTH(@TenantDatabase + N'.dbo.expenses', 'expense_no') IS NOT NULL THEN 1
        ELSE 0
    END;
    SET @HasExpenseId = CASE
        WHEN COL_LENGTH(@TenantDatabase + N'.dbo.expenses', 'expense_id') IS NOT NULL THEN 1
        ELSE 0
    END;
    SET @HasExpenseSeq = CASE
        WHEN COL_LENGTH(@TenantDatabase + N'.dbo.expenses', 'expense_seq') IS NOT NULL THEN 1
        ELSE 0
    END;

    SET @Sql = N'
SELECT @MaxCustomer = ISNULL(MAX(TRY_CAST(REPLACE(customer_no, ''GUS'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.customers
WHERE customer_no LIKE ''GUS%'';

SELECT @MaxReservation = ISNULL(MAX(TRY_CAST(REPLACE(reservation_no, ''REV'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.reservations
WHERE reservation_no LIKE ''REV%'';

SELECT @MaxReceipt = ISNULL(MAX(TRY_CAST(REPLACE(receipt_no, ''REC'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.payment_receipts
WHERE receipt_no LIKE ''REC%'';

SELECT @MaxPaymentRefund = ISNULL(MAX(TRY_CAST(REPLACE(receipt_no, ''PAY'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.payment_receipts
WHERE receipt_no LIKE ''PAY%'';

SELECT @MaxInvoice = ISNULL(MAX(
    TRY_CAST(
        CASE
            WHEN invoice_no LIKE ''INVO-%-%'' THEN PARSENAME(REPLACE(invoice_no, ''-'', ''.''), 1)
            WHEN invoice_no LIKE ''INVO%'' THEN REPLACE(invoice_no, ''INVO'', '''')
            ELSE NULL
        END AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.invoices
WHERE invoice_no LIKE ''INVO%'';

SELECT @MaxOrder = ISNULL(MAX(TRY_CAST(REPLACE(order_no, ''ORD'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.orders
WHERE order_no LIKE ''ORD%'';

SELECT @MaxCreditNote = ISNULL(MAX(TRY_CAST(REPLACE(credit_note_no, ''CRED'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.credit_notes
WHERE credit_note_no LIKE ''CRED%'';

SELECT @MaxCorporate = ISNULL(MAX(TRY_CAST(REPLACE(cor_no, ''COR'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.corporate_customers
WHERE cor_no LIKE ''COR%'';

SELECT @MaxPromissory = ISNULL(MAX(TRY_CAST(REPLACE(promissory_no, ''DRAF'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.promissory_notes
WHERE promissory_no LIKE ''DRAF%'';';

    IF OBJECT_ID(QUOTENAME(@TenantDatabase) + N'.dbo.booking_engine_coupons', N'U') IS NOT NULL
    BEGIN
        SET @Sql = @Sql + N'

SELECT @MaxBookingCoupon = ISNULL(MAX(
    TRY_CAST(REPLACE(REPLACE(coupon_no, ''CUP-'', ''''), ''CUP'', '''') AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.booking_engine_coupons
WHERE coupon_no LIKE ''CUP%'';';
    END
    ELSE
        SET @MaxBookingCoupon = 0;

    IF @HasExpenseNo = 1 OR @HasExpenseSeq = 1
    BEGIN
        SET @Sql = @Sql + N'

SELECT @MaxExpense = ISNULL(MAX(v), 0)
FROM (
    SELECT TRY_CAST(0 AS BIGINT) AS v
    WHERE 1 = 0';

        IF @HasExpenseNo = 1
            SET @Sql = @Sql + N'
    UNION ALL
    SELECT TRY_CAST(
        REPLACE(REPLACE(expense_no, ''EXP_'', ''''), ''EXP'', '''') AS BIGINT)
    FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.expenses
    WHERE expense_no LIKE ''EXP%''';

        IF @HasExpenseSeq = 1
            SET @Sql = @Sql + N'
    UNION ALL
    SELECT TRY_CAST(expense_seq AS BIGINT)
    FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.expenses
    WHERE expense_seq IS NOT NULL AND expense_seq > 0';

        SET @Sql = @Sql + N'
) AS expense_values;';
    END
    ELSE
        SET @MaxExpense = 0;

    IF @HasExpenseNo = 1 OR @HasExpenseSeq = 1
    BEGIN
        EXEC sp_executesql
            @Sql,
            N'@MaxCustomer BIGINT OUTPUT,
              @MaxReservation BIGINT OUTPUT,
              @MaxReceipt BIGINT OUTPUT,
              @MaxPaymentRefund BIGINT OUTPUT,
              @MaxInvoice BIGINT OUTPUT,
              @MaxOrder BIGINT OUTPUT,
              @MaxCreditNote BIGINT OUTPUT,
              @MaxCorporate BIGINT OUTPUT,
              @MaxPromissory BIGINT OUTPUT,
              @MaxExpense BIGINT OUTPUT,
              @MaxBookingCoupon BIGINT OUTPUT',
            @MaxCustomer = @MaxCustomer OUTPUT,
            @MaxReservation = @MaxReservation OUTPUT,
            @MaxReceipt = @MaxReceipt OUTPUT,
            @MaxPaymentRefund = @MaxPaymentRefund OUTPUT,
            @MaxInvoice = @MaxInvoice OUTPUT,
            @MaxOrder = @MaxOrder OUTPUT,
            @MaxCreditNote = @MaxCreditNote OUTPUT,
            @MaxCorporate = @MaxCorporate OUTPUT,
            @MaxPromissory = @MaxPromissory OUTPUT,
            @MaxExpense = @MaxExpense OUTPUT,
            @MaxBookingCoupon = @MaxBookingCoupon OUTPUT;
    END
    ELSE
    BEGIN
        EXEC sp_executesql
            @Sql,
            N'@MaxCustomer BIGINT OUTPUT,
              @MaxReservation BIGINT OUTPUT,
              @MaxReceipt BIGINT OUTPUT,
              @MaxPaymentRefund BIGINT OUTPUT,
              @MaxInvoice BIGINT OUTPUT,
              @MaxOrder BIGINT OUTPUT,
              @MaxCreditNote BIGINT OUTPUT,
              @MaxCorporate BIGINT OUTPUT,
              @MaxPromissory BIGINT OUTPUT,
              @MaxBookingCoupon BIGINT OUTPUT',
            @MaxCustomer = @MaxCustomer OUTPUT,
            @MaxReservation = @MaxReservation OUTPUT,
            @MaxReceipt = @MaxReceipt OUTPUT,
            @MaxPaymentRefund = @MaxPaymentRefund OUTPUT,
            @MaxInvoice = @MaxInvoice OUTPUT,
            @MaxOrder = @MaxOrder OUTPUT,
            @MaxCreditNote = @MaxCreditNote OUTPUT,
            @MaxCorporate = @MaxCorporate OUTPUT,
            @MaxPromissory = @MaxPromissory OUTPUT,
            @MaxBookingCoupon = @MaxBookingCoupon OUTPUT;
    END

    SET @Sql = N'
SELECT @MaxCustomerZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.customers;

SELECT @MaxReservationZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.reservations;

SELECT @MaxPaymentReceiptZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.payment_receipts;

SELECT @MaxInvoiceZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.invoices;

SELECT @MaxOrderZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.orders;

SELECT @MaxCreditNoteZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.credit_notes;

SELECT @MaxCorporateZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.corporate_customers;

SELECT @MaxPromissoryZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.promissory_notes;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.debit_notes', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxDebitNoteZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.debit_notes;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.buildings', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxBuildingZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.buildings;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.floors', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxFloorZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.floors;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.apartments', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxApartmentZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.apartments;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.room_types', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxRoomTypeZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.room_types;';

    IF COL_LENGTH(@TenantDatabase + N'.dbo.facilities', 'zaaer_id') IS NOT NULL
        SET @Sql = @Sql + N'
SELECT @MaxFacilityZaaer = ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.facilities;';

    IF @HasExpenseId = 1
        SET @Sql = @Sql + N'
SELECT @MaxExpenseZaaer = ISNULL(MAX(TRY_CAST(expense_id AS BIGINT)), 0)
FROM ' + QUOTENAME(@TenantDatabase) + N'.dbo.expenses;';

    EXEC sp_executesql
        @Sql,
        N'@MaxCustomerZaaer BIGINT OUTPUT,
          @MaxReservationZaaer BIGINT OUTPUT,
          @MaxPaymentReceiptZaaer BIGINT OUTPUT,
          @MaxInvoiceZaaer BIGINT OUTPUT,
          @MaxOrderZaaer BIGINT OUTPUT,
          @MaxCreditNoteZaaer BIGINT OUTPUT,
          @MaxCorporateZaaer BIGINT OUTPUT,
          @MaxPromissoryZaaer BIGINT OUTPUT,
          @MaxDebitNoteZaaer BIGINT OUTPUT,
          @MaxBuildingZaaer BIGINT OUTPUT,
          @MaxFloorZaaer BIGINT OUTPUT,
          @MaxApartmentZaaer BIGINT OUTPUT,
          @MaxRoomTypeZaaer BIGINT OUTPUT,
          @MaxFacilityZaaer BIGINT OUTPUT,
          @MaxExpenseZaaer BIGINT OUTPUT',
        @MaxCustomerZaaer = @MaxCustomerZaaer OUTPUT,
        @MaxReservationZaaer = @MaxReservationZaaer OUTPUT,
        @MaxPaymentReceiptZaaer = @MaxPaymentReceiptZaaer OUTPUT,
        @MaxInvoiceZaaer = @MaxInvoiceZaaer OUTPUT,
        @MaxOrderZaaer = @MaxOrderZaaer OUTPUT,
        @MaxCreditNoteZaaer = @MaxCreditNoteZaaer OUTPUT,
        @MaxCorporateZaaer = @MaxCorporateZaaer OUTPUT,
        @MaxPromissoryZaaer = @MaxPromissoryZaaer OUTPUT,
        @MaxDebitNoteZaaer = @MaxDebitNoteZaaer OUTPUT,
        @MaxBuildingZaaer = @MaxBuildingZaaer OUTPUT,
        @MaxFloorZaaer = @MaxFloorZaaer OUTPUT,
        @MaxApartmentZaaer = @MaxApartmentZaaer OUTPUT,
        @MaxRoomTypeZaaer = @MaxRoomTypeZaaer OUTPUT,
        @MaxFacilityZaaer = @MaxFacilityZaaer OUTPUT,
        @MaxExpenseZaaer = @MaxExpenseZaaer OUTPUT;

    DECLARE @Counters TABLE
    (
        doc_code NVARCHAR(50) NOT NULL,
        current_value BIGINT NOT NULL
    );

    INSERT INTO @Counters(doc_code, current_value)
    VALUES
        (N'customer', @MaxCustomer),
        (N'reservation', @MaxReservation),
        (N'payment_receipt', @MaxReceipt),
        (N'payment_refund', @MaxPaymentRefund),
        (N'invoice', @MaxInvoice),
        (N'order', @MaxOrder),
        (N'credit_note', @MaxCreditNote),
        (N'corporate', @MaxCorporate),
        (N'promissory_note', @MaxPromissory),
        (N'expense', @MaxExpense),
        (N'booking_coupon', @MaxBookingCoupon);

    -- Seed every doc type (including 0) so DocumentCounters has a full row set per hotel.
    DECLARE counter_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT doc_code, current_value
        FROM @Counters;

    OPEN counter_cursor;
    FETCH NEXT FROM counter_cursor INTO @DocCode, @CurrentValue;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC dbo.SeedDocumentCounter
            @HotelZaaerId = @HotelZaaerId,
            @DocCode = @DocCode,
            @CurrentValue = @CurrentValue,
            @TenantId = @TenantId,
            @LocalHotelId = @LocalHotelId;

        FETCH NEXT FROM counter_cursor INTO @DocCode, @CurrentValue;
    END;

    CLOSE counter_cursor;
    DEALLOCATE counter_cursor;

    IF OBJECT_ID(N'dbo.SeedEntityZaaerCounter', N'P') IS NULL
        THROW 51003, 'Run PerEntityZaaerCounters.sql before seeding entity Zaaer counters.', 1;

    DECLARE @EntityCounters TABLE
    (
        entity_code NVARCHAR(50) NOT NULL,
        current_value BIGINT NOT NULL
    );

    INSERT INTO @EntityCounters(entity_code, current_value)
    VALUES
        (N'customer', @MaxCustomerZaaer),
        (N'reservation', @MaxReservationZaaer),
        (N'payment_receipt', @MaxPaymentReceiptZaaer),
        (N'invoice', @MaxInvoiceZaaer),
        (N'order', @MaxOrderZaaer),
        (N'credit_note', @MaxCreditNoteZaaer),
        (N'debit_note', @MaxDebitNoteZaaer),
        (N'corporate', @MaxCorporateZaaer),
        (N'promissory_note', @MaxPromissoryZaaer),
        (N'expense', @MaxExpenseZaaer),
        (N'building', @MaxBuildingZaaer),
        (N'floor', @MaxFloorZaaer),
        (N'apartment', @MaxApartmentZaaer),
        (N'room_type', @MaxRoomTypeZaaer),
        (N'facility', @MaxFacilityZaaer);

    DECLARE entity_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT entity_code, current_value
        FROM @EntityCounters;

    OPEN entity_cursor;
    FETCH NEXT FROM entity_cursor INTO @EntityCode, @EntityMax;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC dbo.SeedEntityZaaerCounter
            @EntityCode = @EntityCode,
            @CurrentValue = @EntityMax;

        FETCH NEXT FROM entity_cursor INTO @EntityCode, @EntityMax;
    END;

    CLOSE entity_cursor;
    DEALLOCATE entity_cursor;

    SELECT
        @TenantId AS TenantId,
        @TenantDatabase AS TenantDatabase,
        @HotelZaaerId AS HotelZaaerId,
        @LocalHotelId AS LocalHotelId,
        @MaxCustomer AS MaxCustomer,
        @MaxReservation AS MaxReservation,
        @MaxReceipt AS MaxReceipt,
        @MaxPaymentRefund AS MaxPaymentRefund,
        @MaxInvoice AS MaxInvoice,
        @MaxOrder AS MaxOrder,
        @MaxCreditNote AS MaxCreditNote,
        @MaxCorporate AS MaxCorporate,
        @MaxPromissory AS MaxPromissory,
        @MaxExpense AS MaxExpense,
        @MaxCustomerZaaer AS MaxCustomerZaaer,
        @MaxReservationZaaer AS MaxReservationZaaer,
        @MaxPaymentReceiptZaaer AS MaxPaymentReceiptZaaer,
        @MaxInvoiceZaaer AS MaxInvoiceZaaer;
END;
GO
