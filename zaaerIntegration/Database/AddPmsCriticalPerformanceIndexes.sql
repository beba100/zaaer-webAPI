/*
  PMS critical performance indexes (tenant database).

  Purpose:
  - Room board / availability date-overlap queries.
  - Booking engine availability checks.
  - PMS financial reports by document date.
  - Reservation detail and finance lookups by reservation key.

  Safe to run multiple times. Apply to one tenant database first, test PMS flows,
  then roll out to the remaining tenant databases.
*/

PRINT '============================================================';
PRINT 'PMS critical performance indexes - starting';
PRINT 'Database: ' + DB_NAME();
PRINT '============================================================';
GO

/* reservation_units: room board and booking availability overlap checks */
IF OBJECT_ID(N'dbo.reservation_units', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'apartment_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'check_in_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'check_out_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'departure_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'status') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'reservation_id') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservation_units_apartment_checkin'
          AND object_id = OBJECT_ID(N'dbo.reservation_units')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservation_units_apartment_checkin
    ON dbo.reservation_units (apartment_id, check_in_date)
    INCLUDE (check_out_date, departure_date, status, reservation_id);

    PRINT 'Created IX_reservation_units_apartment_checkin';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservation_units_apartment_checkin';
END
GO

IF OBJECT_ID(N'dbo.reservation_units', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'check_in_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'apartment_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'check_out_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'departure_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'status') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_units', N'zaaer_id') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservation_units_reservation_checkin'
          AND object_id = OBJECT_ID(N'dbo.reservation_units')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservation_units_reservation_checkin
    ON dbo.reservation_units (reservation_id, check_in_date)
    INCLUDE (apartment_id, check_out_date, departure_date, status, zaaer_id);

    PRINT 'Created IX_reservation_units_reservation_checkin';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservation_units_reservation_checkin';
END
GO

/* reservation_unit_swaps: unit transfers report by transfer date and reservation key */
IF OBJECT_ID(N'dbo.reservation_unit_swaps', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'created_at') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'switch_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'unit_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'from_apartment_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'to_apartment_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'apply_mode') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'effective_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_swaps', N'created_by_user_id') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservation_unit_swaps_created_reservation'
          AND object_id = OBJECT_ID(N'dbo.reservation_unit_swaps')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservation_unit_swaps_created_reservation
    ON dbo.reservation_unit_swaps (created_at, reservation_id)
    INCLUDE (switch_id, unit_id, from_apartment_id, to_apartment_id, apply_mode, effective_date, created_by_user_id);

    PRINT 'Created IX_reservation_unit_swaps_created_reservation';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservation_unit_swaps_created_reservation';
END
GO

/* maintenances: unit/date overlap checks used by room board and booking engine */
IF OBJECT_ID(N'dbo.maintenances', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.maintenances', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.maintenances', N'unit_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.maintenances', N'from_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.maintenances', N'to_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.maintenances', N'status') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_maintenances_hotel_unit_dates'
          AND object_id = OBJECT_ID(N'dbo.maintenances')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_maintenances_hotel_unit_dates
    ON dbo.maintenances (hotel_id, unit_id, from_date, to_date)
    INCLUDE (status);

    PRINT 'Created IX_maintenances_hotel_unit_dates';
END
ELSE
BEGIN
    PRINT 'Skipped IX_maintenances_hotel_unit_dates';
END
GO

/* payment_receipts: PMS finance reports and reservation payment tab */
IF OBJECT_ID(N'dbo.payment_receipts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_status') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'voucher_code') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_type') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'payment_method') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'amount_paid') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_no') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_payment_receipts_hotel_receipt_date'
          AND object_id = OBJECT_ID(N'dbo.payment_receipts')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_payment_receipts_hotel_receipt_date
    ON dbo.payment_receipts (hotel_id, receipt_date)
    INCLUDE (reservation_id, receipt_status, voucher_code, receipt_type, payment_method, amount_paid, receipt_no);

    PRINT 'Created IX_payment_receipts_hotel_receipt_date';
END
ELSE
BEGIN
    PRINT 'Skipped IX_payment_receipts_hotel_receipt_date';
END
GO

IF OBJECT_ID(N'dbo.payment_receipts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_status') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'voucher_code') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'amount_paid') IS NOT NULL
   AND COL_LENGTH(N'dbo.payment_receipts', N'receipt_no') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_payment_receipts_reservation_date'
          AND object_id = OBJECT_ID(N'dbo.payment_receipts')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_payment_receipts_reservation_date
    ON dbo.payment_receipts (reservation_id, receipt_date)
    INCLUDE (hotel_id, receipt_status, voucher_code, amount_paid, receipt_no);

    PRINT 'Created IX_payment_receipts_reservation_date';
END
ELSE
BEGIN
    PRINT 'Skipped IX_payment_receipts_reservation_date';
END
GO

/* invoices: PMS finance reports and reservation invoice tab */
IF OBJECT_ID(N'dbo.invoices', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'invoice_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'total_amount') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'payment_status') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'invoice_no') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'zaaer_id') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_invoices_hotel_invoice_date'
          AND object_id = OBJECT_ID(N'dbo.invoices')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_invoices_hotel_invoice_date
    ON dbo.invoices (hotel_id, invoice_date)
    INCLUDE (reservation_id, total_amount, payment_status, invoice_no, zaaer_id);

    PRINT 'Created IX_invoices_hotel_invoice_date';
END
ELSE
BEGIN
    PRINT 'Skipped IX_invoices_hotel_invoice_date';
END
GO

IF OBJECT_ID(N'dbo.invoices', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'invoice_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'total_amount') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'payment_status') IS NOT NULL
   AND COL_LENGTH(N'dbo.invoices', N'invoice_no') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_invoices_reservation_date'
          AND object_id = OBJECT_ID(N'dbo.invoices')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_invoices_reservation_date
    ON dbo.invoices (reservation_id, invoice_date)
    INCLUDE (hotel_id, total_amount, payment_status, invoice_no);

    PRINT 'Created IX_invoices_reservation_date';
END
ELSE
BEGIN
    PRINT 'Skipped IX_invoices_reservation_date';
END
GO

/* reservations: PMS reports, dashboard, and date-filtered searches */
IF OBJECT_ID(N'dbo.reservations', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'reservation_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'zaaer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'customer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'status') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'reservation_no') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'check_in_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'check_out_date') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservations_hotel_reservation_date'
          AND object_id = OBJECT_ID(N'dbo.reservations')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservations_hotel_reservation_date
    ON dbo.reservations (hotel_id, reservation_date)
    INCLUDE (reservation_id, zaaer_id, customer_id, status, reservation_no, check_in_date, check_out_date);

    PRINT 'Created IX_reservations_hotel_reservation_date';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservations_hotel_reservation_date';
END
GO

IF OBJECT_ID(N'dbo.reservations', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'check_in_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'check_out_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'zaaer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'customer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'status') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservations', N'reservation_no') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservations_hotel_checkin'
          AND object_id = OBJECT_ID(N'dbo.reservations')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservations_hotel_checkin
    ON dbo.reservations (hotel_id, check_in_date)
    INCLUDE (check_out_date, reservation_id, zaaer_id, customer_id, status, reservation_no);

    PRINT 'Created IX_reservations_hotel_checkin';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservations_hotel_checkin';
END
GO

/* reservation_unit_day_rates: reservation detail and period/day-rate loads */
IF OBJECT_ID(N'dbo.reservation_unit_day_rates', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_day_rates', N'reservation_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_day_rates', N'unit_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_day_rates', N'night_date') IS NOT NULL
   AND COL_LENGTH(N'dbo.reservation_unit_day_rates', N'gross_rate') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_reservation_unit_day_rates_reservation'
          AND object_id = OBJECT_ID(N'dbo.reservation_unit_day_rates')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservation_unit_day_rates_reservation
    ON dbo.reservation_unit_day_rates (reservation_id)
    INCLUDE (unit_id, night_date, gross_rate);

    PRINT 'Created IX_reservation_unit_day_rates_reservation';
END
ELSE
BEGIN
    PRINT 'Skipped IX_reservation_unit_day_rates_reservation';
END
GO

/* apartment/customer helper indexes for PMS dual-id lookups */
IF OBJECT_ID(N'dbo.apartments', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.apartments', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.apartments', N'zaaer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.apartments', N'apartment_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.apartments', N'apartment_code') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_apartments_hotel_zaaer'
          AND object_id = OBJECT_ID(N'dbo.apartments')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_apartments_hotel_zaaer
    ON dbo.apartments (hotel_id, zaaer_id)
    INCLUDE (apartment_id, apartment_code)
    WHERE zaaer_id IS NOT NULL;

    PRINT 'Created IX_apartments_hotel_zaaer';
END
ELSE
BEGIN
    PRINT 'Skipped IX_apartments_hotel_zaaer';
END
GO

IF OBJECT_ID(N'dbo.customers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.customers', N'zaaer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.customers', N'customer_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.customers', N'customer_name') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_customers_zaaer_id'
          AND object_id = OBJECT_ID(N'dbo.customers')
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_customers_zaaer_id
    ON dbo.customers (zaaer_id)
    INCLUDE (customer_id, customer_name)
    WHERE zaaer_id IS NOT NULL;

    PRINT 'Created IX_customers_zaaer_id';
END
ELSE
BEGIN
    PRINT 'Skipped IX_customers_zaaer_id';
END
GO

PRINT '============================================================';
PRINT 'PMS critical performance indexes - completed';
PRINT 'Database: ' + DB_NAME();
PRINT '============================================================';
GO
