using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using zaaerIntegration.DTOs.Expense;
using zaaerIntegration.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace zaaerIntegration.Services.Expense
{
    /// <summary>
    /// Service optimized with Dapper for heavy expense queries
    /// Uses raw SQL for maximum performance
    /// </summary>
    public class ExpenseDapperService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpenseDapperService> _logger;
        private readonly MasterDbContext _masterDbContext;

        public ExpenseDapperService(
            IConfiguration configuration,
            ILogger<ExpenseDapperService> logger,
            MasterDbContext masterDbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _masterDbContext = masterDbContext;
        }

        /// <summary>
        /// Get hotel summary using optimized Dapper queries with parallel execution
        /// </summary>
        public async Task<IEnumerable<ExpenseAnalyticsHotelTableDto>> GetSupervisorHotelSummaryAsync(
            DateTime fromDate,
            DateTime toDate,
            int? expenseCategoryId = null,
            string? approvalStatus = null,
            int? userId = null,
            bool hasFullAccess = false)
        {
            // Normalize dates to KSA date range
            var fromKsa = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var toKsa = new DateTime(toDate.Year, toDate.Month, toDate.Day, 23, 59, 59, DateTimeKind.Utc);

            try
            {
                _logger.LogInformation("🚀 [ExpenseDapperService] GetSupervisorHotelSummaryAsync started. UserId={UserId}, HasFullAccess={HasFullAccess}, FromDate={FromDate}, ToDate={ToDate}",
                    userId, hasFullAccess, fromKsa, toKsa);
                
                // Get all hotels for this supervisor from Master DB (optimized)
                var supervisorHotels = hasFullAccess
                    ? await _masterDbContext.Tenants
                        .AsNoTracking()
                        .Select(t => new
                        {
                            t.Id,
                            t.Code,
                            t.Name,
                            t.DatabaseName
                        })
                        .ToListAsync()
                    : await (from ut in _masterDbContext.UserTenants
                             join t in _masterDbContext.Tenants on ut.TenantId equals t.Id
                             where ut.UserId == userId
                             select new
                             {
                                 t.Id,
                                 t.Code,
                                 t.Name,
                                 t.DatabaseName
                             })
                             .ToListAsync();

                _logger.LogInformation("📊 [ExpenseDapperService] Found {Count} hotels for supervisor", supervisorHotels.Count);
                
                if (!supervisorHotels.Any())
                {
                    _logger.LogWarning("⚠️ [ExpenseDapperService] No hotels found for supervisor UserId={UserId}", userId);
                    return new List<ExpenseAnalyticsHotelTableDto>();
                }

                // Get database connection settings
                var server = _configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = _configuration["TenantDatabase:UserId"]?.Trim();
                var dbPassword = _configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(dbPassword))
                {
                    throw new InvalidOperationException("TenantDatabase settings are not configured correctly.");
                }

                // Build SQL query parameters
                var sqlParams = new
                {
                    FromDate = fromKsa,
                    ToDate = toKsa,
                    ExpenseCategoryId = expenseCategoryId,
                    ApprovalStatus = approvalStatus?.ToLower() == "all" ? null : approvalStatus?.ToLower()
                };

                // Execute queries in parallel for all hotels (much faster!)
                var tasks = supervisorHotels
                    .Where(h => !string.IsNullOrWhiteSpace(h.DatabaseName))
                    .Select(hotel => Task.Run(async () =>
                    {
                        try
                        {
                            // Build connection string
                            var connectionString =
                                $"Server={server}; Database={hotel.DatabaseName}; User Id={dbUserId}; Password={dbPassword}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True; Connection Timeout=30; Command Timeout=60;";

                            // Build optimized SQL query
                            // Note: Using actual column names from database (snake_case)
                            // Fixed: GROUP BY - cannot use parameters, must use actual columns
                            var sql = @"
                                SELECT 
                                    ISNULL(HS.hotel_name, @HotelName) AS HotelName,
                                    ISNULL(HS.hotel_code, @HotelCode) AS HotelCode,
                                    COUNT(*) AS Count,
                                    ISNULL(SUM(E.total_amount), 0) AS Amount,
                                    ISNULL(SUM(E.tax_amount), 0) AS TotalTaxAmount,
                                    CASE 
                                        WHEN COUNT(*) > 0 THEN ISNULL(SUM(E.total_amount), 0) * 1.0 / COUNT(*)
                                        ELSE 0
                                    END AS Average
                                FROM expenses E
                                LEFT JOIN hotel_settings HS ON E.hotel_id = HS.hotel_id
                                WHERE E.date_time >= @FromDate 
                                    AND E.date_time <= @ToDate
                                    AND (@ExpenseCategoryId IS NULL OR E.expense_category_id = @ExpenseCategoryId)
                                    AND (@ApprovalStatus IS NULL OR LOWER(E.approval_status) = @ApprovalStatus)
                                GROUP BY HS.hotel_name, HS.hotel_code";

                            using var connection = new SqlConnection(connectionString);
                            await connection.OpenAsync();

                            _logger.LogInformation("🔍 [ExpenseDapperService] Querying hotel {HotelCode} with params: FromDate={FromDate}, ToDate={ToDate}, CategoryId={CategoryId}, Status={Status}",
                                hotel.Code, sqlParams.FromDate, sqlParams.ToDate, sqlParams.ExpenseCategoryId, sqlParams.ApprovalStatus);

                            var queryResults = await connection.QueryAsync(sql, new
                            {
                                HotelName = hotel.Name ?? "غير معروف",
                                HotelCode = hotel.Code ?? "",
                                sqlParams.FromDate,
                                sqlParams.ToDate,
                                sqlParams.ExpenseCategoryId,
                                sqlParams.ApprovalStatus
                            });

                            var rawResults = queryResults.ToList();
                            _logger.LogInformation("📊 [ExpenseDapperService] Raw query returned {Count} rows for hotel {HotelCode} (Database: {Database})", 
                                rawResults.Count, hotel.Code, hotel.DatabaseName);

                            // Log first result if any to debug
                            if (rawResults.Any())
                            {
                                var first = rawResults.First();
                                try
                                {
                                    var hotelName = first?.HotelName?.ToString() ?? "NULL";
                                    var count = first?.Count?.ToString() ?? "0";
                                    var amount = first?.Amount?.ToString() ?? "0";
                                    _logger.LogInformation($"📊 [ExpenseDapperService] Sample result for {hotel.Code}: HotelName={hotelName}, Count={count}, Amount={amount}");
                                }
                                catch
                                {
                                    // If logging fails, skip it
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️ [ExpenseDapperService] No results for hotel {hotel.Code} (Database: {hotel.DatabaseName}) with params: FromDate={sqlParams.FromDate}, ToDate={sqlParams.ToDate}, CategoryId={sqlParams.ExpenseCategoryId}, Status={sqlParams.ApprovalStatus}");
                            }

                            var results = rawResults.Select(r => new ExpenseAnalyticsHotelTableDto
                            {
                                HotelName = r.HotelName ?? hotel.Name ?? "غير معروف",
                                HotelCode = r.HotelCode ?? hotel.Code,
                                Count = r.Count,
                                Amount = r.Amount,
                                TotalTaxAmount = r.TotalTaxAmount,
                                Average = r.Average
                            });

                            return results.ToList();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ [ExpenseDapperService] Error querying hotel {HotelCode}: {Message}", hotel.Code, ex.Message);
                            return new List<ExpenseAnalyticsHotelTableDto>();
                        }
                    }));

                // Wait for all parallel queries to complete
                var allResults = await Task.WhenAll(tasks);
                var flatResults = allResults.SelectMany(r => r).ToList();
                
                _logger.LogInformation("📊 [ExpenseDapperService] All parallel queries completed. Total raw results: {Count}", flatResults.Count);

                // Group by hotel (in case same hotel appears multiple times) and aggregate
                var grouped = flatResults
                    .GroupBy(r => new { r.HotelName, r.HotelCode })
                    .Select(g => new ExpenseAnalyticsHotelTableDto
                    {
                        HotelName = g.Key.HotelName ?? "غير معروف",
                        HotelCode = g.Key.HotelCode,
                        Count = g.Sum(x => x.Count),
                        Amount = g.Sum(x => x.Amount),
                        TotalTaxAmount = g.Sum(x => x.TotalTaxAmount),
                        Average = g.Sum(x => x.Count) > 0 ? g.Sum(x => x.Amount) / g.Sum(x => x.Count) : 0
                    })
                    .OrderByDescending(x => x.Amount)
                    .Select((g, index) => new ExpenseAnalyticsHotelTableDto
                    {
                        Rank = index + 1,
                        HotelName = g.HotelName,
                        HotelCode = g.HotelCode,
                        Count = g.Count,
                        Amount = g.Amount,
                        TotalTaxAmount = g.TotalTaxAmount,
                        Average = g.Average
                    })
                    .ToList();

                _logger.LogInformation("✅ [ExpenseDapperService] Final result: {Count} hotels after grouping and ranking", grouped.Count);
                return grouped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExpenseDapperService] Error in GetSupervisorHotelSummaryAsync");
                throw;
            }
        }

        /// <summary>
        /// Get hotel expenses using optimized Dapper query
        /// </summary>
        public async Task<IEnumerable<ExpenseResponseDto>> GetSupervisorHotelExpensesAsync(
            string hotelCode,
            DateTime fromDate,
            DateTime toDate,
            int? expenseCategoryId = null,
            string? approvalStatus = null)
        {
            // Normalize dates
            var fromKsa = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var toKsa = new DateTime(toDate.Year, toDate.Month, toDate.Day, 23, 59, 59, DateTimeKind.Utc);

            try
            {
                // Get hotel info from Master DB
                var hotel = await _masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code.ToLower() == hotelCode.ToLower());

                if (hotel == null || string.IsNullOrWhiteSpace(hotel.DatabaseName))
                {
                    _logger.LogWarning("⚠️ [ExpenseDapperService] Hotel not found: {HotelCode}", hotelCode);
                    return new List<ExpenseResponseDto>();
                }

                // Get database connection settings
                var server = _configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = _configuration["TenantDatabase:UserId"]?.Trim();
                var dbPassword = _configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(dbPassword))
                {
                    throw new InvalidOperationException("TenantDatabase settings are not configured correctly.");
                }

                // ✅ FIX: Get HotelSettings separately to get ZaaerId
                // First, get HotelSettings for this hotelCode to find the correct ZaaerId
                var hotelSettingsConnectionString =
                    $"Server={server}; Database={hotel.DatabaseName}; User Id={dbUserId}; Password={dbPassword}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True; Connection Timeout=30; Command Timeout=60;";
                
                int? hotelZaaerId = null;
                string? hotelName = null;
                using (var hotelSettingsConnection = new SqlConnection(hotelSettingsConnectionString))
                {
                    await hotelSettingsConnection.OpenAsync();
                    var hotelSettingsSql = @"
                        SELECT hotel_id, hotel_name, hotel_code, zaaer_id
                        FROM hotel_settings
                        WHERE LOWER(hotel_code) = LOWER(@HotelCode)";
                    var hotelSettingsResult = await hotelSettingsConnection.QueryFirstOrDefaultAsync(hotelSettingsSql, new { HotelCode = hotelCode });
                    if (hotelSettingsResult != null)
                    {
                        hotelZaaerId = hotelSettingsResult.zaaer_id;
                        hotelName = hotelSettingsResult.hotel_name;
                        _logger.LogInformation("✅ [ExpenseDapperService] Found HotelSettings: HotelCode={HotelCode}, ZaaerId={ZaaerId}, HotelName={HotelName}", 
                            hotelCode, hotelZaaerId, hotelName);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [ExpenseDapperService] HotelSettings not found for HotelCode={HotelCode}, will search without hotel filter", hotelCode);
                        // If HotelSettings not found, we'll search without hotel filter (for backward compatibility)
                    }
                }

                // Build optimized SQL query
                // ✅ FIX: Use ZaaerId to filter expenses (expenses.hotel_id contains zaaer_id, not hotel_id PK)
                // Note: ExpenseCategoryName will be populated from Master DB later (ExpenseCategories are in Master DB, not Tenant DB)
                // Using actual column names from database (snake_case)
                var sql = @"
                    SELECT 
                        E.expense_id AS ExpenseId,
                        E.hotel_id AS HotelId,
                        E.date_time AS DateTime,
                        E.due_date AS DueDate,
                        E.total_amount AS TotalAmount,
                        E.tax_amount AS TaxAmount,
                        E.tax_rate AS TaxRate,
                        E.comment AS Comment,
                        E.approval_status AS ApprovalStatus,
                        E.expense_category_id AS ExpenseCategoryId,
                        E.created_at AS CreatedAt,
                        E.updated_at AS UpdatedAt,
                        E.created_by AS CreatedBy,
                        E.updated_by AS UpdatedBy,
                        E.approved_by AS ApprovedBy,
                        E.approved_at AS ApprovedAt,
                        E.rejection_reason AS RejectionReason,
                        E.payment_source AS PaymentSource,
                        NULL AS ExpenseCategoryName,
                        @HotelName AS HotelName,
                        @HotelCode AS HotelCode
                    FROM expenses E
                    WHERE E.date_time >= @FromDate 
                        AND E.date_time <= @ToDate
                        AND (@HotelZaaerId IS NULL OR E.hotel_id = @HotelZaaerId)
                        AND (@ExpenseCategoryId IS NULL OR E.expense_category_id = @ExpenseCategoryId)
                        AND (@ApprovalStatus IS NULL OR LOWER(E.approval_status) = @ApprovalStatus)
                    ORDER BY E.date_time DESC";

                var connectionString =
                    $"Server={server}; Database={hotel.DatabaseName}; User Id={dbUserId}; Password={dbPassword}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True; Connection Timeout=30; Command Timeout=60;";

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var results = await connection.QueryAsync<ExpenseResponseDto>(sql, new
                {
                    FromDate = fromKsa,
                    ToDate = toKsa,
                    HotelZaaerId = hotelZaaerId,
                    ExpenseCategoryId = expenseCategoryId,
                    ApprovalStatus = approvalStatus?.ToLower() == "all" ? null : approvalStatus?.ToLower(),
                    HotelName = hotelName ?? hotel.Name ?? "غير معروف",
                    HotelCode = hotelCode
                });

                var expensesList = results.ToList();
                
                // ✅ Get category names from Master DB
                var categoryIds = expensesList
                    .Where(e => e.ExpenseCategoryId.HasValue)
                    .Select(e => e.ExpenseCategoryId!.Value)
                    .Distinct()
                    .ToList();
                    
                if (categoryIds.Any())
                {
                    var categories = await _masterDbContext.ExpenseCategories
                        .AsNoTracking()
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, c => c.MainCategory);
                    
                    // Populate category names
                    foreach (var expense in expensesList)
                    {
                        if (expense.ExpenseCategoryId.HasValue && categories.TryGetValue(expense.ExpenseCategoryId.Value, out var categoryName))
                        {
                            expense.ExpenseCategoryName = categoryName;
                        }
                    }
                }

                // ✅ Load ExpenseRooms for all expenses
                var expenseIds = expensesList.Select(e => e.ExpenseId).ToList();
                if (expenseIds.Any())
                {
                    var expenseRoomsSql = @"
                        SELECT 
                            ER.expense_room_id AS ExpenseRoomId,
                            ER.expense_id AS ExpenseId,
                            ER.zaaer_id AS ZaaerId,
                            ER.purpose AS Purpose,
                            ER.amount AS Amount,
                            ER.created_at AS CreatedAt,
                            A.apartment_id AS ApartmentId,
                            A.apartment_code AS ApartmentCode,
                            A.apartment_name AS ApartmentName
                        FROM expense_rooms ER
                        LEFT JOIN apartments A ON ER.zaaer_id = A.zaaer_id
                        WHERE ER.expense_id IN @ExpenseIds";
                    
                    var expenseRooms = await connection.QueryAsync(expenseRoomsSql, new { ExpenseIds = expenseIds });
                    
                    // Group ExpenseRooms by ExpenseId
                    var expenseRoomsDict = expenseRooms
                        .GroupBy(er => (long)er.ExpenseId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(er => new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                ZaaerId = er.ZaaerId,
                                Purpose = er.Purpose,
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt,
                                ApartmentId = er.ApartmentId,
                                ApartmentCode = er.ApartmentCode,
                                ApartmentName = er.ApartmentName
                            }).ToList());
                    
                    // Assign ExpenseRooms to each expense
                    foreach (var expense in expensesList)
                    {
                        if (expenseRoomsDict.TryGetValue(expense.ExpenseId, out var rooms))
                        {
                            expense.ExpenseRooms = rooms;
                        }
                        else
                        {
                            expense.ExpenseRooms = new List<ExpenseRoomResponseDto>();
                        }
                    }
                }
                else
                {
                    // No expenses, so no ExpenseRooms needed
                    foreach (var expense in expensesList)
                    {
                        expense.ExpenseRooms = new List<ExpenseRoomResponseDto>();
                    }
                }

                _logger.LogInformation("✅ [ExpenseDapperService] Retrieved {Count} expenses for hotel {HotelCode} with ExpenseRooms", expensesList.Count, hotelCode);
                return expensesList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExpenseDapperService] Error in GetSupervisorHotelExpensesAsync");
                throw;
            }
        }
    }
}

