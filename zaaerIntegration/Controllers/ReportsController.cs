using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zaaerIntegration.Data;
using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Services;
using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using zaaerIntegration.Configuration;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Controller for Reports operations
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<ReportsController> _logger;
        private readonly ITenantService _tenantService;
        private readonly IConfiguration _configuration;
        private readonly SmartLogger? _smartLogger;
        private readonly PaymentDailyNetExTaxOptions _paymentDailyNetExTaxOptions;
        private readonly IHotelScopeService _hotelScopeService;

        /// <summary>
        /// Initializes a new instance of the ReportsController class
        /// </summary>
        public ReportsController(
            MasterDbContext masterDbContext,
            ILogger<ReportsController> logger,
            ITenantService tenantService,
            IConfiguration configuration,
            IOptions<PaymentDailyNetExTaxOptions> paymentDailyNetExTaxOptions,
            IHotelScopeService hotelScopeService,
            SmartLogger? smartLogger = null)
        {
            _masterDbContext = masterDbContext;
            _logger = logger;
            _tenantService = tenantService;
            _configuration = configuration;
            _paymentDailyNetExTaxOptions = paymentDailyNetExTaxOptions.Value;
            _hotelScopeService = hotelScopeService;
            _smartLogger = smartLogger;
        }

        private async Task<(List<FinanceLedgerAPI.Models.Tenant>? Tenants, IActionResult? Error)> ResolveReportTenantsAsync(
            string? hotelCodesCsv,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var tenants = await _hotelScopeService.ResolveTenantsAsync(hotelCodesCsv, cancellationToken);
                if (tenants.Count == 0)
                {
                    return (null, StatusCode(StatusCodes.Status403Forbidden, new { message = "No accessible hotels for this account." }));
                }

                return (tenants.ToList(), null);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "[SECURITY] Report tenant scope denied");
                return (null, StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }));
            }
        }

        /// <summary>
        /// Build connection string for tenant
        /// </summary>
        private string BuildConnectionStringForTenant(FinanceLedgerAPI.Models.Tenant tenant)
        {
            var server = _configuration["TenantDatabase:Server"]?.Trim();
            var userId = _configuration["TenantDatabase:UserId"]?.Trim();
            var password = _configuration["TenantDatabase:Password"]?.Trim();

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("TenantDatabase settings are missing in appsettings.json");
            }

            return $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        }

        /// <summary>
        /// Get daily report for all hotels or specific hotel - using Dapper for direct queries
        /// </summary>
        /// <param name="dateFrom">Start date (YYYY-MM-DD)</param>
        /// <param name="dateTo">End date (YYYY-MM-DD)</param>
        /// <param name="hotelCode">Optional hotel code filter (comma-separated). When omitted, returns all hotels assigned to the user.</param>
        /// <returns>Report data grouped by hotel and payment method</returns>
        [HttpGet("daily-report")]
        public async Task<IActionResult> GetDailyReport(
            [FromQuery] string dateFrom,
            [FromQuery] string dateTo,
            [FromQuery] string? hotelCode = null)
        {
            try
            {
                // Log request
                _smartLogger?.LogWarning(
                    category: "REPORTS",
                    message: $"Daily report request | DateFrom: {dateFrom}, DateTo: {dateTo}, HotelCode: {hotelCode ?? "ALL"}",
                    action: "GetDailyReport");

                if (string.IsNullOrWhiteSpace(dateFrom) || string.IsNullOrWhiteSpace(dateTo))
                {
                    _smartLogger?.LogWarning(
                        category: "REPORTS",
                        message: "Missing date parameters in daily report request",
                        action: "GetDailyReport");
                    return BadRequest(new { message = "يجب تحديد تاريخ البداية والنهاية" });
                }

                if (!DateTime.TryParse(dateFrom, out var fromDate) || !DateTime.TryParse(dateTo, out var toDate))
                {
                    _smartLogger?.LogWarning(
                        category: "REPORTS",
                        message: $"Invalid date format | DateFrom: {dateFrom}, DateTo: {dateTo}",
                        action: "GetDailyReport");
                    return BadRequest(new { message = "صيغة التاريخ غير صحيحة. استخدم YYYY-MM-DD" });
                }

                var (tenants, scopeError) = await ResolveReportTenantsAsync(hotelCode);
                if (scopeError != null)
                {
                    return scopeError;
                }

                if (tenants == null || tenants.Count == 0)
                {
                    return Ok(new { success = true, hotels = Array.Empty<object>(), message = "No accessible hotels" });
                }

                // Process each accessible tenant database

                if (!tenants.Any())
                {
                    return Ok(new { hotels = new List<object>() });
                }

                var hotelsData = new List<object>();

                // SQL queries using direct table access (no joins with mappings)
                // Include receipts with voucher_code = 'receipt' and receipt_status = 'paid'
                var receiptsSql = @"
                    SELECT 
                        PaymentMethod = ISNULL(payment_method, 'غير محدد'),
                        TotalAmount = SUM(amount_paid),
                        ReceiptCount = COUNT(*),
                        ReceiptNumbers = STRING_AGG(receipt_no, ', ')
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE) 
                      AND receipt_status = 'paid'
                      AND (voucher_code = 'receipt' OR voucher_code = 'security_deposit' OR voucher_code IS NULL)
                    GROUP BY payment_method";

                var invoicesSql = @"
                    SELECT 
                        PaymentMethod = 'غير محدد',
                        TotalAmount = SUM(ISNULL(i.total_amount, 0)),
                        InvoiceCount = COUNT(*),
                        InvoiceNumbers = STRING_AGG(i.invoice_no, ', ')
                    FROM invoices i WITH (NOLOCK)
                    WHERE CAST(i.invoice_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(i.invoice_date AS DATE) <= CAST(@DateTo AS DATE)";

                // Get cash balance summary for each hotel
                var cashBalanceSql = @"
                    -- حساب رصيد الصندوق المحاسبي الدقيق لكل فندق
                    SELECT
                        hs.zaaer_id as HotelId,
                        hs.hotel_name as HotelName,

                        -- الرصيد الافتتاحي
                        ISNULL((
                            SELECT TOP 1 opening_amount
                            FROM cash_opening_balance cob
                            WHERE cob.hotel_id = hs.zaaer_id
                              AND cob.opening_date <= CAST(@DateFrom AS DATE)
                            ORDER BY cob.opening_date DESC
                        ), 0) +
                        ISNULL((
                            SELECT SUM(
                                CASE WHEN pr.voucher_code IN ('receipt','security_deposit') THEN pr.amount_paid
                                     WHEN pr.voucher_code IN ('refund','security_deposit_refund','transfers_to_bank') THEN -pr.amount_paid
                                     ELSE 0 END
                            )
                            FROM payment_receipts pr
                            WHERE pr.hotel_id = hs.zaaer_id
                              AND pr.payment_method = 'Cash'
                              AND pr.receipt_status = 'paid'
                              AND pr.voucher_code IN ('receipt','security_deposit','refund','security_deposit_refund','transfers_to_bank')
                              AND pr.receipt_date < CAST(@DateFrom AS DATE)
                        ), 0) as OpeningBalance,

                        -- الرصيد الحالي (محاسبي دقيق)
                        ISNULL((
                            SELECT TOP 1 opening_amount
                            FROM cash_opening_balance cob
                            WHERE cob.hotel_id = hs.zaaer_id
                              AND cob.opening_date <= CAST(@DateFrom AS DATE)
                            ORDER BY cob.opening_date DESC
                        ), 0) +
                        ISNULL((
                            SELECT SUM(
                                CASE WHEN pr.voucher_code IN ('receipt','security_deposit') THEN pr.amount_paid
                                     WHEN pr.voucher_code IN ('refund','security_deposit_refund','transfers_to_bank') THEN -pr.amount_paid
                                     ELSE 0 END
                            )
                            FROM payment_receipts pr
                            WHERE pr.hotel_id = hs.zaaer_id
                              AND pr.payment_method = 'Cash'
                              AND pr.receipt_status = 'paid'
                              AND pr.voucher_code IN ('receipt','security_deposit','refund','security_deposit_refund','transfers_to_bank')
                              AND pr.receipt_date <= CAST(@DateTo AS DATE)
                        ), 0) as CurrentCashBalance

                    FROM hotel_settings hs
                    WHERE hs.zaaer_id IN (" + string.Join(",", tenants.Select(t => t.Id.ToString())) + @")";

                // Get cancelled receipts (for display only, not in sum)
                var cancelledReceiptsSql = @"
                    SELECT 
                        receipt_no,
                        receipt_type,
                        payment_method,
                        amount_paid,
                        receipt_date,
                        receipt_status,
                        voucher_code
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE) 
                      AND receipt_status = 'cancelled'
                    ORDER BY receipt_date DESC";

                // Get receipts detail (paid receipts with voucher_code = receipt)
                var receiptsDetailSql = @"
                    SELECT 
                        receipt_no,
                        receipt_type,
                        payment_method,
                        amount_paid,
                        receipt_date,
                        receipt_status,
                        voucher_code
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE) 
                      AND receipt_status = 'paid'
                      AND (voucher_code = 'receipt' OR voucher_code = 'security_deposit' OR voucher_code IS NULL)
                    ORDER BY receipt_date DESC";

                // Get refunds from payment_receipts where voucher_code = 'refund' (Rent Refund) or 'security_deposit_refund' (Security Deposit Refund)
                var refundsSql = @"
                    SELECT 
                        PaymentMethod = ISNULL(payment_method, 'Undefined'),
                        VoucherCode = voucher_code,
                        RefundType = CASE 
                            WHEN voucher_code = 'refund' THEN 'Rent Refund'
                            WHEN voucher_code = 'security_deposit_refund' THEN 'Security Deposit Refund'
                            ELSE ISNULL(voucher_code, 'Undefined')
                        END,
                        TotalAmount = SUM(ABS(amount_paid)),
                        RefundCount = COUNT(*),
                        RefundNumbers = STRING_AGG(receipt_no, ', ')
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE)
                      AND receipt_status = 'paid'
                      AND (voucher_code = 'refund' OR voucher_code = 'security_deposit_refund')
                    GROUP BY payment_method, voucher_code";

                var refundsDetailSql = @"
                    SELECT 
                        receipt_no AS refund_no,
                        receipt_type AS refund_type,
                        payment_method,
                        amount_paid AS refund_amount,
                        receipt_date AS refund_date,
                        receipt_status,
                        voucher_code,
                        CASE 
                            WHEN voucher_code = 'refund' THEN 'Rent Refund'
                            WHEN voucher_code = 'security_deposit_refund' THEN 'Security Deposit Refund'
                            ELSE ISNULL(voucher_code, 'Undefined')
                        END AS RefundTypeName
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE)
                      AND receipt_status = 'paid'
                      AND (voucher_code = 'refund' OR voucher_code = 'security_deposit_refund')
                    ORDER BY receipt_date DESC";

                // Get transfers_to_bank (expense, bilad, riyad)
                var transfersToBankSql = @"
                    SELECT 
                        receipt_no,
                        receipt_type,
                        payment_method,
                        amount_paid,
                        receipt_date,
                        bank_name,
                        voucher_code,
                        CASE 
                            WHEN bank_name = 'expense' THEN 'Expense'
                            WHEN bank_name = 'bilad' THEN 'Deposit to AlBilad Bank'
                            WHEN bank_name = 'riyad' THEN 'Deposit to Riyad Bank'
                            ELSE 'Other'
                        END AS TransferType
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE CAST(receipt_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(receipt_date AS DATE) <= CAST(@DateTo AS DATE) 
                      AND voucher_code = 'transfers_to_bank'
                    ORDER BY receipt_date DESC";

                var invoicesDetailSql = @"
                    SELECT 
                        invoice_no,
                        invoice_type,
                        payment_method = NULL,
                        total_amount,
                        invoice_date,
                        invoice_status = payment_status
                    FROM invoices WITH (NOLOCK)
                    WHERE CAST(invoice_date AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(invoice_date AS DATE) <= CAST(@DateTo AS DATE)
                    ORDER BY invoice_date DESC";

                // Get expenses from expenses table (instead of payment_receipts)
                var expensesSql = @"
                    SELECT 
                        expense_id,
                        date_time,
                        due_date,
                        total_amount,
                        comment,
                        approval_status,
                        expense_category_id
                    FROM expenses WITH (NOLOCK)
                    WHERE CAST(date_time AS DATE) >= CAST(@DateFrom AS DATE) 
                      AND CAST(date_time AS DATE) <= CAST(@DateTo AS DATE)
                      AND approval_status NOT IN('cancelled')
                    ORDER BY date_time DESC";

                // Use parallel processing for better performance - process all tenants concurrently
                var tenantTasks = tenants.Select(async tenant =>
                {
                    try
                    {
                        var connectionString = BuildConnectionStringForTenant(tenant);
                        await using var connection = new SqlConnection(connectionString);
                        await connection.OpenAsync();

                        _smartLogger?.LogWarning(
                            category: "REPORTS",
                            message: $"Querying tenant database | TenantCode: {tenant.Code}, Database: {tenant.DatabaseName}",
                            action: "GetDailyReport");

                        var parameters = new DynamicParameters();
                        parameters.Add("@DateFrom", fromDate.Date, DbType.Date);
                        parameters.Add("@DateTo", toDate.Date, DbType.Date);

                        // Get hotel settings
                        var hotelSettings = await connection.QueryFirstOrDefaultAsync<dynamic>(
                            "SELECT TOP 1 hotel_name, hotel_code, zaaer_id, hotel_id FROM hotel_settings WITH (NOLOCK)");

                        var hotelName = hotelSettings?.hotel_name?.ToString() 
                            ?? hotelSettings?.hotel_code?.ToString() 
                            ?? (hotelSettings?.zaaer_id != null ? $"فندق {hotelSettings.zaaer_id}" : tenant.Name);
                        var hotelId = hotelSettings?.zaaer_id ?? hotelSettings?.hotel_id ?? tenant.Id;

                        // Execute all queries in parallel for this tenant
                        var receiptsByMethodTask = connection.QueryAsync<dynamic>(receiptsSql, parameters);
                        var invoicesByMethodTask = connection.QueryAsync<dynamic>(invoicesSql, parameters);
                        var receiptsDetailTask = connection.QueryAsync<dynamic>(receiptsDetailSql, parameters);
                        var invoicesDetailTask = connection.QueryAsync<dynamic>(invoicesDetailSql, parameters);
                        var cancelledReceiptsTask = connection.QueryAsync<dynamic>(cancelledReceiptsSql, parameters);
                        var refundsByMethodTask = connection.QueryAsync<dynamic>(refundsSql, parameters);
                        var refundsDetailTask = connection.QueryAsync<dynamic>(refundsDetailSql, parameters);
                        var transfersToBankTask = connection.QueryAsync<dynamic>(transfersToBankSql, parameters);
                        var expensesTask = connection.QueryAsync<dynamic>(expensesSql, parameters);
                        var cashBalanceTask = connection.QueryFirstOrDefaultAsync<dynamic>(cashBalanceSql, parameters);

                        await Task.WhenAll(receiptsByMethodTask, invoicesByMethodTask, receiptsDetailTask,
                            invoicesDetailTask, cancelledReceiptsTask, refundsByMethodTask, refundsDetailTask, transfersToBankTask, expensesTask, cashBalanceTask);

                        var receiptsByMethod = (await receiptsByMethodTask).ToList();
                        var invoicesByMethod = (await invoicesByMethodTask).ToList();
                        var receiptsDetail = (await receiptsDetailTask).ToList();
                        var invoicesDetail = (await invoicesDetailTask).ToList();
                        var cancelledReceipts = (await cancelledReceiptsTask).ToList();
                        var refundsByMethod = (await refundsByMethodTask).ToList();
                        var refundsDetail = (await refundsDetailTask).ToList();
                        var transfersToBank = (await transfersToBankTask).ToList();
                        var expenses = (await expensesTask).ToList();
                        var cashBalance = await cashBalanceTask;

                        var totalReceipts = receiptsByMethod.Sum(r => (decimal)(r.TotalAmount ?? 0));
                        var totalInvoices = invoicesByMethod.Sum(i => (decimal)(i.TotalAmount ?? 0));
                        var totalRefunds = refundsByMethod.Sum(r => Math.Abs((decimal)(r.TotalAmount ?? 0))); // Use absolute value for refunds
                        var cancelledReceiptsTotal = cancelledReceipts.Sum(r => (decimal)(r.amount_paid ?? 0));
                        var cancelledReceiptsCount = cancelledReceipts.Count;

                        return new
                        {
                            hotelId = hotelId,
                            hotelCode = tenant.Code,
                            hotelName = hotelName,
                            totalReceipts = totalReceipts,
                            totalInvoices = totalInvoices,
                            totalRefunds = totalRefunds,
                            cancelledReceiptsTotal = cancelledReceiptsTotal,
                            cancelledReceiptsCount = cancelledReceiptsCount,
                            cashBalance = new
                            {
                                openingBalance = (decimal)(cashBalance?.OpeningBalance ?? 0),
                                currentCashBalance = (decimal)(cashBalance?.CurrentCashBalance ?? 0)
                            },
                            receiptsByMethod = receiptsByMethod.Select(r => new
                            {
                                paymentMethod = r.PaymentMethod?.ToString() ?? "غير محدد",
                                totalAmount = (decimal)(r.TotalAmount ?? 0),
                                receiptCount = (int)(r.ReceiptCount ?? 0),
                                receiptNumbers = r.ReceiptNumbers?.ToString() ?? ""
                            }),
                            invoicesByMethod = invoicesByMethod.Select(i => new
                            {
                                paymentMethod = "غير محدد", // Invoices don't have payment_method column
                                totalAmount = (decimal)(i.TotalAmount ?? 0),
                                invoiceCount = (int)(i.InvoiceCount ?? 0),
                                invoiceNumbers = i.InvoiceNumbers?.ToString() ?? ""
                            }),
                            refundsByMethod = refundsByMethod.Select(r => new
                            {
                                paymentMethod = r.PaymentMethod?.ToString() ?? "غير محدد",
                                voucherCode = r.VoucherCode?.ToString() ?? "",
                                refundType = r.RefundType?.ToString() ?? "",
                                totalAmount = Math.Abs((decimal)(r.TotalAmount ?? 0)), // Use absolute value for refunds
                                refundCount = (int)(r.RefundCount ?? 0),
                                refundNumbers = r.RefundNumbers?.ToString() ?? ""
                            }),
                            receiptsDetail = receiptsDetail.Select(r => new
                            {
                                receiptNo = r.receipt_no?.ToString() ?? "",
                                receiptType = r.receipt_type?.ToString() ?? "",
                                paymentMethod = r.payment_method?.ToString() ?? "غير محدد",
                                amountPaid = (decimal)(r.amount_paid ?? 0),
                                receiptDate = r.receipt_date != null ? ((DateTime)r.receipt_date).ToString("yyyy-MM-dd") : "",
                                receiptStatus = r.receipt_status?.ToString() ?? "",
                                voucherCode = r.voucher_code?.ToString() ?? ""
                            }),
                            invoicesDetail = invoicesDetail.Select(i => new
                            {
                                invoiceNo = i.invoice_no?.ToString() ?? "",
                                invoiceType = i.invoice_type?.ToString() ?? "",
                                paymentMethod = "غير محدد", // Invoices don't have payment_method column
                                totalAmount = (decimal)(i.total_amount ?? 0),
                                invoiceDate = i.invoice_date != null ? ((DateTime)i.invoice_date).ToString("yyyy-MM-dd") : "",
                                invoiceStatus = i.invoice_status?.ToString() ?? ""
                            }),
                            refundsDetail = refundsDetail.Select(r => new
                            {
                                refundNo = r.refund_no?.ToString() ?? "",
                                refundType = r.refund_type?.ToString() ?? "",
                                refundTypeName = r.RefundTypeName?.ToString() ?? "",
                                paymentMethod = r.payment_method?.ToString() ?? "غير محدد",
                                refundAmount = Math.Abs((decimal)(r.refund_amount ?? 0)), // Use absolute value for refunds
                                refundDate = r.refund_date != null ? ((DateTime)r.refund_date).ToString("yyyy-MM-dd") : "",
                                voucherCode = r.voucher_code?.ToString() ?? "",
                                receiptStatus = r.receipt_status?.ToString() ?? ""
                            }),
                            cancelledReceiptsDetail = cancelledReceipts.Select(r => new
                            {
                                receiptNo = r.receipt_no?.ToString() ?? "",
                                receiptType = r.receipt_type?.ToString() ?? "",
                                paymentMethod = r.payment_method?.ToString() ?? "غير محدد",
                                amountPaid = (decimal)(r.amount_paid ?? 0),
                                receiptDate = r.receipt_date != null ? ((DateTime)r.receipt_date).ToString("yyyy-MM-dd") : "",
                                receiptStatus = r.receipt_status?.ToString() ?? "",
                                voucherCode = r.voucher_code?.ToString() ?? ""
                            }),
                            transfersToBank = transfersToBank.Select(t => new
                            {
                                receiptNo = t.receipt_no?.ToString() ?? "",
                                receiptType = t.receipt_type?.ToString() ?? "",
                                paymentMethod = t.payment_method?.ToString() ?? "غير محدد",
                                amountPaid = (decimal)(t.amount_paid ?? 0),
                                receiptDate = t.receipt_date != null ? ((DateTime)t.receipt_date).ToString("yyyy-MM-dd") : "",
                                bankName = t.bank_name?.ToString() ?? "",
                                transferType = t.TransferType?.ToString() ?? ""
                            }),
                            expenses = expenses.Select(e => new
                            {
                                expenseId = (int)(e.expense_id ?? 0),
                                dateTime = e.date_time != null ? ((DateTime)e.date_time).ToString("yyyy-MM-dd") : "",
                                totalAmount = (decimal)(e.total_amount ?? 0),
                                comment = e.comment?.ToString() ?? "",
                                approvalStatus = e.approval_status?.ToString() ?? "",
                                expenseCategoryId = e.expense_category_id != null ? (int?)e.expense_category_id : null
                            })
                        };
                    }
                    catch (Exception ex)
                    {
                        _smartLogger?.LogError(
                            category: "REPORTS",
                            message: $"Error processing tenant in daily report | TenantCode: {tenant.Code}, Error: {ex.Message}",
                            action: "GetDailyReport",
                            exception: ex);
                        // Return null for failed tenant - will be filtered out
                        return null;
                    }
                });

                // Wait for all tenants to complete in parallel (much faster than sequential)
                var tenantResults = await Task.WhenAll(tenantTasks);
                
                // Filter out null results (failed tenants) and add to hotelsData
                foreach (var result in tenantResults.Where(r => r != null))
                {
                    hotelsData.Add(result!);
                }

                return Ok(new { hotels = hotelsData });
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "REPORTS",
                    message: $"Error generating daily report | DateFrom: {dateFrom}, DateTo: {dateTo}, Error: {ex.Message}",
                    action: "GetDailyReport",
                    exception: ex);
                return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء التقرير", error = ex.Message });
            }
        }

        /// <summary>
        /// Export daily report to Excel
        /// </summary>
        [HttpGet("daily-report/export")]
        public async Task<IActionResult> ExportDailyReport(
            [FromQuery] string dateFrom,
            [FromQuery] string dateTo)
        {
            try
            {
                // For now, return JSON. You can implement Excel export later using EPPlus or similar
                var result = await GetDailyReport(dateFrom, dateTo);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting daily report");
                return StatusCode(500, new { message = "حدث خطأ أثناء تصدير التقرير", error = ex.Message });
            }
        }

        /// <summary>
        /// Get tenant info by code
        /// </summary>
        [HttpGet("find-tenant-by-code/{code}")]
        public async Task<IActionResult> FindTenantByCode(string code)
        {
            var tenant = await _hotelScopeService.FindAccessibleTenantByCodeAsync(code);

            if (tenant == null)
            {
                return NotFound(new { message = $"Tenant with code {code} not found or not accessible" });
            }

            return Ok(new
            {
                tenantId = tenant.Id,
                tenantCode = tenant.Code,
                database = tenant.DatabaseName,
                zaaerId = tenant.ZaaerId
            });
        }

        /// <summary>
        /// Find tenant that has a specific zaaer_id
        /// </summary>
        [HttpGet("find-tenant-by-zaaer-id/{zaaerId}")]
        public async Task<IActionResult> FindTenantByZaaerId(int zaaerId)
        {
            try
            {
                var cachedTenant = await _masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ZaaerId == zaaerId);

                if (cachedTenant != null)
                {
                    if (!_hotelScopeService.CanAccessTenantId(cachedTenant.Id))
                    {
                        return NotFound(new { message = $"لم يتم العثور على tenant يحتوي على zaaer_id = {zaaerId}" });
                    }

                    return Ok(new { tenantId = cachedTenant.Id, tenantCode = cachedTenant.Code, source = "cached" });
                }

                var accessibleTenants = await _hotelScopeService.ResolveTenantsAsync();

                foreach (var tenant in accessibleTenants)
                {
                    try
                    {
                        var connectionString = BuildConnectionStringForTenant(tenant);
                        using var conn = new SqlConnection(connectionString);

                        var exists = await conn.ExecuteScalarAsync<int?>(
                            "SELECT 1 FROM hotel_settings WHERE zaaer_id = @ZaaerId",
                            new { ZaaerId = zaaerId });

                        if (exists == 1)
                        {
                            // Update the cache
                            tenant.ZaaerId = zaaerId;
                            _masterDbContext.Tenants.Update(tenant);
                            await _masterDbContext.SaveChangesAsync();

                            return Ok(new { tenantId = tenant.Id, tenantCode = tenant.Code, database = tenant.DatabaseName, source = "found" });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking tenant {Code}: {Message}", tenant.Code, ex.Message);
                        continue;
                    }
                }

                return NotFound(new { message = $"لم يتم العثور على tenant يحتوي على zaaer_id = {zaaerId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "خطأ في البحث", error = ex.Message });
            }
        }

        /// <summary>
        /// Get DevExtreme License Key
        /// </summary>
        /// <returns>License key for DevExtreme</returns>
        [HttpGet("devextreme-license")]
        [ProducesResponseType(500)]
        public ActionResult GetDevExtremeLicenseKey()
        {
            try
            {
                var licenseKey = _configuration["DevExtreme:LicenseKey"];
                
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    _logger?.LogWarning("[REPORTS] DevExtreme license key not found in configuration");
                    return BadRequest(new { message = "DevExtreme license key not configured" });
                }

                _smartLogger?.LogError(
                    "[REPORTS]",
                    "DevExtreme license key requested",
                    action: "GetDevExtremeLicenseKey"
                );
                
                return Ok(new { licenseKey });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[REPORTS] Error retrieving DevExtreme license key");
                _smartLogger?.LogError(
                    "[REPORTS]",
                    $"Error retrieving DevExtreme license key: {ex.Message}",
                    action: "GetDevExtremeLicenseKey",
                    exception: ex
                );
                return StatusCode(500, new { message = "Error retrieving license key", error = ex.Message });
            }
        }

        /// <summary>
        /// Get pending and failed payment receipts from all tenant databases
        /// </summary>
        /// <returns>List of payment receipts with status_vom IN ('pending', 'failed') grouped by database</returns>
        [HttpGet("pending-failed-receipts")]
        public async Task<IActionResult> GetPendingFailedReceipts()
        {
            try
            {
                _smartLogger?.LogWarning(
                    category: "REPORTS",
                    message: "Pending/Failed receipts request",
                    action: "GetPendingFailedReceipts");

                // Resolve tenants scoped to the authenticated user's hotel access
                var (tenants, scopeError) = await ResolveReportTenantsAsync(null);
                if (scopeError != null)
                {
                    return scopeError;
                }

                if (tenants == null || tenants.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        totalDatabases = 0,
                        totalRecords = 0,
                        databases = new List<object>()
                    });
                }

                var pendingFailedReceiptsSql = @"
                    SELECT 
                        receipt_id,
                        receipt_no,
                        receipt_type,
                        payment_method,
                        amount_paid,
                        receipt_date,
                        receipt_status,
                        status_vom,
                        vom_retry_count,
                        voucher_code,
                        hotel_id,
                        zaaer_id,
                        created_at,
                        vom_error,
                        'receipt' AS record_type
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE status_vom IN ('pending', 'failed')
                    ORDER BY receipt_date DESC, receipt_no DESC";

                var pendingFailedInvoicesSql = @"
                    SELECT 
                        invoice_id,
                        invoice_no,
                        invoice_type,
                        NULL AS payment_method,
                        total_amount AS amount_paid,
                        invoice_date AS receipt_date,
                        payment_status AS receipt_status,
                        status_vom,
                        vom_retry_count,
                        NULL AS voucher_code,
                        hotel_id,
                        zaaer_id,
                        created_at,
                        vom_error,
                        'invoice' AS record_type
                    FROM invoices WITH (NOLOCK)
                    WHERE status_vom IN ('pending', 'failed')
                    ORDER BY invoice_date DESC, invoice_no DESC";

                var pendingFailedCreditNotesSql = @"
                    SELECT 
                        credit_note_id,
                        credit_note_no,
                        'credit_note' AS receipt_type,
                        NULL AS payment_method,
                        credit_amount AS amount_paid,
                        credit_note_date AS receipt_date,
                        'paid' AS receipt_status,
                        status_vom,
                        vom_retry_count,
                        NULL AS voucher_code,
                        hotel_id,
                        zaaer_id,
                        created_at,
                        vom_error,
                        'credit_note' AS record_type
                    FROM credit_notes WITH (NOLOCK)
                    WHERE status_vom IN ('pending', 'failed')
                    ORDER BY credit_note_date DESC, credit_note_no DESC";

                // Process all tenants in parallel
                var tenantTasks = tenants.AsEnumerable().Select<FinanceLedgerAPI.Models.Tenant, Task<dynamic>>(async tenant =>
                {
                    try
                    {
                        var connectionString = BuildConnectionStringForTenant(tenant);
                        await using var connection = new SqlConnection(connectionString);
                        await connection.OpenAsync(); // Ensure connection is open
                        
                        var allRecords = new List<dynamic>();

                        // Check and query payment_receipts
                        var receiptsTableExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                            SELECT CASE WHEN EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'payment_receipts'
                            ) THEN 1 ELSE 0 END");

                        if (receiptsTableExists)
                        {
                            var receiptsColumnExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                                SELECT CASE WHEN EXISTS (
                                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                                    WHERE TABLE_SCHEMA = 'dbo' 
                                    AND TABLE_NAME = 'payment_receipts' 
                                    AND COLUMN_NAME = 'status_vom'
                                ) THEN 1 ELSE 0 END");

                            if (receiptsColumnExists)
                            {
                                try
                                {
                                    // First, try a simple count query to verify data exists
                                    var countResult = await connection.QueryFirstOrDefaultAsync<int>(@"
                                        SELECT COUNT(*) 
                                        FROM payment_receipts WITH (NOLOCK)
                                        WHERE status_vom IN ('pending', 'failed')");
                                    
                                    _smartLogger?.LogWarning(
                                        category: "REPORTS",
                                        message: $"Count query result for payment_receipts | TenantCode: {tenant.Code}, Count: {countResult}",
                                        action: "GetPendingFailedReceipts");
                                    
                                    if (countResult > 0)
                                    {
                                        var receipts = await connection.QueryAsync(pendingFailedReceiptsSql);
                                        var receiptsList = receipts?.ToList() ?? new List<dynamic>();
                                        _smartLogger?.LogWarning(
                                            category: "REPORTS",
                                            message: $"Query result for payment_receipts | TenantCode: {tenant.Code}, ExpectedCount: {countResult}, ActualCount: {receiptsList.Count}",
                                            action: "GetPendingFailedReceipts");
                                        
                                        if (receiptsList.Any())
                                        {
                                            allRecords.AddRange(receiptsList);
                                            _smartLogger?.LogWarning(
                                                category: "REPORTS",
                                                message: $"Added {receiptsList.Count} pending/failed receipts to allRecords | TenantCode: {tenant.Code}, TotalAllRecords: {allRecords.Count}",
                                                action: "GetPendingFailedReceipts");
                                        }
                                        else if (countResult > 0)
                                        {
                                            _smartLogger?.LogWarning(
                                                category: "REPORTS",
                                                message: $"WARNING: Count query found {countResult} records but QueryAsync returned 0 | TenantCode: {tenant.Code}",
                                                action: "GetPendingFailedReceipts");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _smartLogger?.LogError(
                                        category: "REPORTS",
                                        message: $"Error querying payment_receipts | TenantCode: {tenant.Code}, Error: {ex.Message}",
                                        action: "GetPendingFailedReceipts",
                                        exception: ex);
                                }
                            }
                        }

                        // Check and query invoices
                        var invoicesTableExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                            SELECT CASE WHEN EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'invoices'
                            ) THEN 1 ELSE 0 END");

                        if (invoicesTableExists)
                        {
                            var invoicesColumnExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                                SELECT CASE WHEN EXISTS (
                                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                                    WHERE TABLE_SCHEMA = 'dbo' 
                                    AND TABLE_NAME = 'invoices' 
                                    AND COLUMN_NAME = 'status_vom'
                                ) THEN 1 ELSE 0 END");

                            if (invoicesColumnExists)
                            {
                                var invoices = await connection.QueryAsync(pendingFailedInvoicesSql);
                                var invoicesList = invoices?.ToList() ?? new List<dynamic>();
                                if (invoicesList.Any())
                                {
                                    allRecords.AddRange(invoicesList);
                                    _smartLogger?.LogWarning(
                                        category: "REPORTS",
                                        message: $"Found {invoicesList.Count} pending/failed invoices | TenantCode: {tenant.Code}",
                                        action: "GetPendingFailedReceipts");
                                }
                            }
                        }

                        // Check and query credit_notes
                        var creditNotesTableExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                            SELECT CASE WHEN EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'credit_notes'
                            ) THEN 1 ELSE 0 END");

                        if (creditNotesTableExists)
                        {
                            var creditNotesColumnExists = await connection.QueryFirstOrDefaultAsync<bool>(@"
                                SELECT CASE WHEN EXISTS (
                                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                                    WHERE TABLE_SCHEMA = 'dbo' 
                                    AND TABLE_NAME = 'credit_notes' 
                                    AND COLUMN_NAME = 'status_vom'
                                ) THEN 1 ELSE 0 END");

                            if (creditNotesColumnExists)
                            {
                                var creditNotes = await connection.QueryAsync(pendingFailedCreditNotesSql);
                                var creditNotesList = creditNotes?.ToList() ?? new List<dynamic>();
                                if (creditNotesList.Any())
                                {
                                    allRecords.AddRange(creditNotesList);
                                    _smartLogger?.LogWarning(
                                        category: "REPORTS",
                                        message: $"Found {creditNotesList.Count} pending/failed credit notes | TenantCode: {tenant.Code}",
                                        action: "GetPendingFailedReceipts");
                                }
                            }
                        }

                        _smartLogger?.LogWarning(
                            category: "REPORTS",
                            message: $"Total raw records before processing: {allRecords.Count} | TenantCode: {tenant.Code}",
                            action: "GetPendingFailedReceipts");
                        
                        // Helper functions to safely check for DBNull (must be defined before Select)
                        bool IsDbNull(object value) => value == null || Convert.IsDBNull(value);
                        string SafeToString(object value) => IsDbNull(value) ? string.Empty : (value?.ToString() ?? string.Empty);
                        
                        // Debug: Log first record if exists
                        if (allRecords.Any())
                        {
                            var firstRecord = allRecords.First();
                            try
                            {
                                var hasReceiptNo = !IsDbNull(firstRecord.receipt_no);
                                var hasInvoiceNo = !IsDbNull(firstRecord.invoice_no);
                                var hasRecordType = !IsDbNull(firstRecord.record_type);
                                _smartLogger?.LogWarning(
                                    category: "REPORTS",
                                    message: $"First raw record sample | TenantCode: {tenant.Code}, HasReceiptNo: {hasReceiptNo}, HasInvoiceNo: {hasInvoiceNo}, HasRecordType: {hasRecordType}",
                                    action: "GetPendingFailedReceipts");
                            }
                            catch (Exception ex)
                            {
                                _smartLogger?.LogError(
                                    category: "REPORTS",
                                    message: $"Error inspecting first record | TenantCode: {tenant.Code}, Error: {ex.Message}",
                                    action: "GetPendingFailedReceipts");
                            }
                        }
                        
                        var recordsList = allRecords.Select(r =>
                        {
                            // Safely get record type (handle DBNull.Value)
                            string recordTypeValue = string.Empty;
                            if (!IsDbNull(r.record_type))
                            {
                                recordTypeValue = SafeToString(r.record_type).ToLower();
                            }
                            
                            // Determine record type if not set - check which fields exist
                            if (string.IsNullOrEmpty(recordTypeValue))
                            {
                                if (!IsDbNull(r.invoice_no))
                                    recordTypeValue = "invoice";
                                else if (!IsDbNull(r.credit_note_no))
                                    recordTypeValue = "credit_note";
                                else
                                    recordTypeValue = "receipt";
                            }
                            
                            // Get record number - check all possible fields regardless of type
                            string recordNoValue = string.Empty;
                            
                            // First, try to get based on record type
                            if (recordTypeValue == "invoice")
                            {
                                if (!IsDbNull(r.invoice_no))
                                    recordNoValue = SafeToString(r.invoice_no);
                            }
                            else if (recordTypeValue == "credit_note")
                            {
                                if (!IsDbNull(r.credit_note_no))
                                    recordNoValue = SafeToString(r.credit_note_no);
                            }
                            else // receipt
                            {
                                if (!IsDbNull(r.receipt_no))
                                    recordNoValue = SafeToString(r.receipt_no);
                            }
                            
                            // Fallback: if still empty, try all fields
                            if (string.IsNullOrEmpty(recordNoValue))
                            {
                                if (!IsDbNull(r.receipt_no))
                                    recordNoValue = SafeToString(r.receipt_no);
                                else if (!IsDbNull(r.invoice_no))
                                    recordNoValue = SafeToString(r.invoice_no);
                                else if (!IsDbNull(r.credit_note_no))
                                    recordNoValue = SafeToString(r.credit_note_no);
                            }
                            
                            return new
                            {
                                recordId = (!IsDbNull(r.receipt_id) ? (int?)Convert.ToInt32(r.receipt_id) : null) ??
                                          (!IsDbNull(r.invoice_id) ? (int?)Convert.ToInt32(r.invoice_id) : null) ??
                                          (!IsDbNull(r.credit_note_id) ? (int?)Convert.ToInt32(r.credit_note_id) : null) ?? 0,
                                recordNo = recordNoValue,
                                recordType = recordTypeValue,
                                receiptType = SafeToString(r.receipt_type),
                                paymentMethod = SafeToString(r.payment_method),
                                amountPaid = IsDbNull(r.amount_paid) ? 0m : (r.amount_paid ?? 0m),
                                receiptDate = r.receipt_date,
                                receiptStatus = SafeToString(r.receipt_status),
                                statusVoM = SafeToString(r.status_vom),
                                retryCount = IsDbNull(r.vom_retry_count) ? 0 : (r.vom_retry_count ?? 0),
                                voucherCode = SafeToString(r.voucher_code),
                                hotelId = r.hotel_id,
                                zaaerId = r.zaaer_id,
                                createdAt = r.created_at,
                                vomError = SafeToString(r.vom_error)
                            };
                        }).ToList();

                        _smartLogger?.LogWarning(
                            category: "REPORTS",
                            message: $"Querying tenant database | TenantCode: {tenant.Code}, Database: {tenant.DatabaseName}, RawRecords: {allRecords.Count}, ProcessedRecords: {recordsList.Count}",
                            action: "GetPendingFailedReceipts");

                        // Debug: Log sample record to verify structure
                        if (recordsList.Any())
                        {
                            var sampleRecord = recordsList.First();
                            _smartLogger?.LogWarning(
                                category: "REPORTS",
                                message: $"Sample record structure | TenantCode: {tenant.Code}, RecordNo: {sampleRecord.recordNo}, RecordType: {sampleRecord.recordType}, StatusVoM: {sampleRecord.statusVoM}",
                                action: "GetPendingFailedReceipts");
                        }

                        return new
                        {
                            databaseName = tenant.DatabaseName,
                            tenantCode = tenant.Code,
                            tenantName = tenant.Name,
                            receiptCount = recordsList.Count,
                            receipts = recordsList
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[REPORTS] Error processing tenant in pending/failed receipts | TenantCode: {TenantCode}", tenant.Code);
                        _smartLogger?.LogError(
                            category: "REPORTS",
                            message: $"Error processing tenant in pending/failed receipts | TenantCode: {tenant.Code}, Error: {ex.Message}",
                            action: "GetPendingFailedReceipts",
                            exception: ex);
                        return new
                        {
                            databaseName = tenant.DatabaseName,
                            tenantCode = tenant.Code,
                            tenantName = tenant.Name,
                            receiptCount = 0,
                            receipts = new List<object>(),
                            error = ex.Message
                        };
                    }
                });

                var results = (await Task.WhenAll(tenantTasks)).ToList();
                var totalRecords = results.Sum(r => r.receiptCount);

                _smartLogger?.LogWarning(
                    category: "REPORTS",
                    message: $"Pending/Failed receipts completed | TotalDatabases: {results.Count}, TotalRecords: {totalRecords}",
                    action: "GetPendingFailedReceipts");

                return Ok(new
                {
                    success = true,
                    totalDatabases = results.Count,
                    totalRecords = totalRecords,
                    databases = results
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[REPORTS] Error retrieving pending/failed receipts");
                _smartLogger?.LogError(
                    category: "REPORTS",
                    message: $"Error retrieving pending/failed receipts: {ex.Message}",
                    action: "GetPendingFailedReceipts",
                    exception: ex);
                return StatusCode(500, new { message = "Error retrieving pending/failed receipts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get Zaaer integration counts (reservations, customers, payment receipts, invoices, credit notes) from all tenant databases
        /// </summary>
        /// <param name="dateFrom">Start date (YYYY-MM-DD)</param>
        /// <param name="dateTo">End date (YYYY-MM-DD)</param>
        /// <param name="hotelCode">Optional hotel code filter (if not provided, returns all hotels)</param>
        /// <returns>Counts grouped by database</returns>
        [HttpGet("zaaer-integrate")]
        public async Task<IActionResult> GetZaaerIntegrateCounts(
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? hotelCode = null)
        {
            try
            {
                _smartLogger?.LogWarning(
                    category: "REPORTS",
                    message: $"Zaaer integrate request | DateFrom: {dateFrom ?? "NULL"}, DateTo: {dateTo ?? "NULL"}, HotelCode: {hotelCode ?? "ALL"}",
                    action: "GetZaaerIntegrateCounts");

                DateTime? fromDate = null;
                DateTime? toDate = null;

                if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var parsedFromDate))
                {
                    fromDate = parsedFromDate.Date;
                }

                if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var parsedToDate))
                {
                    toDate = parsedToDate.Date.AddDays(1); // Add 1 day to include the entire end date
                }

                var (tenants, scopeError) = await ResolveReportTenantsAsync(hotelCode);
                if (scopeError != null)
                {
                    return scopeError;
                }

                if (tenants == null || tenants.Count == 0)
                {
                    return Ok(new { databases = new List<object>() });
                }

                var databasesData = new List<object>();

                // SQL queries - Count by created_at to match Zaaer system counting method
                // Note: We count by created_at (like Zaaer) but filter details by receipt_date/invoice_date/etc (for display)
                var bookingsSql = @"
                    SELECT COUNT(*)
                    FROM reservations WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                var receiptsSql = @"
                    SELECT COUNT(*)
                    FROM payment_receipts WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                var invoicesSql = @"
                    SELECT COUNT(*)
                    FROM invoices WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                var customersSql = @"
                    SELECT COUNT(*)
                    FROM customers WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                var creditNotesSql = @"
                    SELECT COUNT(*)
                    FROM credit_notes WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                var ordersSql = @"
                    SELECT COUNT(*)
                    FROM orders WITH (NOLOCK)
                    WHERE created_at >= @StartDate
                      AND created_at < @EndDate";

                // Process all tenants in parallel
                var tenantTasks = tenants.Select(async tenant =>
                {
                    try
                    {
                        var connectionString = BuildConnectionStringForTenant(tenant);
                        await using var connection = new SqlConnection(connectionString);
                        await connection.OpenAsync();

                        // Check if date filters are provided
                        DateTime startDate;
                        DateTime endDate;

                        // If no dates provided, use current date as default
                        if (!fromDate.HasValue && !toDate.HasValue)
                        {
                            startDate = DateTime.Now.Date;
                            endDate = startDate.AddDays(1);
                        }
                        else
                        {
                            startDate = fromDate ?? DateTime.MinValue;
                            endDate = toDate ?? DateTime.MaxValue;
                        }

                        var parameters = new DynamicParameters();
                        parameters.Add("@StartDate", startDate, DbType.DateTime2);
                        parameters.Add("@EndDate", endDate, DbType.DateTime2);

                        // Execute all queries in parallel for this tenant
                        var bookingsTask = connection.QueryFirstOrDefaultAsync<int?>(bookingsSql, parameters);
                        var receiptsTask = connection.QueryFirstOrDefaultAsync<int?>(receiptsSql, parameters);
                        var invoicesTask = connection.QueryFirstOrDefaultAsync<int?>(invoicesSql, parameters);
                        var customersTask = connection.QueryFirstOrDefaultAsync<int?>(customersSql, parameters);
                        var creditNotesTask = connection.QueryFirstOrDefaultAsync<int?>(creditNotesSql, parameters);
                        var ordersTask = connection.QueryFirstOrDefaultAsync<int?>(ordersSql, parameters);

                        await Task.WhenAll(bookingsTask, receiptsTask, invoicesTask, customersTask, creditNotesTask, ordersTask);

                        var bookingsCnt = (await bookingsTask) ?? 0;
                        var receiptsCnt = (await receiptsTask) ?? 0;
                        var invoicesCnt = (await invoicesTask) ?? 0;
                        var customersCnt = (await customersTask) ?? 0;
                        var creditNotesCnt = (await creditNotesTask) ?? 0;
                        var ordersCnt = (await ordersTask) ?? 0;

                        _smartLogger?.LogWarning(
                            category: "REPORTS",
                            message: $"Zaaer integrate counts | TenantCode: {tenant.Code}, Database: {tenant.DatabaseName}, Bookings: {bookingsCnt}, Receipts: {receiptsCnt}, Invoices: {invoicesCnt}, Customers: {customersCnt}, CreditNotes: {creditNotesCnt}, Orders: {ordersCnt}",
                            action: "GetZaaerIntegrateCounts");

                        // Only return databases with activity if all counts are 0, skip it (or return it anyway based on requirements)
                        return new
                        {
                            databaseName = tenant.DatabaseName,
                            tenantCode = tenant.Code,
                            tenantName = tenant.Name,
                            bookingsCnt = bookingsCnt,
                            receiptsCnt = receiptsCnt,
                            invoicesCnt = invoicesCnt,
                            customersCnt = customersCnt,
                            creditNotesCnt = creditNotesCnt,
                            ordersCnt = ordersCnt
                        };
                    }
                    catch (Exception ex)
                    {
                        _smartLogger?.LogError(
                            category: "REPORTS",
                            message: $"Error processing tenant in Zaaer integrate | TenantCode: {tenant.Code}, Error: {ex.Message}",
                            action: "GetZaaerIntegrateCounts",
                            exception: ex);
                        // Return null for failed tenant - will be filtered out
                        return null;
                    }
                });

                // Wait for all tenants to complete in parallel
                var tenantResults = await Task.WhenAll(tenantTasks);

                // Filter out null results (failed tenants) and convert to list
                var validResults = tenantResults.Where(r => r != null).ToList();

                // Calculate totals from valid results (excluding any future TOTAL rows)
                int totalBookings = 0;
                int totalReceipts = 0;
                int totalInvoices = 0;
                int totalCustomers = 0;
                int totalCreditNotes = 0;
                int totalOrders = 0;

                foreach (var result in validResults)
                {
                    // Add result to list first
                    databasesData.Add(result!);
                    
                    // Use dynamic to access properties for totals calculation
                    dynamic db = result!;
                    // Skip TOTAL rows in calculation
                    if (db.databaseName?.ToString() != "TOTAL" && db.tenantCode?.ToString() != "TOTAL")
                    {
                        totalBookings += db.bookingsCnt ?? 0;
                        totalReceipts += db.receiptsCnt ?? 0;
                        totalInvoices += db.invoicesCnt ?? 0;
                        totalCustomers += db.customersCnt ?? 0;
                        totalCreditNotes += db.creditNotesCnt ?? 0;
                        totalOrders += db.ordersCnt ?? 0;
                    }
                }

                // Add TOTAL row at the end
                databasesData.Add(new
                {
                    databaseName = "TOTAL",
                    tenantCode = "TOTAL",
                    tenantName = "TOTAL",
                    bookingsCnt = totalBookings,
                    receiptsCnt = totalReceipts,
                    invoicesCnt = totalInvoices,
                    customersCnt = totalCustomers,
                    creditNotesCnt = totalCreditNotes,
                    ordersCnt = totalOrders
                });

                return Ok(new { databases = databasesData });
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "REPORTS",
                    message: $"Error generating Zaaer integrate report | DateFrom: {dateFrom}, DateTo: {dateTo}, Error: {ex.Message}",
                    action: "GetZaaerIntegrateCounts",
                    exception: ex);
                return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء التقرير", error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed records for Zaaer integration (reservations, customers, payment receipts, invoices, credit notes)
        /// </summary>
        [HttpGet("zaaer-integrate-details")]
        public async Task<IActionResult> GetZaaerIntegrateDetails(
            [FromQuery] string recordType,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? hotelCode = null)
        {
            try
            {
                _smartLogger?.LogWarning(
                    category: "REPORTS",
                    message: $"Zaaer integrate details request | RecordType: {recordType}, DateFrom: {dateFrom ?? "NULL"}, DateTo: {dateTo ?? "NULL"}, HotelCode: {hotelCode ?? "ALL"}",
                    action: "GetZaaerIntegrateDetails");

                DateTime? fromDate = null;
                DateTime? toDate = null;

                if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var parsedFromDate))
                {
                    fromDate = parsedFromDate.Date;
                }

                if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var parsedToDate))
                {
                    toDate = parsedToDate.Date.AddDays(1);
                }

                var (tenants, scopeError) = await ResolveReportTenantsAsync(hotelCode);
                if (scopeError != null)
                {
                    return scopeError;
                }

                if (tenants == null || tenants.Count == 0)
                {
                    return Ok(new { records = new List<object>() });
                }

                var allRecords = new List<dynamic>();

                // Build SQL based on record type
                string sql = recordType.ToLower() switch
                {
                    "reservations" or "bookings" => @"
                        SELECT 
                            reservation_id AS RecordId,
                            reservation_no AS RecordNo,
                            reservation_date AS RecordDate,
                            created_at AS CreatedAt,
                            customer_id AS CustomerId,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            ISNULL(total_amount, 0) AS Amount
                        FROM reservations WITH (NOLOCK)
                        WHERE reservation_date >= @StartDate
                          AND reservation_date < @EndDate
                        ORDER BY reservation_date DESC",
                    "customers" => @"
                        SELECT 
                            customer_id AS RecordId,
                            customer_no AS RecordNo,
                            entered_at AS RecordDate,
                            created_at AS CreatedAt,
                            customer_name AS CustomerName,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            mobile_no AS MobileNo
                        FROM customers WITH (NOLOCK)
                        WHERE entered_at >= @StartDate
                          AND entered_at < @EndDate
                        ORDER BY entered_at DESC",
                    "receipts" or "payment_receipts" => @"
                        SELECT 
                            receipt_id AS RecordId,
                            receipt_no AS RecordNo,
                            receipt_date AS RecordDate,
                            created_at AS CreatedAt,
                            ISNULL(amount_paid, 0) AS Amount,
                            payment_method AS PaymentMethod,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            receipt_status AS Status
                        FROM payment_receipts WITH (NOLOCK)
                        WHERE receipt_date >= @StartDate
                          AND receipt_date < @EndDate
                        ORDER BY receipt_date DESC",
                    "invoices" => @"
                        SELECT 
                            invoice_id AS RecordId,
                            invoice_no AS RecordNo,
                            invoice_date AS RecordDate,
                            created_at AS CreatedAt,
                            ISNULL(total_amount, 0) AS Amount,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            payment_status AS Status
                        FROM invoices WITH (NOLOCK)
                        WHERE invoice_date >= @StartDate
                          AND invoice_date < @EndDate
                        ORDER BY invoice_date DESC",
                    "credit_notes" or "creditnotes" => @"
                        SELECT 
                            credit_note_id AS RecordId,
                            credit_note_no AS RecordNo,
                            credit_note_date AS RecordDate,
                            created_at AS CreatedAt,
                            ISNULL(credit_amount, 0) AS Amount,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            'active' AS Status
                        FROM credit_notes WITH (NOLOCK)
                        WHERE credit_note_date >= @StartDate
                          AND credit_note_date < @EndDate
                        ORDER BY credit_note_date DESC",
                    "orders" => @"
                        SELECT 
                            order_id AS RecordId,
                            order_no AS RecordNo,
                            order_date AS RecordDate,
                            created_at AS CreatedAt,
                            ISNULL(total_amount, 0) AS Amount,
                            order_status AS OrderStatus,
                            payment_status AS PaymentStatus,
                            order_type AS OrderType,
                            ISNULL(subtotal, 0) AS Subtotal,
                            ISNULL(tax_amount, 0) AS TaxAmount,
                            ISNULL(discount_amount, 0) AS DiscountAmount,
                            ISNULL(paid_amount, 0) AS PaidAmount,
                            ISNULL(balance, 0) AS Balance,
                            hotel_id AS HotelId,
                            zaaer_id AS ZaaerId,
                            customer_id AS CustomerId,
                            reservation_id AS ReservationId,
                            outlet_id AS OutletId,
                            table_id AS TableId
                        FROM orders WITH (NOLOCK)
                        WHERE order_date >= @StartDate
                          AND order_date < @EndDate
                        ORDER BY order_date DESC",
                    _ => throw new ArgumentException($"Invalid record type: {recordType}")
                };

                // Process all tenants in parallel
                var tenantTasks = tenants.Select(async tenant =>
                {
                    try
                    {
                        var connectionString = BuildConnectionStringForTenant(tenant);
                        await using var connection = new SqlConnection(connectionString);
                        await connection.OpenAsync();

                        DateTime startDate;
                        DateTime endDate;

                        // If no dates provided, use current date as default
                        if (!fromDate.HasValue && !toDate.HasValue)
                        {
                            startDate = DateTime.Now.Date;
                            endDate = startDate.AddDays(1);
                        }
                        else
                        {
                            startDate = fromDate ?? DateTime.MinValue;
                            endDate = toDate ?? DateTime.MaxValue;
                        }

                        var parameters = new DynamicParameters();
                        parameters.Add("@StartDate", startDate, DbType.DateTime2);
                        parameters.Add("@EndDate", endDate, DbType.DateTime2);

                        var records = await connection.QueryAsync(sql, parameters);
                        var recordsList = records?.ToList() ?? new List<dynamic>();

                        // Add tenant information to each record
                        foreach (var record in recordsList)
                        {
                            ((IDictionary<string, object>)record)["DatabaseName"] = tenant.DatabaseName;
                            ((IDictionary<string, object>)record)["TenantCode"] = tenant.Code;
                            ((IDictionary<string, object>)record)["TenantName"] = tenant.Name;
                            ((IDictionary<string, object>)record)["RecordType"] = recordType;
                        }

                        return recordsList;
                    }
                    catch (Exception ex)
                    {
                        _smartLogger?.LogError(
                            category: "REPORTS",
                            message: $"Error querying tenant details | TenantCode: {tenant.Code}, RecordType: {recordType}, Error: {ex.Message}",
                            action: "GetZaaerIntegrateDetails",
                            exception: ex);
                        return new List<dynamic>();
                    }
                });

                var tenantResults = await Task.WhenAll(tenantTasks);
                foreach (var result in tenantResults)
                {
                    allRecords.AddRange(result);
                }

                return Ok(new { records = allRecords, recordType = recordType });
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "REPORTS",
                    message: $"Error retrieving Zaaer integrate details | RecordType: {recordType}, Error: {ex.Message}",
                    action: "GetZaaerIntegrateDetails",
                    exception: ex);
                return StatusCode(500, new { message = "Error retrieving details", error = ex.Message });
            }
        }

        /// <summary>
        /// Returns tax rule configuration used by payment daily net reports.
        /// </summary>
        [HttpGet("payment-daily-net-ex-tax-rules")]
        public IActionResult GetPaymentDailyNetExTaxRules()
        {
            return Ok(new
            {
                vatOnlyHotelCodes = _paymentDailyNetExTaxOptions.VatOnlyHotelCodes,
                vatOnlyTenantIds = _paymentDailyNetExTaxOptions.VatOnlyTenantIds,
                vatOnlyZaaerIds = _paymentDailyNetExTaxOptions.VatOnlyZaaerIds,
                taxVatFactor = PaymentDailyNetExTaxHelper.TaxVatFactor,
                taxGrossWithVatAndLodging = PaymentDailyNetExTaxHelper.TaxGrossWithVatAndLodging
            });
        }

        /// <summary>
        /// Returns payment method totals across accessible hotels for the selected date range.
        /// </summary>
        [HttpGet("payment-method-summary")]
public async Task<IActionResult> GetPaymentMethodSummary([FromQuery] string dateFrom, [FromQuery] string dateTo, [FromQuery] string? hotelCodes = null)
{
    try
    {
        if (string.IsNullOrWhiteSpace(dateFrom) || string.IsNullOrWhiteSpace(dateTo))
        {
            return BadRequest(new { message = "يجب تحديد تاريخ البداية والنهاية" });
        }

        if (!DateTime.TryParse(dateFrom, out var fromDate) || !DateTime.TryParse(dateTo, out var toDate))
        {
            return BadRequest(new { message = "صيغة التاريخ غير صحيحة. استخدم YYYY-MM-DD" });
        }

        var (tenants, scopeError) = await ResolveReportTenantsAsync(hotelCodes);
        if (scopeError != null)
        {
            return scopeError;
        }

        if (tenants == null || tenants.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        // Build dynamic SQL for all tenants at once (much faster)
        var sqlParts = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("@DateFrom", fromDate.Date);
        parameters.Add("@DateTo", toDate.Date);

        foreach (var tenant in tenants)
        {
            var tenantParamName = $"@Tenant_{tenant.Id}";
            parameters.Add(tenantParamName, tenant.Code);

            sqlParts.Add($@"
                SELECT
                    {tenantParamName} as TenantName,
                    pr.payment_method,
                    SUM(CASE
                            WHEN pr.receipt_status = 'paid'
                             AND ISNULL(pr.voucher_code,'') <> 'transfers_to_bank'
                            THEN pr.amount_paid
                            ELSE 0
                        END) AS ReceiptsNet,
                    SUM(CASE
                            WHEN pr.receipt_status = 'paid'
                             AND pr.voucher_code = 'transfers_to_bank'
                             AND pr.bank_name = 'expense'
                            THEN pr.amount_paid
                            ELSE 0
                        END) AS Expenses,
                    SUM(CASE
                            WHEN pr.receipt_status = 'paid'
                             AND pr.voucher_code = 'transfers_to_bank'
                             AND pr.bank_name = 'bilad'
                            THEN pr.amount_paid
                            ELSE 0
                        END) AS Deposits_Bilad,
                    SUM(CASE
                            WHEN pr.receipt_status = 'paid'
                             AND pr.voucher_code = 'transfers_to_bank'
                             AND pr.bank_name = 'riyad'
                            THEN pr.amount_paid
                            ELSE 0
                        END) AS Deposits_Riyad
                FROM [{tenant.DatabaseName}].dbo.payment_receipts pr WITH (NOLOCK)
                WHERE CAST(pr.receipt_date as date) >= @DateFrom
                  AND CAST(pr.receipt_date as date) <= @DateTo
                GROUP BY pr.payment_method");
        }

        // Combine all tenant queries with UNION ALL
        var finalSql = string.Join(" UNION ALL ", sqlParts) + " ORDER BY TenantName, payment_method";

        // Execute single query against master database with cross-database access
        var masterConnectionString = _configuration["ConnectionStrings:MasterDb"];
        var allResults = new List<dynamic>();

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync(finalSql, parameters);
        allResults.AddRange(results);

        // Calculate totals in memory (much faster)
        var tenantTotals = allResults
            .GroupBy(r => (string)r.TenantName)
            .Select(g => new
            {
                TenantName = g.Key,
                TotalNet = g.Sum(r => (decimal)r.ReceiptsNet),
                Expenses = g.Sum(r => (decimal)r.Expenses),
                Deposits_Bilad = g.Sum(r => (decimal)r.Deposits_Bilad),
                Deposits_Riyad = g.Sum(r => (decimal)r.Deposits_Riyad)
            })
            .ToDictionary(t => t.TenantName, t => t);

        // Build final results
        var finalResults = allResults.Select(r => {
            var tenantName = (string)r.TenantName;
            var tenantData = tenantTotals.ContainsKey(tenantName) ? tenantTotals[tenantName] : null;

            return new
            {
                TenantName = tenantName,
                PaymentMethod = r.payment_method ?? "غير محدد",
                ReceiptsNet = r.ReceiptsNet ?? 0m,
                Expenses = tenantData?.Expenses ?? 0m,
                Deposits_Bilad = tenantData?.Deposits_Bilad ?? 0m,
                Deposits_Riyad = tenantData?.Deposits_Riyad ?? 0m,
                TotalNet = tenantData?.TotalNet ?? 0m
            };
        }).ToList();

        return Ok(new {
            success = true,
            data = finalResults,
            dateFrom = fromDate.Date,
            dateTo = toDate.Date,
            summary = new {
                totalHotels = tenantTotals.Count,
                totalReceiptsNet = tenantTotals.Sum(t => t.Value.TotalNet),
                totalExpenses = tenantTotals.Sum(t => t.Value.Expenses),
                totalDepositsBilad = tenantTotals.Sum(t => t.Value.Deposits_Bilad),
                totalDepositsRiyad = tenantTotals.Sum(t => t.Value.Deposits_Riyad)
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating payment method summary: {Message}", ex.Message);
        return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء التقرير", error = ex.Message });
    }
}
    }
}

