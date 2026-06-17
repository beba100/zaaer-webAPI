using FinanceLedgerAPI.Models;
using ExpenseModel = FinanceLedgerAPI.Models.Expense;
using ExpenseRoomModel = FinanceLedgerAPI.Models.ExpenseRoom;
using ExpenseCategoryModel = FinanceLedgerAPI.Models.ExpenseCategory;
using ExpenseApprovalHistoryModel = FinanceLedgerAPI.Models.ExpenseApprovalHistory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Expense;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Expense
{
    /// <summary>
    /// Service لإدارة النفقات (Expenses)
    /// يستخدم ITenantService للحصول على HotelId من X-Hotel-Code header
    /// يستخدم Unit of Work pattern للوصول إلى قاعدة البيانات
    /// </summary>
    public class ExpenseService : IExpenseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context; // For complex queries with Include
        private readonly ITenantService _tenantService;
        private readonly ILogger<ExpenseService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICurrentUserContext _currentUser;
        private readonly MasterDbContext _masterDbContext;
        private readonly IExpenseApprovalRuleService _ruleService;
        private readonly ExpenseDapperService _dapperService; // For optimized heavy queries
        private readonly INumberingService _numberingService;

        private bool? _expenseIdUsesIdentity;

        private static readonly HashSet<string> EditableApprovalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pending",
            "awaiting-manager",
            "awaiting-accountant",
            "awaiting-admin",
            "awaiting-officer",
            "awaiting-verifier",
            "rejected",
            "auto-approved"
        };

        private static bool IsEditableApprovalStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return true; // Treat null/empty as pending (editable)
            }

            return EditableApprovalStatuses.Contains(status.Trim());
        }

        /// <summary>
        /// Constructor for ExpenseService
        /// </summary>
        /// <param name="unitOfWork">Unit of Work for database operations</param>
        /// <param name="context">Application database context (for complex queries with Include)</param>
        /// <param name="tenantService">Tenant service for getting current hotel</param>
        /// <param name="logger">Logger</param>
        /// <param name="configuration">Configuration for reading app settings</param>
        /// <param name="httpContextAccessor">HTTP context accessor for getting current user</param>
        /// <param name="masterDbContext">Master database context for getting user info</param>
        /// <param name="ruleService">Expense approval rule service</param>
        /// <param name="dapperService">Dapper service for optimized heavy queries</param>
        /// <param name="numberingService">Central Master DB numbering</param>
        public ExpenseService(
            IUnitOfWork unitOfWork,
            ApplicationDbContext context,
            ITenantService tenantService,
            ILogger<ExpenseService> logger,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserContext currentUser,
            MasterDbContext masterDbContext,
            IExpenseApprovalRuleService ruleService,
            ExpenseDapperService dapperService,
            INumberingService numberingService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
            _dapperService = dapperService ?? throw new ArgumentNullException(nameof(dapperService));
            _numberingService = numberingService ?? throw new ArgumentNullException(nameof(numberingService));
        }

        /// <summary>
        /// الحصول على HotelId من Tenant (يُقرأ من X-Hotel-Code header)
        /// 1. يحصل على Tenant.Code و ZaaerId من Master DB (Tenants table)
        /// 2. يبحث عن HotelSettings في Tenant DB باستخدام HotelCode == Tenant.Code
        /// 3. ✅ يتحقق من تطابق hotel_settings.zaaer_id مع Tenants.ZaaerId في Master DB
        /// 4. ✅ يستخدم HotelSettings.ZaaerId كـ hotel_id في expenses table
        /// </summary>
        private async Task<int> GetCurrentHotelIdAsync()
        {
            _logger.LogInformation("🔍 [GetCurrentHotelIdAsync] ===== START ======");
            var tenant = _tenantService.GetTenant();
            if (tenant == null)
            {
                _logger.LogError("❌ [GetCurrentHotelIdAsync] Tenant not resolved");
                throw new InvalidOperationException("Tenant not resolved. Cannot get hotel ID.");
            }

            _logger.LogInformation("🔍 [GetCurrentHotelIdAsync] Tenant Code: {TenantCode}, DatabaseName: {DatabaseName}", 
                tenant.Code, tenant.DatabaseName);

            // ✅ البحث عن HotelSettings في Tenant DB باستخدام Tenant.Code من Master DB
            _logger.LogInformation("🔍 [GetCurrentHotelIdAsync] Searching for HotelSettings with HotelCode = '{TenantCode}' (case-insensitive)", tenant.Code);
            var hotelSettings = await _unitOfWork.HotelSettings
                .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower());

            if (hotelSettings == null)
            {
                _logger.LogError("❌ [GetCurrentHotelIdAsync] HotelSettings not found for Tenant Code: {TenantCode} in Tenant DB", tenant.Code);
                
                // ✅ DEBUG: List all available HotelSettings
                var allHotelSettings = await _unitOfWork.HotelSettings.GetAllAsync();
                _logger.LogError("❌ [GetCurrentHotelIdAsync] Available HotelSettings in tenant DB: {Available}", 
                    string.Join(", ", allHotelSettings.Select(h => $"HotelId={h.HotelId}, HotelCode='{h.HotelCode}', ZaaerId={h.ZaaerId}")));
                
                throw new InvalidOperationException(
                    $"HotelSettings not found for hotel code: {tenant.Code}. " +
                    "Please ensure hotel settings are configured in the tenant database with matching HotelCode.");
            }

            _logger.LogInformation("✅ [GetCurrentHotelIdAsync] Found HotelSettings: HotelId={HotelId}, HotelCode={HotelCode}, ZaaerId={ZaaerId}", 
                hotelSettings.HotelId, hotelSettings.HotelCode, hotelSettings.ZaaerId);

            // ✅ CRITICAL: التحقق من وجود zaaer_id في hotel_settings
            // NOTE: zaaer_id في hotel_settings يجب أن يطابق ZaaerId في Tenants table في Master DB
            // هذا يتم التحقق منه عند إنشاء/تحديث hotel_settings
            if (!hotelSettings.ZaaerId.HasValue)
            {
                _logger.LogError("❌ [GetCurrentHotelIdAsync] ZaaerId not configured for Tenant Code: {TenantCode} in HotelSettings. HotelId: {HotelId}", 
                    tenant.Code, hotelSettings.HotelId);
                throw new InvalidOperationException(
                    $"ZaaerId is not configured for hotel code: {tenant.Code} in hotel_settings table. " +
                    "Please ensure ZaaerId is set in hotel_settings table and matches ZaaerId in Tenants table in Master DB.");
            }

            var zaaerId = hotelSettings.ZaaerId.Value;
            _logger.LogInformation("✅ [GetCurrentHotelIdAsync] Using ZaaerId as HotelId: {ZaaerId} for Tenant Code: {TenantCode} (HotelSettings.HotelId: {HotelId}, HotelCode: {HotelCode})", 
                zaaerId, tenant.Code, hotelSettings.HotelId, hotelSettings.HotelCode);
            
            _logger.LogInformation("✅ [GetCurrentHotelIdAsync] ===== END - Returning: {ZaaerId} ======", zaaerId);
            
            // ✅ Return ZaaerId from hotel_settings (which should match Tenants.ZaaerId in Master DB)
            return zaaerId;
        }

        /// <summary>
        /// الحصول على جميع النفقات للفندق الحالي
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<IEnumerable<ExpenseResponseDto>> GetAllAsync()
        {
            var tenant = _tenantService.GetTenant();
            if (tenant == null)
            {
                throw new InvalidOperationException("Tenant not resolved. Cannot get expenses.");
            }

            // ✅ Get ZaaerId from hotel_settings (which should match Tenants.ZaaerId in Master DB)
            _logger.LogInformation("🔍 [GetAllAsync] About to call GetCurrentHotelIdAsync() for Tenant Code: {TenantCode}", tenant.Code);
            var hotelId = await GetCurrentHotelIdAsync();
            
            _logger.LogInformation("🔍 [GetAllAsync] GetCurrentHotelIdAsync() returned HotelId: {HotelId} for Tenant Code: {TenantCode}", 
                hotelId, tenant.Code);

            try
            {
                // ✅ PERFORMANCE OPTIMIZATION: Use Select projection to only load needed fields
                // This avoids loading full entity graphs and reduces memory usage
                // ✅ Use ZaaerId from hotel_settings as hotel_id (matches Tenants.ZaaerId in Master DB)
                // ✅ DEBUG: Log raw query before execution
                _logger.LogInformation("🔍 [GetAllAsync] Executing query: WHERE HotelId = {HotelId}", hotelId);
                
                // ✅ DEBUG: Check total expenses count in database for this hotelId
                var totalExpensesCount = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.HotelId == hotelId)
                    .CountAsync();
                _logger.LogInformation("🔍 [GetAllAsync] Total expenses count in database for HotelId {HotelId}: {Count}", hotelId, totalExpensesCount);
                
                // ✅ DEBUG: Get all hotel_ids in expenses table for debugging
                var allHotelIdsInExpenses = await _context.Expenses
                    .AsNoTracking()
                    .Select(e => e.HotelId)
                    .Distinct()
                    .ToListAsync();
                _logger.LogInformation("🔍 [GetAllAsync] All unique HotelIds found in expenses table: {HotelIds}", string.Join(", ", allHotelIdsInExpenses));
                
                // ✅ FIX: Load expenses WITHOUT Include because Foreign Key relationship is broken
                // The FK points to hotel_settings.hotel_id (PK=1) but we store zaaer_id (62) in expenses.hotel_id
                // So Include won't work. We'll load HotelSettings and ExpenseRooms separately.
                // ✅ Use explicit Select projection to avoid selecting removed payment_account_id column
                var expenses = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.HotelId == hotelId)
                    .OrderByDescending(e => e.DateTime)
                    .Select(e => new FinanceLedgerAPI.Models.Expense
                    {
                        ExpenseId = e.ExpenseId,
                        ExpenseNo = e.ExpenseNo,
                        ExpenseSeq = e.ExpenseSeq,
                        OldExpenseId = e.OldExpenseId,
                        LocalExpenseId = e.LocalExpenseId,
                        DateTime = e.DateTime,
                        DueDate = e.DueDate,
                        Comment = e.Comment,
                        HotelId = e.HotelId,
                        ExpenseCategoryId = e.ExpenseCategoryId,
                        TaxRate = e.TaxRate,
                        TaxAmount = e.TaxAmount,
                        BeforeTaxAmount = e.BeforeTaxAmount,
                        TotalAmount = e.TotalAmount,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        UpdatedAt = e.UpdatedAt,
                        UpdatedBy = e.UpdatedBy,
                        ApprovalStatus = e.ApprovalStatus,
                        ApprovedBy = e.ApprovedBy,
                        ApprovedAt = e.ApprovedAt,
                        RejectionReason = e.RejectionReason,
                        PaymentSource = e.PaymentSource,
                        StatusVoM = e.StatusVoM,
                        VomPayload = e.VomPayload,
                        VomSentAt = e.VomSentAt,
                        VomError = e.VomError,
                        VomRetryCount = e.VomRetryCount
                    })
                    .ToListAsync();
                
                _logger.LogInformation("🔍 [GetAllAsync] Expenses loaded: {Count}", expenses.Count);
                
                // ✅ Load HotelSettings separately (by HotelCode, not by FK)
                var hotelSettings = await _unitOfWork.HotelSettings
                    .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower());
                var hotelName = hotelSettings?.HotelName;
                
                // ✅ Load ExpenseRooms separately for all expenses
                var expenseIds = expenses.Select(e => e.ExpenseId).ToList();
                var allExpenseRooms = expenseIds.Any()
                    ? await _context.ExpenseRooms
                        .AsNoTracking()
                        .Where(er => expenseIds.Contains(er.ExpenseId))
                        .ToListAsync()
                    : new List<ExpenseRoomModel>();
                
                // ✅ Group ExpenseRooms by ExpenseId - keep as ExpenseRoomModel for now
                var expenseRoomsByExpenseId = allExpenseRooms
                    .GroupBy(er => er.ExpenseId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                
                // ✅ Project in memory (after loading from DB)
                var expenseData = expenses.Select(e => 
                {
                    List<object> roomsList;
                    if (expenseRoomsByExpenseId.ContainsKey(e.ExpenseId))
                    {
                        var projectedRooms = expenseRoomsByExpenseId[e.ExpenseId].Select(er => new
                        {
                            ExpenseRoomId = er.ExpenseRoomId,
                            ExpenseId = er.ExpenseId,
                            ZaaerId = er.ZaaerId,
                            Purpose = er.Purpose,
                            Amount = er.Amount,
                            CreatedAt = er.CreatedAt
                        }).ToList();
                        roomsList = projectedRooms.Cast<object>().ToList();
                    }
                    else
                    {
                        roomsList = new List<object>();
                    }
                    
                    return new
                    {
                        Expense = e,
                        HotelName = hotelName,
                        ExpenseRooms = roomsList
                    };
                }).ToList();

                _logger.LogInformation("🔍 [GetAllAsync] Raw expenseData count after query: {Count}", expenseData.Count);
                
                // ✅ DEBUG: Log each expense found
                foreach (var item in expenseData)
                {
                    var roomsCount = item.ExpenseRooms is System.Collections.ICollection collection ? collection.Count : ((System.Collections.Generic.IEnumerable<object>)item.ExpenseRooms).Count();
                    _logger.LogInformation("🔍 [GetAllAsync] Found Expense: ExpenseId={ExpenseId}, HotelId={HotelId}, DateTime={DateTime}, TotalAmount={TotalAmount}, Status={Status}, ExpenseRoomsCount={RoomsCount}",
                        item.Expense.ExpenseId, item.Expense.HotelId, item.Expense.DateTime, item.Expense.TotalAmount, item.Expense.ApprovalStatus, roomsCount);
                }

                // ✅ Get all unique ExpenseCategoryIds from expenses
                var categoryIds = expenseData
                    .Where(e => e.Expense.ExpenseCategoryId.HasValue)
                    .Select(e => e.Expense.ExpenseCategoryId!.Value)
                    .Distinct()
                    .ToList();
                
                _logger.LogInformation("🔍 [GetAllAsync] Unique CategoryIds found: {CategoryIds}", string.Join(", ", categoryIds));

                // ✅ Load category names from Master DB
                var masterCategories = categoryIds.Any()
                    ? await _masterDbContext.ExpenseCategories
                        .AsNoTracking()
                        .Where(ec => categoryIds.Contains(ec.Id))
                        .ToDictionaryAsync(ec => ec.Id, ec => ec.MainCategory)
                    : new Dictionary<int, string>();

                // ✅ Add category names to expense data
                var expenseDataWithCategories = expenseData.Select(e => new
                {
                    e.Expense,
                    ExpenseCategoryName = e.Expense.ExpenseCategoryId.HasValue && masterCategories.TryGetValue(e.Expense.ExpenseCategoryId.Value, out var categoryName)
                        ? categoryName
                        : null,
                    e.HotelName,
                    e.ExpenseRooms
                }).ToList();

                // ✅ PERFORMANCE OPTIMIZATION: Load all apartments in one query using dictionary for O(1) lookup
                var allZaaerIds = expenseDataWithCategories
                    .SelectMany(e => e.ExpenseRooms)
                    .Select(er => 
                    {
                        // Extract ZaaerId from anonymous type using reflection
                        var zaaerIdProperty = er.GetType().GetProperty("ZaaerId");
                        return zaaerIdProperty?.GetValue(er) as int?;
                    })
                    .Where(z => z.HasValue)
                    .Select(z => z!.Value)
                    .Distinct()
                    .ToList();

                var apartmentsDict = allZaaerIds.Any()
                    ? await _context.Apartments
                        .AsNoTracking()
                        .Where(a => allZaaerIds.Contains(a.ZaaerId ?? 0))
                        .ToDictionaryAsync(a => a.ZaaerId!.Value, a => a)
                    : new Dictionary<int, Apartment>();

                // ✅ PERFORMANCE OPTIMIZATION: Get all unique ApprovedBy user IDs
                var approvedByUserIds = expenseDataWithCategories
                    .Where(e => e.Expense.ApprovedBy.HasValue)
                    .Select(e => e.Expense.ApprovedBy!.Value)
                    .Distinct()
                    .ToList();

                // ✅ Load all approved by user info (full name, role, tenant) from Master DB in one query
                var approvedByUsersDict = new Dictionary<int, (string fullName, string? role, string? tenantName)>();
                if (approvedByUserIds.Any())
                {
                    var users = await _masterDbContext.MasterUsers
                        .AsNoTracking()
                        .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                        .Include(u => u.Tenant)
                        .Where(u => approvedByUserIds.Contains(u.Id))
                        .ToListAsync();

                    foreach (var user in users)
                    {
                        var fullName = user.FullName ?? user.Username;
                        var primaryRole = user.UserRoles?.FirstOrDefault()?.Role;
                        var roleName = GetRoleDisplayName(primaryRole?.Code);
                        var tenantName = user.Tenant?.Name;
                        approvedByUsersDict[user.Id] = (fullName, roleName, tenantName);
                    }
                }

                // ✅ PERFORMANCE OPTIMIZATION: Map to DTOs efficiently without nested loops
                var result = new List<ExpenseResponseDto>();
                foreach (var item in expenseDataWithCategories)
                {
                    var expense = item.Expense;
                    
                    // ✅ Create approval link only for pending expenses
                    string? approvalLink = null;
                    if (expense.ApprovalStatus == "pending")
                    {
                        var approvalBaseUrl = _configuration["AppSettings:ApprovalBaseUrl"] ?? "https://aleery.tryasp.net";
                        approvalBaseUrl = approvalBaseUrl.TrimEnd('/');
                        approvalLink = $"{approvalBaseUrl}/approve-expense.html?id={expense.ExpenseId}";
                    }

                    // ✅ Get approved by user info from dictionary
                    string? approvedByFullName = null;
                    string? approvedByRole = null;
                    string? approvedByTenantName = null;
                    if (expense.ApprovedBy.HasValue && approvedByUsersDict.TryGetValue(expense.ApprovedBy.Value, out var userInfo))
                    {
                        approvedByFullName = userInfo.fullName;
                        approvedByRole = userInfo.role;
                        approvedByTenantName = userInfo.tenantName;
                    }

                    // ✅ Map expense rooms efficiently
                    // Extract properties from anonymous type using reflection
                    var expenseRooms = new List<ExpenseRoomResponseDto>();
                    foreach (var er in item.ExpenseRooms)
                    {
                        var erType = er.GetType();
                        var expenseRoomId = (int)erType.GetProperty("ExpenseRoomId")!.GetValue(er)!;
                        var expenseId = Convert.ToInt64(erType.GetProperty("ExpenseId")!.GetValue(er)!);
                        var zaaerId = erType.GetProperty("ZaaerId")?.GetValue(er) as int?;
                        var purpose = erType.GetProperty("Purpose")?.GetValue(er) as string;
                        var amount = erType.GetProperty("Amount")?.GetValue(er) as decimal?;
                        var createdAt = (DateTime)erType.GetProperty("CreatedAt")!.GetValue(er)!;
                        
                        // ✅ Extract category code from purpose if it exists
                        string? categoryCode = null;
                        string? actualPurpose = purpose;
                        
                        if (zaaerId == null || (!string.IsNullOrEmpty(purpose) && purpose.StartsWith("CAT_")))
                        {
                            if (!string.IsNullOrEmpty(purpose) && purpose.StartsWith("CAT_"))
                            {
                                var parts = purpose.Split(new[] { " - " }, 2, StringSplitOptions.None);
                                if (parts.Length > 0)
                                {
                                    categoryCode = parts[0];
                                    actualPurpose = parts.Length > 1 ? parts[1] : null;
                                }
                            }
                        }

                        // ✅ Get apartment name if ZaaerId exists (O(1) dictionary lookup)
                        string? apartmentName = null;
                        if (zaaerId.HasValue && apartmentsDict.TryGetValue(zaaerId.Value, out var apartment))
                        {
                            apartmentName = apartment.ApartmentName;
                        }

                        expenseRooms.Add(new ExpenseRoomResponseDto
                        {
                            ExpenseRoomId = expenseRoomId,
                            ExpenseId = expenseId,
                            ZaaerId = zaaerId,
                            CategoryCode = categoryCode,
                            Purpose = actualPurpose,
                            Amount = amount ?? 0,
                            ApartmentName = apartmentName,
                            CreatedAt = createdAt
                        });
                    }

                    result.Add(new ExpenseResponseDto
                    {
                        ExpenseId = expense.ExpenseId,
                        ExpenseNo = expense.ExpenseNo,
                        ExpenseSeq = expense.ExpenseSeq,
                        HotelId = expense.HotelId,
                        HotelName = item.HotelName,
                        DateTime = expense.DateTime,
                        DueDate = expense.DueDate,
                        Comment = expense.Comment,
                        ExpenseCategoryId = expense.ExpenseCategoryId,
                        ExpenseCategoryName = item.ExpenseCategoryName,
                        TaxRate = expense.TaxRate,
                        TaxAmount = expense.TaxAmount,
                        BeforeTaxAmount = expense.BeforeTaxAmount,
                        TotalAmount = expense.TotalAmount,
                        CreatedAt = expense.CreatedAt,
                        UpdatedAt = expense.UpdatedAt,
                        UpdatedBy = expense.UpdatedBy,
                        ApprovalStatus = expense.ApprovalStatus,
                        ApprovedBy = expense.ApprovedBy,
                        PaymentSource = expense.PaymentSource, // ✅ Add payment source
                        ApprovedByFullName = approvedByFullName,
                        ApprovedByRole = approvedByRole,
                        ApprovedByTenantName = approvedByTenantName,
                        ApprovedAt = expense.ApprovedAt,
                        RejectionReason = expense.RejectionReason,
                        ApprovalLink = approvalLink,
                        ExpenseRooms = expenseRooms
                    });
                }

                _logger.LogInformation("✅ [GetAllAsync] Successfully loaded {Count} expenses with optimized query", result.Count);
                
                // ✅ DEBUG: Log final result details
                _logger.LogInformation("🔍 [GetAllAsync] Final result details:");
                foreach (var expenseDto in result)
                {
                    _logger.LogInformation("🔍 [GetAllAsync] Final DTO: ExpenseId={ExpenseId}, HotelId={HotelId}, HotelName={HotelName}, DateTime={DateTime}, TotalAmount={TotalAmount}, Status={Status}, CategoryId={CategoryId}, CategoryName={CategoryName}, RoomsCount={RoomsCount}",
                        expenseDto.ExpenseId, expenseDto.HotelId, expenseDto.HotelName, expenseDto.DateTime, expenseDto.TotalAmount, expenseDto.ApprovalStatus, 
                        expenseDto.ExpenseCategoryId, expenseDto.ExpenseCategoryName, expenseDto.ExpenseRooms?.Count ?? 0);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetAllAsync: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// تقرير ملخص المصروفات حسب الفندق للمشرف
        /// يرجع إجمالي المصروفات وعدد السندات لكل فندق في الفترة المحددة
        /// يجلب البيانات من جميع الفنادق المرتبطة بالمشرف في Master DB
        /// </summary>
        /// <summary>
        /// Get supervisor hotel summary - Optimized with Dapper for maximum performance
        /// </summary>
        public async Task<IEnumerable<ExpenseAnalyticsHotelTableDto>> GetSupervisorHotelSummaryAsync(DateTime fromDate, DateTime toDate, int? expenseCategoryId = null, string? approvalStatus = null)
        {
            try
            {
                // Get user ID from HttpContext
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null)
                {
                    throw new InvalidOperationException("HttpContext is not available.");
                }

                var userIdStr = httpContext.Items["UserId"]?.ToString();
                if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    throw new InvalidOperationException("User ID not found in request context.");
                }

                // Get user roles to determine access level
                var rolesStr = httpContext.Items["Roles"]?.ToString() ?? "";
                var roles = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var fullAccessRoles = new[] { "Admin", "Manager", "Accountant", "Officer", "Owner" };
                var hasFullAccess = roles.Any(r => fullAccessRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

                _logger.LogInformation("⚡ [GetSupervisorHotelSummaryAsync] Using Dapper for optimized performance. UserId={UserId}, From={From}, To={To}, CategoryId={CategoryId}, Status={Status}",
                    userId, fromDate, toDate, expenseCategoryId, approvalStatus);

                // Use Dapper service for optimized parallel queries
                var result = await _dapperService.GetSupervisorHotelSummaryAsync(
                    fromDate, 
                    toDate, 
                    expenseCategoryId, 
                    approvalStatus, 
                    userId, 
                    hasFullAccess);

                _logger.LogInformation("✅ [GetSupervisorHotelSummaryAsync] Retrieved {Count} hotels using Dapper", result.Count());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorHotelSummaryAsync] Error building supervisor hotel summary report.");
                throw;
            }
        }

        /// <summary>
        /// الحصول على تفاصيل المصروفات لفندق محدد في سياق المشرف
        /// Get expense details for a specific hotel in supervisor context
        /// </summary>
        /// <summary>
        /// Get supervisor hotel expenses - Optimized with Dapper for maximum performance
        /// </summary>
        public async Task<IEnumerable<ExpenseResponseDto>> GetSupervisorHotelExpensesAsync(
            string hotelCode,
            DateTime fromDate,
            DateTime toDate,
            int? expenseCategoryId = null,
            string? approvalStatus = null)
        {
            try
            {
                _logger.LogInformation("⚡ [GetSupervisorHotelExpensesAsync] Using Dapper for optimized performance. HotelCode={HotelCode}, From={From}, To={To}, CategoryId={CategoryId}, Status={Status}",
                    hotelCode, fromDate, toDate, expenseCategoryId, approvalStatus);

                // Use Dapper service for optimized query
                var result = await _dapperService.GetSupervisorHotelExpensesAsync(
                    hotelCode,
                    fromDate,
                    toDate,
                    expenseCategoryId,
                    approvalStatus);

                _logger.LogInformation("✅ [GetSupervisorHotelExpensesAsync] Retrieved {Count} expenses using Dapper", result.Count());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorHotelExpensesAsync] Error loading expenses for hotel: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// الحصول على نفقة محددة بالمعرف
        /// ✅ يسمح بالوصول بدون X-Hotel-Code header للسماح للمشرفين بالوصول المباشر
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<ExpenseResponseDto?> GetByIdAsync(long id)
        {
            // ✅ محاولة الحصول على hotelId، لكن إذا فشل، نبحث بدون filter
            int? hotelId = null;
            Tenant? tenant = null;
            try
            {
                hotelId = await GetCurrentHotelIdAsync();
                tenant = _tenantService.GetTenant();
                _logger.LogInformation("🔍 [GetByIdAsync] Searching expense for ExpenseId: {ExpenseId} using HotelId (ZaaerId): {HotelId}", 
                    id, hotelId);
            }
            catch (InvalidOperationException)
            {
                // ✅ إذا لم يكن هناك X-Hotel-Code header، نبحث بدون hotel filter
                // هذا يسمح للمشرفين بالوصول المباشر عبر رابط الموافقة
                _logger.LogInformation("⚠️ No X-Hotel-Code header found, searching expense without hotel filter (for public approval access)");
            }

            // ✅ FIX: Load expense WITHOUT Include because Foreign Key relationship is broken
            // The FK points to hotel_settings.hotel_id (PK=1) but we store zaaer_id (62) in expenses.hotel_id
            // ✅ Use explicit Select projection to avoid selecting removed payment_account_id column
            FinanceLedgerAPI.Models.Expense? expense = hotelId.HasValue
                ? await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.ExpenseId == id && e.HotelId == hotelId.Value)
                    .Select(e => new FinanceLedgerAPI.Models.Expense
                    {
                        ExpenseId = e.ExpenseId,
                        ExpenseNo = e.ExpenseNo,
                        ExpenseSeq = e.ExpenseSeq,
                        OldExpenseId = e.OldExpenseId,
                        LocalExpenseId = e.LocalExpenseId,
                        DateTime = e.DateTime,
                        DueDate = e.DueDate,
                        Comment = e.Comment,
                        HotelId = e.HotelId,
                        ExpenseCategoryId = e.ExpenseCategoryId,
                        TaxRate = e.TaxRate,
                        TaxAmount = e.TaxAmount,
                        BeforeTaxAmount = e.BeforeTaxAmount,
                        TotalAmount = e.TotalAmount,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        UpdatedAt = e.UpdatedAt,
                        UpdatedBy = e.UpdatedBy,
                        ApprovalStatus = e.ApprovalStatus,
                        ApprovedBy = e.ApprovedBy,
                        ApprovedAt = e.ApprovedAt,
                        RejectionReason = e.RejectionReason,
                        PaymentSource = e.PaymentSource,
                        StatusVoM = e.StatusVoM,
                        VomPayload = e.VomPayload,
                        VomSentAt = e.VomSentAt,
                        VomError = e.VomError,
                        VomRetryCount = e.VomRetryCount
                    })
                    .FirstOrDefaultAsync()
                : await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.ExpenseId == id)
                    .Select(e => new FinanceLedgerAPI.Models.Expense
                    {
                        ExpenseId = e.ExpenseId,
                        ExpenseNo = e.ExpenseNo,
                        ExpenseSeq = e.ExpenseSeq,
                        OldExpenseId = e.OldExpenseId,
                        LocalExpenseId = e.LocalExpenseId,
                        DateTime = e.DateTime,
                        DueDate = e.DueDate,
                        Comment = e.Comment,
                        HotelId = e.HotelId,
                        ExpenseCategoryId = e.ExpenseCategoryId,
                        TaxRate = e.TaxRate,
                        TaxAmount = e.TaxAmount,
                        BeforeTaxAmount = e.BeforeTaxAmount,
                        TotalAmount = e.TotalAmount,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        UpdatedAt = e.UpdatedAt,
                        UpdatedBy = e.UpdatedBy,
                        ApprovalStatus = e.ApprovalStatus,
                        ApprovedBy = e.ApprovedBy,
                        ApprovedAt = e.ApprovedAt,
                        RejectionReason = e.RejectionReason,
                        PaymentSource = e.PaymentSource,
                        StatusVoM = e.StatusVoM,
                        VomPayload = e.VomPayload,
                        VomSentAt = e.VomSentAt,
                        VomError = e.VomError,
                        VomRetryCount = e.VomRetryCount
                    })
                    .FirstOrDefaultAsync();
            
            // Load ExpenseRooms separately (since we can't use Include with Select projection)
            if (expense != null)
            {
                var expenseRooms = await _context.ExpenseRooms
                    .AsNoTracking()
                    .Include(er => er.Apartment)
                    .Where(er => er.ExpenseId == expense.ExpenseId)
                    .ToListAsync();
                expense.ExpenseRooms = expenseRooms;
            }

            if (expense == null)
            {
                _logger.LogWarning("⚠️ [GetByIdAsync] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId}", id, hotelId);
                return null;
            }

            // ✅ Load HotelSettings separately (by HotelCode, not by FK)
            string? hotelName = null;
            if (tenant != null)
            {
                var hotelSettings = await _unitOfWork.HotelSettings
                    .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower());
                hotelName = hotelSettings?.HotelName;
            }
            else if (expense.HotelId > 0)
            {
                // Fallback: try to find HotelSettings by ZaaerId (if we're searching without tenant)
                var hotelSettings = await _unitOfWork.HotelSettings
                    .FindSingleAsync(h => h.ZaaerId == expense.HotelId);
                hotelName = hotelSettings?.HotelName;
            }

            // ✅ Get category name from Master DB
            string? categoryName = null;
            if (expense.ExpenseCategoryId.HasValue)
            {
                var masterCategory = await _masterDbContext.ExpenseCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ec => ec.Id == expense.ExpenseCategoryId.Value);
                categoryName = masterCategory?.MainCategory;
            }

            // ✅ Map to DTO manually since HotelSettings navigation property won't work
            return await MapToDtoWithHotelNameAsync(expense, categoryName, hotelName);
        }

        /// <summary>
        /// إنشاء نفقة جديدة
        /// ✅ Transaction Management: All operations wrapped in a single transaction for atomicity
        /// </summary>
        public async Task<ExpenseResponseDto> CreateAsync(CreateExpenseDto dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            var auditIds = new List<long>();
            var tenantCode = _tenantService.GetTenant()?.Code;

            try
            {
                var tenant = _tenantService.GetTenant()
                    ?? throw new InvalidOperationException("Tenant not resolved. Cannot create expense.");
                tenantCode = tenant.Code;

                var hotelSettings = await _unitOfWork.HotelSettings
                    .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower())
                    ?? throw new InvalidOperationException(
                        $"HotelSettings not found for hotel code: {tenant.Code}.");

                if (!hotelSettings.ZaaerId.HasValue || hotelSettings.ZaaerId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        $"ZaaerId is not configured for hotel code: {tenant.Code} in hotel_settings.");
                }

                var hotelZaaerId = hotelSettings.ZaaerId.Value;
                var localHotelId = hotelSettings.HotelId;

                // ✅ Determine approval status based on payment source
                string approvalStatus;
                string paymentSource = dto.PaymentSource ?? "Branch";
                
                // ✅ Normalize payment source value (handle typos like "Managemer" -> "Management")
                string normalizedPaymentSource = paymentSource.Trim();
                if (normalizedPaymentSource.Equals("Management", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPaymentSource.Equals("Managemer", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPaymentSource.StartsWith("Manag", StringComparison.OrdinalIgnoreCase))
                {
                    // ✅ Normalize to "Management" if it looks like Management (handles typos)
                    normalizedPaymentSource = "Management";
                    // ✅ If payment source is Management, set status to awaiting-officer
                    approvalStatus = "awaiting-officer";
                    _logger.LogInformation("⏳ Setting expense status to awaiting-officer (requires Officer approval - Payment Source: {PaymentSource} -> normalized to Management)", paymentSource);
                }
                else
                {
                    // ✅ Normalize to "Branch" for all other cases
                    normalizedPaymentSource = "Branch";
                    // ✅ If payment source is Branch, use existing logic (pending for supervisor)
                    approvalStatus = "pending";
                    _logger.LogInformation("⏳ Setting expense status to pending (requires supervisor approval - Payment Source: {PaymentSource} -> normalized to Branch)", paymentSource);
                }

                // ✅ Set DueDate to today if not provided, or apply "end of KSA day" rule if provided:
                // - If DueDate is between midnight and 4:00 AM KSA, treat as previous day (after adding 4 hours, then subtracting them to date only)
                // - Else, use provided (converted) DueDate as-is, but in KSA date
                DateTime? dueDate;
                if (dto.DueDate.HasValue)
                {
                    // Always convert input value to UTC, then to KSA time
                    var inputUtc = (dto.DueDate.Value.Kind == DateTimeKind.Utc)
                        ? dto.DueDate.Value
                        : dto.DueDate.Value.ToUniversalTime();

                    var ksaTime = KsaTime.ConvertFromUtc(inputUtc);

                    // If KSA hour is from 0 (inclusive) to <4, consider "end of previous day"
                    if (ksaTime.Hour >= 0 && ksaTime.Hour < 4)
                    {
                        // Add 4 hours, then get .Date, then subtract back 4 hours
                        var adjusted = ksaTime.AddHours(4).Date.AddHours(-4);
                        dueDate = adjusted.Date;
                    }
                    else
                    {
                        dueDate = ksaTime.Date;
                    }
                }
                else
                {
                    dueDate = KsaTime.Now.Date;
                }

                // Resolve creator from HttpContext.Items (legacy) or JWT claims (PMS).
                var (createdBy, createdByFullName) = await ResolveCurrentUserForAuditAsync();

                // ✅ Get tax rate from taxes table if not provided in DTO
                decimal? taxRate = dto.TaxRate;
                if (!taxRate.HasValue)
                {
                    var tax = await _context.Taxes
                        .AsNoTracking()
                        .Where(t => t.HotelId == hotelZaaerId && t.Enabled)
                        .OrderByDescending(t => t.TaxType == "VAT" || t.TaxType == "vat")
                        .ThenBy(t => t.Id)
                        .FirstOrDefaultAsync();

                    if (tax != null)
                    {
                        taxRate = tax.TaxRate;
                        _logger.LogInformation(
                            "✅ Tax rate retrieved from taxes table: {TaxRate}% for HotelZaaerId (taxes.hotel_id) = {HotelZaaerId}",
                            taxRate,
                            tax.HotelId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ No enabled tax found in taxes table for HotelZaaerId (taxes.hotel_id) = {HotelZaaerId}",
                            hotelZaaerId);
                    }
                }

                // ✅ Validate ExpenseCategoryId exists in Master DB if provided
                if (dto.ExpenseCategoryId.HasValue)
                {
                    if (dto.UseTenantCategories)
                    {
                        var categoryExists = await TenantExpenseCategoryExistsAsync(
                            dto.ExpenseCategoryId.Value,
                            tenant.Code,
                            localHotelId,
                            hotelZaaerId);

                        if (!categoryExists)
                        {
                            _logger.LogError(
                                "❌ [CreateAsync] ExpenseCategoryId {CategoryId} not found in tenant expense_categories for hotel code {HotelCode}",
                                dto.ExpenseCategoryId.Value,
                                tenant.Code);
                            throw new InvalidOperationException(
                                $"Expense category with ID {dto.ExpenseCategoryId.Value} not found for this hotel.");
                        }
                    }
                    else
                    {
                    var category = await _masterDbContext.ExpenseCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ec => ec.Id == dto.ExpenseCategoryId.Value);
                    
                    if (category == null)
                    {
                        _logger.LogError("❌ [CreateAsync] ExpenseCategoryId {CategoryId} not found in Master DB", dto.ExpenseCategoryId.Value);
                        throw new InvalidOperationException($"Expense category with ID {dto.ExpenseCategoryId.Value} not found.");
                    }
                    
                    if (!category.IsActive)
                    {
                        _logger.LogWarning("⚠️ [CreateAsync] ExpenseCategoryId {CategoryId} exists but is inactive (MainCategory: {MainCategory})", 
                            dto.ExpenseCategoryId.Value, category.MainCategory);
                        // ✅ Allow inactive categories - don't throw error, just log warning
                    }
                    }
                }
                
                // ✅ Store Master DB ExpenseCategory ID (dto.ExpenseCategoryId is from Master DB)
                // ✅ Convert DateTime to KSA timezone using KsaTime
                DateTime expenseDateTime = dto.DateTime.Kind == DateTimeKind.Utc 
                    ? KsaTime.ConvertFromUtc(dto.DateTime) 
                    : KsaTime.ConvertFromUtc(dto.DateTime.ToUniversalTime());
                
                // فكرة: سنقرأ ساعة آخر اليوم الخاصة بالسعودية (مثلاً 4 صباحاً) من الإعدادات (appsettings.json).
                // إذا كان DueDate بين الساعة 12 منتصف الليل حتى 4:00 صباحاً، نُبقي التاريخ مقيداً لليوم السابق (نضيف 4 ساعات، ثم نعيد طرحها).
                // وإلا نستخدم dueDate كما هو.
                //
                // إعداد في appsettings.json مثل:
                //  "SADayEndHour": 4
                //
                // ثم في الكود نحققه هنا:

                // اقرأ التهيئة (مرة واحدة) من الكونفيج
                int saDayEndHour = 4; // Default إذا لم تكن في الإعدادات
                try
                {
                    var saDayEndHourStr = _configuration["SADayEndHour"];
                    if (int.TryParse(saDayEndHourStr, out var configuredHour) && configuredHour > 0 && configuredHour < 24)
                    {
                        saDayEndHour = configuredHour;
                    }
                }
                catch { /* safe default */ }

                DateTime? adjustedDueDate = null;
                if (dueDate.HasValue)
                {
                    // نحن نفترض dueDate بالتوقيت المحلي للسعودية
                    var original = dueDate.Value;
                    if (original.TimeOfDay < TimeSpan.FromHours(saDayEndHour)) // مثلاً أقل من 4 ص
                    {
                        // يعتبر تابعاً لليوم السابق
                        adjustedDueDate = original.Date.AddDays(-1);
                    }
                    else
                    {
                        // التاريخ الطبيعي
                        adjustedDueDate = original.Date;
                    }
                }

                await EnsureExpenseDocumentCounterSyncedAsync(localHotelId, hotelZaaerId);

                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "expense",
                    localHotelId,
                    createdBy?.ToString(),
                    $"expense:{localHotelId}:{Guid.NewGuid():N}");

                auditIds.Add(identity.AuditId);

                var expenseId = await ResolveExpenseIdAsync(identity, createdBy?.ToString(), auditIds);

                var nextLocalExpenseId = await AllocateNextLocalExpenseIdAsync();

                var expense = new ExpenseModel
                {
                    ExpenseId = expenseId,
                    ExpenseNo = identity.DocumentNo,
                    ExpenseSeq = checked((int)identity.NumericValue),
                    LocalExpenseId = nextLocalExpenseId,
                    HotelId = hotelZaaerId,
                    DateTime = expenseDateTime,
                    DueDate = adjustedDueDate,
                    Comment = dto.Comment,
                    ExpenseCategoryId = dto.ExpenseCategoryId,
                    TaxRate = taxRate,
                    TaxAmount = dto.TaxAmount,
                    BeforeTaxAmount = dto.BeforeTaxAmount ?? (dto.TaxAmount.HasValue
                        ? dto.TotalAmount - dto.TaxAmount.Value
                        : dto.TotalAmount),
                    TotalAmount = dto.TotalAmount,
                    ApprovalStatus = approvalStatus,
                    PaymentSource = normalizedPaymentSource,
                    CreatedAt = KsaTime.Now,
                    CreatedBy = createdBy
                };

                await PersistNewExpenseAsync(expense);
                var oldExpenseId = await EnsureExpenseOldExpenseIdAsync(expense);

                await AddApprovalHistoryAsync(
                    expense.ExpenseId,
                    oldExpenseId,
                    action: "created",
                    status: approvalStatus,
                    actionBy: createdBy,
                    actionByFullName: createdByFullName,
                    comments: "تم إنشاء طلب المصروف");

            // إضافة expense_rooms إذا وُجدت
            if (dto.ExpenseRooms != null && dto.ExpenseRooms.Any())
            {
                // ✅ CRITICAL FIX: Get all HotelIds with the same HotelCode (like in ApartmentService)
                var hotelCode = hotelSettings.HotelCode ?? tenant.Code;
                
                // Get all HotelIds with the same HotelCode
                var allHotelIdsWithSameCode = await _context.HotelSettings
                    .AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower())
                    .Select(h => h.HotelId)
                    .ToListAsync();
                
                // Check what HotelIds are actually used in apartments table
                var hotelIdsInApartments = await _context.Apartments
                    .AsNoTracking()
                    .Select(a => a.HotelId)
                    .Distinct()
                    .ToListAsync();
                
                // If apartments are linked to HotelId=11 but we're searching with HotelId=1, include HotelId=11
                var hotelSettingsWithId11 = await _context.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelId == 11);
                
                if (hotelSettingsWithId11 != null && hotelIdsInApartments.Contains(11))
                {
                    if (!allHotelIdsWithSameCode.Contains(11))
                    {
                        allHotelIdsWithSameCode.Add(11);
                        _logger.LogWarning("⚠️ [CreateAsync] Added HotelId=11 to search list (data exists but different HotelCode: '{DifferentCode}')", 
                            hotelSettingsWithId11.HotelCode);
                    }
                }
                else if (hotelIdsInApartments.Contains(11))
                {
                    allHotelIdsWithSameCode.Add(11);
                    _logger.LogWarning("⚠️ [CreateAsync] Added HotelId=11 to search list (data exists but no HotelSettings record)");
                }
                
                _logger.LogInformation("🔍 [CreateAsync] Final HotelIds to search for apartments: {HotelIds}", 
                    string.Join(", ", allHotelIdsWithSameCode));

                foreach (var roomDto in dto.ExpenseRooms)
                {
                    // ✅ Check if it's a category (CAT_BUILDING, CAT_RECEPTION, CAT_CORRIDORS) or actual room
                    if (!string.IsNullOrEmpty(roomDto.CategoryCode) && roomDto.CategoryCode.StartsWith("CAT_"))
                    {
                        // ✅ It's a room category (مبنى كامل, الاستقبال, الممرات)
                        // For categories, we don't need to find an apartment - just save the category code
                        // We'll use ApartmentId = 0 or a special value, but store categoryCode in Purpose field
                        // Or we need to add category_code column to expense_rooms table
                        var categoryRoom = new ExpenseRoomModel
                        {
                            ExpenseId = expense.ExpenseId,
                            ZaaerId = null, // ✅ Use null for categories (ZaaerId is nullable)
                            Purpose = roomDto.CategoryCode + (string.IsNullOrEmpty(roomDto.Purpose) ? "" : " - " + roomDto.Purpose), // ✅ Store category code in purpose
                            Amount = roomDto.Amount,
                            CreatedAt = KsaTime.Now
                        };

                        await _unitOfWork.ExpenseRooms.AddAsync(categoryRoom);
                        _logger.LogInformation("✅ [CreateAsync] Added ExpenseRoom with Category: ExpenseId={ExpenseId}, CategoryCode={CategoryCode}, Purpose={Purpose}, Amount={Amount}", 
                            expense.ExpenseId, roomDto.CategoryCode, roomDto.Purpose, roomDto.Amount);
                        continue;
                    }

                    // ✅ البحث عن Apartment باستخدام ApartmentId أو ZaaerId مع جميع HotelIds المرتبطة بنفس HotelCode
                    Apartment? apartment = null;
                    
                    if (roomDto.ApartmentId.HasValue)
                    {
                        // البحث باستخدام ApartmentId مع جميع HotelIds
                        apartment = await _context.Apartments
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.ApartmentId == roomDto.ApartmentId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));
                    }
                    else if (roomDto.ZaaerId.HasValue)
                    {
                        // ✅ البحث باستخدام ZaaerId مع جميع HotelIds المرتبطة بنفس HotelCode
                        _logger.LogInformation("🔍 [CreateAsync] Searching for apartment with ZaaerId={ZaaerId}, HotelIds={HotelIds}", 
                            roomDto.ZaaerId.Value, string.Join(", ", allHotelIdsWithSameCode));
                        
                        apartment = await _context.Apartments
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));
                        
                        if (apartment == null)
                        {
                            // ✅ Try searching without HotelId filter as fallback
                            _logger.LogWarning("⚠️ [CreateAsync] Apartment not found with HotelId filter, trying without filter...");
                            apartment = await _context.Apartments
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value);
                        }
                    }

                    if (apartment == null)
                    {
                        _logger.LogError("❌ [CreateAsync] Apartment not found: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, HotelIds={HotelIds}", 
                            roomDto.ApartmentId, roomDto.ZaaerId, string.Join(", ", allHotelIdsWithSameCode));
                        continue; // Skip invalid apartment
                    }

                    _logger.LogInformation("✅ [CreateAsync] Found apartment: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, Name={Name}, HotelId={HotelId}", 
                        apartment.ApartmentId, apartment.ZaaerId, apartment.ApartmentName, apartment.HotelId);

                    // ✅ Save zaaerId directly (Foreign Key to apartments.zaaer_id)
                    if (!apartment.ZaaerId.HasValue)
                    {
                        _logger.LogWarning("⚠️ [CreateAsync] Apartment found but ZaaerId is null: ApartmentId={ApartmentId}, Name={Name}", 
                            apartment.ApartmentId, apartment.ApartmentName);
                        continue; // Skip if apartment doesn't have zaaerId
                    }

                    var expenseRoom = new ExpenseRoomModel
                    {
                        ExpenseId = expense.ExpenseId,
                        ZaaerId = apartment.ZaaerId.Value, // ✅ حفظ zaaerId مباشرة (Foreign Key to apartments.zaaer_id)
                        Purpose = roomDto.Purpose,
                        Amount = roomDto.Amount,
                        CreatedAt = KsaTime.Now
                    };

                    await _unitOfWork.ExpenseRooms.AddAsync(expenseRoom);
                    _logger.LogInformation("✅ [CreateAsync] Added ExpenseRoom: ExpenseId={ExpenseId}, ZaaerId={ZaaerId}, Purpose={Purpose}, Amount={Amount}", 
                        expense.ExpenseId, apartment.ZaaerId.Value, roomDto.Purpose, roomDto.Amount);
                }

                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("✅ [CreateAsync] Saved {Count} expense rooms to database", dto.ExpenseRooms.Count);
                }

                await _unitOfWork.CommitTransactionAsync();

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId);
                }

                _logger.LogInformation(
                    "✅ Expense created successfully: ExpenseId={ExpenseId}, ExpenseNo={ExpenseNo}, HotelId={HotelId}",
                    expense.ExpenseId,
                    expense.ExpenseNo,
                    hotelZaaerId);

                // ✅ Get category name from Master DB for response
                string? categoryName = null;
                if (expense.ExpenseCategoryId.HasValue)
                {
                    var masterCategory = await _masterDbContext.ExpenseCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ec => ec.Id == expense.ExpenseCategoryId.Value);
                    categoryName = masterCategory?.MainCategory;
                }

                // ✅ Always reload expense with all related data (ExpenseRooms) after commit
                // Note: HotelSettings is loaded separately because FK relationship is broken
                var expenseWithRelations = await _context.Expenses
                    .AsNoTracking()
                    .Include(e => e.ExpenseRooms)
                        .ThenInclude(er => er.Apartment)
                    .FirstOrDefaultAsync(e => e.ExpenseId == expense.ExpenseId);

                if (expenseWithRelations != null)
                {
                    return await MapToDtoWithHotelNameAsync(expenseWithRelations, categoryName, hotelSettings.HotelName);
                }

                _logger.LogWarning("⚠️ [CreateAsync] Failed to reload expense after commit, using original entity: ExpenseId={ExpenseId}", expense.ExpenseId);
                return await MapToDtoWithHotelNameAsync(expense, categoryName, hotelSettings.HotelName);
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message);
                }

                await _unitOfWork.RollbackTransactionAsync();
                var detail = ex.InnerException?.Message ?? ex.Message;
                if (detail.Contains("identity column", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(
                        ex,
                        "❌ Failed to create expense: INSERT hit an IDENTITY column on dbo.expenses (hotel={HotelCode}). " +
                        "Check sys.columns for is_identity=1 — typically old_expense_id must not be set manually. Detail: {Detail}",
                        tenantCode ?? "?",
                        detail);
                }
                else
                {
                    _logger.LogError(ex, "❌ Failed to create expense. Transaction rolled back. Error: {ErrorMessage}", detail);
                }
                throw;
            }
        }

        private async Task<int> AllocateNextLocalExpenseIdAsync()
        {
            var maxLocal = await _context.Expenses.MaxAsync(e => (int?)e.LocalExpenseId) ?? 0;
            return maxLocal + 1;
        }

        /// <summary>
        /// Resolves tenant <c>expense_id</c> from Master numbering (never SQL IDENTITY).
        /// Prefers <see cref="GeneratedBusinessIdentity.ZaaerId"/>; falls back to entity counter when doc type has no global id.
        /// </summary>
        private async Task<long> ResolveExpenseIdAsync(
            GeneratedBusinessIdentity identity,
            string? createdBy,
            List<long> auditIds)
        {
            if (identity.ZaaerId.HasValue && identity.ZaaerId.Value > 0)
            {
                return identity.ZaaerId.Value;
            }

            _logger.LogWarning(
                "GetNextBusinessIdentity for expense returned no ZaaerId; falling back to GetNextEntityZaaerIdAsync.");

            var entityIdentity = await _numberingService.GetNextEntityZaaerIdAsync(
                "expense",
                createdBy,
                $"expense-entity:{Guid.NewGuid():N}");

            auditIds.Add(entityIdentity.AuditId);

            if (entityIdentity.ZaaerId <= 0)
            {
                throw new InvalidOperationException(
                    "Master DB did not return a global expense id (expense_id). " +
                    "Ensure DocumentTypes.uses_global_zaaer_id = 1 for doc_code 'expense' or EntityZaaerCounters is configured.");
            }

            return entityIdentity.ZaaerId;
        }


        /// <summary>
        /// Aligns Master <c>DocumentCounters</c> for <c>expense</c> with tenant data so the next
        /// <c>expense_seq</c> does not collide with <c>UX_expenses_expense_seq</c>.
        /// </summary>
        private async Task EnsureExpenseDocumentCounterSyncedAsync(
            int localHotelId,
            int hotelZaaerId,
            CancellationToken cancellationToken = default)
        {
            var hotelIds = new HashSet<int> { localHotelId, hotelZaaerId };

            var maxFromSeq = await _context.Expenses
                .AsNoTracking()
                .Where(e => hotelIds.Contains(e.HotelId))
                .MaxAsync(e => (int?)e.ExpenseSeq, cancellationToken) ?? 0;

            var maxFromNoRows = await _context.Database.SqlQueryRaw<long?>(
                    """
                    SELECT MAX(TRY_CAST(
                        REPLACE(REPLACE(expense_no, 'EXP_', ''), 'EXP', '') AS BIGINT))
                    FROM dbo.expenses
                    WHERE hotel_id IN ({0}, {1})
                      AND expense_no LIKE 'EXP%'
                    """,
                    localHotelId,
                    hotelZaaerId)
                .ToListAsync(cancellationToken);

            var maxFromNo = maxFromNoRows.FirstOrDefault() ?? 0L;
            var tenantMax = Math.Max(maxFromSeq, maxFromNo);

            if (tenantMax <= 0)
            {
                return;
            }

            await _numberingService.EnsureDocumentCounterAtLeastAsync(
                "expense",
                localHotelId,
                tenantMax,
                cancellationToken);

            _logger.LogInformation(
                "Expense document counter synced from tenant data: max_seq={MaxSeq}, max_from_no={MaxFromNo}, hotel_local={LocalHotelId}, hotel_zaaer={HotelZaaerId}",
                maxFromSeq,
                maxFromNo,
                localHotelId,
                hotelZaaerId);
        }

        private async Task<bool> ExpensesColumnIsIdentityAsync(
            string columnName,
            CancellationToken cancellationToken = default)
        {
            var flags = await _context.Database.SqlQueryRaw<int>(
                    """
                    SELECT CAST(c.is_identity AS int)
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = N'expenses' AND c.name = {0}
                    """,
                    columnName)
                .ToListAsync(cancellationToken);

            return flags.Count > 0 && flags[0] == 1;
        }

        private async Task<(int? UserId, string? FullName)> ResolveCurrentUserForAuditAsync()
        {
            int? userId = null;

            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("UserId", out var userIdObj) == true &&
                userIdObj != null &&
                int.TryParse(userIdObj.ToString(), out var itemsUserId) &&
                itemsUserId > 0)
            {
                userId = itemsUserId;
            }
            else if (_currentUser.UserId.HasValue && _currentUser.UserId.Value > 0)
            {
                userId = _currentUser.UserId.Value;
            }

            if (!userId.HasValue)
            {
                return (null, null);
            }

            var masterUser = await _masterDbContext.MasterUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            var fullName = masterUser?.FullName ?? masterUser?.Username ?? _currentUser.Username;
            return (userId, fullName);
        }

        private async Task<int> EnsureExpenseOldExpenseIdAsync(ExpenseModel expense)
        {
            if (expense.OldExpenseId > 0)
            {
                return expense.OldExpenseId;
            }

            var oldExpenseId = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.ExpenseId == expense.ExpenseId)
                .Select(e => e.OldExpenseId)
                .FirstOrDefaultAsync();

            if (oldExpenseId <= 0)
            {
                throw new InvalidOperationException(
                    $"Expense {expense.ExpenseId} has no old_expense_id — cannot link child rows.");
            }

            // Do not assign to tracked expense — old_expense_id is IDENTITY on tenant DBs and must not be updated.
            return oldExpenseId;
        }

        private void DetachExpenseIdentityColumns(ExpenseModel expense)
        {
            var entry = _context.Entry(expense);
            if (entry.State == EntityState.Detached)
            {
                return;
            }

            entry.Property(e => e.OldExpenseId).IsModified = false;
        }

        private async Task AddApprovalHistoryAsync(
            long expenseId,
            int oldExpenseId,
            string action,
            string status,
            int? actionBy = null,
            string? actionByFullName = null,
            string? rejectionReason = null,
            string? comments = null,
            string? recommendation = null,
            int? recommendationToUserId = null)
        {
            var history = new ExpenseApprovalHistoryModel
            {
                ExpenseId = expenseId,
                OldExpenseId = oldExpenseId,
                Action = action,
                ActionBy = actionBy,
                ActionByFullName = actionByFullName,
                ActionAt = KsaTime.Now,
                Status = status,
                RejectionReason = rejectionReason,
                Comments = comments,
                Recommendation = !string.IsNullOrWhiteSpace(recommendation) ? recommendation.Trim() : null,
                RecommendationToUserId = recommendationToUserId > 0 ? recommendationToUserId : null,
                RecommendationReadBy = null
            };

            await _context.ExpenseApprovalHistories.AddAsync(history);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "✅ Expense approval history saved: ExpenseId={ExpenseId}, OldExpenseId={OldExpenseId}, Action={Action}, Status={Status}, ActionBy={ActionBy}",
                expenseId,
                oldExpenseId,
                action,
                status,
                actionBy);
        }

        private async Task PersistNewExpenseAsync(ExpenseModel expense, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "Insert expense row: ExpenseId={ExpenseId}, LocalExpenseId={LocalExpenseId}, ExpenseNo={ExpenseNo} (OldExpenseId left to DB if identity)",
                expense.ExpenseId,
                expense.LocalExpenseId,
                expense.ExpenseNo);

            await _unitOfWork.Expenses.AddAsync(expense);

            var expenseIdIsIdentity = await ExpensesColumnIsIdentityAsync("expense_id", cancellationToken);
            if (!expenseIdIsIdentity)
            {
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            _logger.LogWarning(
                "expenses.expense_id is IDENTITY on this tenant — using IDENTITY_INSERT for Master ZaaerId={ExpenseId}.",
                expense.ExpenseId);

            await ExecuteSqlOnActiveTransactionAsync("SET IDENTITY_INSERT dbo.expenses ON;", cancellationToken);
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            finally
            {
                await ExecuteSqlOnActiveTransactionAsync("SET IDENTITY_INSERT dbo.expenses OFF;", cancellationToken);
            }
        }

        private async Task ExecuteSqlOnActiveTransactionAsync(string sql, CancellationToken cancellationToken)
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var transaction = _context.Database.CurrentTransaction;
            if (transaction != null)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Hotel ids that may appear in expense_categories.hotel_id (PK and/or zaaer_id).
        /// </summary>
        private async Task<List<int>> ResolveTenantExpenseCategoryHotelIdsAsync(
            string tenantCode,
            int localHotelId,
            int hotelZaaerId)
        {
            var ids = new HashSet<int> { localHotelId, hotelZaaerId };

            var settings = await _context.HotelSettings
                .AsNoTracking()
                .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == tenantCode.ToLower())
                .Select(h => new { h.HotelId, h.ZaaerId })
                .ToListAsync();

            foreach (var row in settings)
            {
                ids.Add(row.HotelId);
                if (row.ZaaerId.HasValue)
                {
                    ids.Add(row.ZaaerId.Value);
                }
            }

            return ids.ToList();
        }

        private async Task<bool> TenantExpenseCategoryExistsAsync(
            int expenseCategoryId,
            string tenantCode,
            int localHotelId,
            int hotelZaaerId)
        {
            var hotelIds = await ResolveTenantExpenseCategoryHotelIdsAsync(tenantCode, localHotelId, hotelZaaerId);

            var found = await _context.ExpenseCategories
                .AsNoTracking()
                .AnyAsync(ec =>
                    ec.ExpenseCategoryId == expenseCategoryId &&
                    ec.IsActive &&
                    hotelIds.Contains(ec.HotelId));

            if (found)
            {
                return true;
            }

            return await _context.ExpenseCategories
                .AsNoTracking()
                .AnyAsync(ec => ec.ExpenseCategoryId == expenseCategoryId && ec.IsActive);
        }

        /// <summary>
        /// تحديث نفقة موجودة
        /// ✅ Transaction Management: All operations wrapped in a single transaction for atomicity
        /// </summary>
        public async Task<ExpenseResponseDto?> UpdateAsync(long id, UpdateExpenseDto dto)
        {
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] ========== START ==========");
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] ExpenseId: {ExpenseId}", id);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] UpdateExpenseDto: {@UpdateExpenseDto}", dto);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] DTO Properties - ExpenseCategoryId: {ExpenseCategoryId}, TotalAmount: {TotalAmount}, TaxAmount: {TaxAmount}, TaxRate: {TaxRate}, ApprovalStatus: {ApprovalStatus}, PaymentSource: {PaymentSource}",
                dto.ExpenseCategoryId, dto.TotalAmount, dto.TaxAmount, dto.TaxRate, dto.ApprovalStatus ?? "null", dto.PaymentSource ?? "null");
            
            // ✅ Begin transaction - all operations will be atomic
            await _unitOfWork.BeginTransactionAsync();
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Transaction started");
            
            try
            {
                // ✅ CRITICAL FIX: Get all HotelIds with the same HotelCode (like in GetAllAsync)
                // This handles cases where expenses are linked to different HotelIds but same HotelCode
                var tenant = _tenantService.GetTenant();
                if (tenant == null)
                {
                    throw new InvalidOperationException("Tenant not resolved. Cannot update expense.");
                }

                // ✅ Get database connection info for debugging
                var databaseName = _context.Database.GetDbConnection().Database;
                var serverName = _context.Database.GetDbConnection().DataSource;
                
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Current tenant context: TenantCode={TenantCode}, DatabaseName={DatabaseName}, ConnectedDB={ConnectedDB}, Server={Server}", 
                    tenant.Code, tenant.DatabaseName, databaseName, serverName);

                // ✅ CRITICAL FIX: Since we're already in the correct tenant database context (isolated by TenantCode),
                // and multiple hotels may share the same hotel_id but differ by HotelCode, we should search for
                // expenses by expense_id only, without filtering by hotel_id. The tenant database isolation
                // already ensures we're accessing the correct hotel's data.
                // 
                // For example: Dammam1 and Dammam2 tenant databases may both have hotel_id=1,
                // but they're distinguished by their TenantCode (Dammam1 vs Dammam2) and separate databases.
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Searching for expense by ID only (tenant database isolation): ExpenseId={ExpenseId}, TenantCode={TenantCode}, ConnectedDB={ConnectedDB}", 
                    id, tenant.Code, databaseName);
                
                // ✅ Try using _context directly first (not UnitOfWork) to ensure we're querying the correct database
                var expense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == id);

                if (expense == null)
                {
                    // ✅ DEBUG: Check if expense exists at all in the database (even in other tables)
                    var expenseCount = await _context.Expenses.CountAsync();
                    _logger.LogWarning("⚠️ [ExpenseService.UpdateAsync] Expense not found in current tenant database: ExpenseId={ExpenseId}, TenantCode={TenantCode}, DatabaseName={DatabaseName}, TotalExpensesInDB={TotalExpenses}", 
                        id, tenant.Code, tenant.DatabaseName, expenseCount);
                    
                    // ✅ Try with UnitOfWork as fallback (in case there's a context issue)
                    expense = await _unitOfWork.Expenses.FindSingleAsync(e => e.ExpenseId == id);
                    if (expense != null)
                    {
                        _logger.LogInformation("✅ [ExpenseService.UpdateAsync] Expense found using UnitOfWork fallback: ExpenseId={ExpenseId}", id);
                    }
                }

                if (expense == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return null;
                }

                // ✅ Log the expense's hotel_id for debugging (may be same across different hotels in different tenant DBs)
                _logger.LogInformation("✅ [ExpenseService.UpdateAsync] Expense found: ExpenseId={ExpenseId}, HotelId={HotelId}, TenantCode={TenantCode}", 
                    id, expense.HotelId, tenant.Code);

                var approvalStatus = expense.ApprovalStatus?.Trim();
                
                // ✅ Handle resubmission of rejected expenses FIRST (before editability check)
                // This allows rejected expenses to be updated when resubmitting
                _logger.LogInformation("🔍 [ExpenseService.UpdateAsync] Checking resubmission - CurrentStatus: {CurrentStatus}, DTO.ApprovalStatus: {DtoApprovalStatus}, DTO.PaymentSource: {DtoPaymentSource}", 
                    approvalStatus ?? "null", dto.ApprovalStatus ?? "null", dto.PaymentSource ?? "null");
                
                if (string.Equals(approvalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("🔍 [ExpenseService.UpdateAsync] Expense is rejected, automatically resubmitting...");
                    
                    // ✅ Always resubmit rejected expenses when they are updated
                    // Determine new status based on ApprovalStatus from DTO, or PaymentSource, or existing PaymentSource
                    string? newApprovalStatus = null;
                    
                    // Priority 1: Use ApprovalStatus from DTO if explicitly provided
                    if (!string.IsNullOrWhiteSpace(dto.ApprovalStatus))
                    {
                        newApprovalStatus = dto.ApprovalStatus.Trim();
                        _logger.LogInformation("🔍 [ExpenseService.UpdateAsync] Using ApprovalStatus from DTO: {NewStatus}", newApprovalStatus);
                    }
                    else
                    {
                        // Priority 2: Determine from PaymentSource in DTO
                        string? paymentSourceToCheck = null;
                        if (!string.IsNullOrWhiteSpace(dto.PaymentSource))
                        {
                            paymentSourceToCheck = dto.PaymentSource.Trim();
                        }
                        else if (!string.IsNullOrWhiteSpace(expense.PaymentSource))
                        {
                            paymentSourceToCheck = expense.PaymentSource.Trim();
                        }
                        
                        if (!string.IsNullOrWhiteSpace(paymentSourceToCheck))
                        {
                            if (paymentSourceToCheck.Equals("Management", StringComparison.OrdinalIgnoreCase) ||
                                paymentSourceToCheck.Equals("Managemer", StringComparison.OrdinalIgnoreCase) ||
                                paymentSourceToCheck.StartsWith("Manag", StringComparison.OrdinalIgnoreCase))
                            {
                                newApprovalStatus = "awaiting-officer";
                            }
                            else
                            {
                                newApprovalStatus = "pending";
                            }
                            _logger.LogInformation("🔍 [ExpenseService.UpdateAsync] Determined new status from PaymentSource '{PaymentSource}': {NewStatus}", 
                                paymentSourceToCheck, newApprovalStatus);
                        }
                        else
                        {
                            // Priority 3: Default to pending
                            newApprovalStatus = "pending";
                            _logger.LogInformation("🔍 [ExpenseService.UpdateAsync] No PaymentSource found, defaulting to: {NewStatus}", newApprovalStatus);
                        }
                    }
                    
                    // Always update status and reset rejection fields for rejected expenses
                    _logger.LogInformation("🔄 [ExpenseService.UpdateAsync] Resubmitting rejected expense: ExpenseId={ExpenseId}, NewStatus={NewStatus}", 
                        id, newApprovalStatus);
                    
                    // Update approval status BEFORE editability check
                    expense.ApprovalStatus = newApprovalStatus;
                    
                    // Reset rejection-related fields
                    expense.RejectionReason = null;
                    expense.ApprovedBy = null;
                    expense.ApprovedAt = null;
                    
                    _logger.LogInformation("✅ [ExpenseService.UpdateAsync] Reset rejection fields and updated status to: {NewStatus}", 
                        expense.ApprovalStatus);
                }

                // ✅ Now check editability using the UPDATED status (if resubmitting, status is already changed)
                var currentStatusForCheck = expense.ApprovalStatus?.Trim();
                if (!IsEditableApprovalStatus(currentStatusForCheck))
                {
                    _logger.LogWarning("⚠️ [ExpenseService.UpdateAsync] Attempt to update locked expense: ExpenseId={ExpenseId}, Status={Status}", id, currentStatusForCheck);
                    await _unitOfWork.RollbackTransactionAsync();
                    throw new InvalidOperationException(
                        $"لا يمكن تعديل هذا المصروف لأن حالته الحالية هي '{currentStatusForCheck ?? "غير معروفة"}'. يرجى تحديث الصفحة قبل إعادة المحاولة.");
                }

                // ✅ Update PaymentSource if provided
                if (!string.IsNullOrWhiteSpace(dto.PaymentSource))
                {
                    string paymentSource = dto.PaymentSource.Trim();
                    // Normalize payment source (handle typos)
                    if (paymentSource.Equals("Management", StringComparison.OrdinalIgnoreCase) ||
                        paymentSource.Equals("Managemer", StringComparison.OrdinalIgnoreCase) ||
                        paymentSource.StartsWith("Manag", StringComparison.OrdinalIgnoreCase))
                    {
                        paymentSource = "Management";
                    }
                    else
                    {
                        paymentSource = "Branch";
                    }
                    
                    expense.PaymentSource = paymentSource;
                    _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated PaymentSource: {PaymentSource}", expense.PaymentSource);
                }

                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Expense found - Current values:");
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   ExpenseCategoryId: {ExpenseCategoryId}", expense.ExpenseCategoryId);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TotalAmount: {TotalAmount}", expense.TotalAmount);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TaxAmount: {TaxAmount}", expense.TaxAmount);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TaxRate: {TaxRate}", expense.TaxRate);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   DateTime: {DateTime}", expense.DateTime);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   DueDate: {DueDate}", expense.DueDate);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   Comment: {Comment}", expense.Comment);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   ApprovalStatus: {ApprovalStatus}", expense.ApprovalStatus);
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   PaymentSource: {PaymentSource}", expense.PaymentSource);

            // تحديث الحقول
            if (dto.DateTime.HasValue)
            {
                // ✅ Convert DateTime to KSA timezone using KsaTime
                DateTime updatedDateTime = dto.DateTime.Value.Kind == DateTimeKind.Utc 
                    ? KsaTime.ConvertFromUtc(dto.DateTime.Value) 
                    : KsaTime.ConvertFromUtc(dto.DateTime.Value.ToUniversalTime());
                expense.DateTime = updatedDateTime; // ✅ Use KSA time
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated DateTime: {DateTime}", expense.DateTime);
            }
            //(Logic for adjusting DueDate per KSA "end of day" hour like in CreateAsync)
            if (dto.DueDate.HasValue)
            {
                int saDayEndHour = 4; // Default (could move to const/config if preferred)
                try
                {
                    var saDayEndHourStr = _configuration["SADayEndHour"];
                    if (int.TryParse(saDayEndHourStr, out var configuredHour) && configuredHour > 0 && configuredHour < 24)
                    {
                        saDayEndHour = configuredHour;
                    }
                }
                catch { /* safe default */ }

                // Always update to KSA-date (converted), then apply the "previous day if < saDayEndHour" rule
                DateTime localDueDateKsa = dto.DueDate.Value.Kind == DateTimeKind.Utc
                    ? KsaTime.ConvertFromUtc(dto.DueDate.Value)
                    : KsaTime.ConvertFromUtc(dto.DueDate.Value.ToUniversalTime());

                DateTime? adjustedDueDate = null;
                if (localDueDateKsa.TimeOfDay < TimeSpan.FromHours(saDayEndHour))
                {
                    adjustedDueDate = localDueDateKsa.Date.AddDays(-1);
                }
                else
                {
                    adjustedDueDate = localDueDateKsa.Date;
                }

                expense.DueDate = adjustedDueDate;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated DueDate (adjusted for SA day end): {DueDate}", expense.DueDate);
            }
            // else: If DueDate is not provided at all, do not touch the existing value (leave as is)
            if (dto.Comment != null)
            {
                expense.Comment = dto.Comment;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated Comment: {Comment}", expense.Comment);
            }
            if (dto.ExpenseCategoryId.HasValue)
            {
                if (dto.UseTenantCategories)
                {
                    var hotelSettingsForCategory = await _unitOfWork.HotelSettings
                        .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower());
                    if (hotelSettingsForCategory != null && hotelSettingsForCategory.ZaaerId.HasValue)
                    {
                        var categoryExists = await TenantExpenseCategoryExistsAsync(
                            dto.ExpenseCategoryId.Value,
                            tenant.Code,
                            hotelSettingsForCategory.HotelId,
                            hotelSettingsForCategory.ZaaerId.Value);

                        if (!categoryExists)
                        {
                            throw new InvalidOperationException(
                                $"Expense category with ID {dto.ExpenseCategoryId.Value} not found for this hotel.");
                        }
                    }
                }

                var oldCategoryId = expense.ExpenseCategoryId;
                expense.ExpenseCategoryId = dto.ExpenseCategoryId;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated ExpenseCategoryId: {OldCategoryId} -> {NewCategoryId}", 
                    oldCategoryId, expense.ExpenseCategoryId);
            }
            
            // Handle tax fields - update if provided
            // Note: If both are null, we keep existing values (don't clear)
            // To clear tax, explicitly set both to 0 or handle separately
            if (dto.TaxRate.HasValue)
            {
                var oldTaxRate = expense.TaxRate;
                expense.TaxRate = dto.TaxRate.Value;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated TaxRate: {OldTaxRate} -> {NewTaxRate}", 
                    oldTaxRate, expense.TaxRate);
            }
            else if (dto.TaxRate == null && !dto.TaxAmount.HasValue)
            {
                // If TaxRate is explicitly null and TaxAmount is also null/not provided, clear tax
                // This handles the case when checkbox is unchecked
                expense.TaxRate = null;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Cleared TaxRate (both TaxRate and TaxAmount are null)");
            }
            
            if (dto.TaxAmount.HasValue)
            {
                var oldTaxAmount = expense.TaxAmount;
                expense.TaxAmount = dto.TaxAmount.Value;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated TaxAmount: {OldTaxAmount} -> {NewTaxAmount}", 
                    oldTaxAmount, expense.TaxAmount);
            }
            else if (dto.TaxAmount == null && !dto.TaxRate.HasValue)
            {
                // If TaxAmount is explicitly null and TaxRate is also null/not provided, clear tax
                expense.TaxAmount = null;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Cleared TaxAmount (both TaxRate and TaxAmount are null)");
            }

            if (dto.BeforeTaxAmount.HasValue)
            {
                expense.BeforeTaxAmount = dto.BeforeTaxAmount.Value;
            }
            else if (dto.TotalAmount.HasValue || dto.TaxAmount.HasValue || dto.TaxRate.HasValue)
            {
                expense.BeforeTaxAmount = expense.TaxAmount.HasValue
                    ? expense.TotalAmount - expense.TaxAmount.Value
                    : expense.TotalAmount;
            }
            else if (expense.TaxAmount == null && expense.TaxRate == null)
            {
                expense.BeforeTaxAmount = expense.TotalAmount;
            }
            
            if (dto.TotalAmount.HasValue)
            {
                var oldTotalAmount = expense.TotalAmount;
                expense.TotalAmount = dto.TotalAmount.Value;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Updated TotalAmount: {OldTotalAmount} -> {NewTotalAmount}", 
                    oldTotalAmount, expense.TotalAmount);
            }

            expense.UpdatedAt = KsaTime.Now;
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Set UpdatedAt: {UpdatedAt}", expense.UpdatedAt);
            
            // ✅ Capture current user ID who is updating the expense
            int? currentUserId = null;
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("UserId", out var userIdObj) == true && userIdObj != null)
            {
                if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                {
                    currentUserId = parsedUserId;
                    expense.UpdatedBy = currentUserId;
                    _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Set UpdatedBy: {UpdatedBy}", expense.UpdatedBy);
                }
            }
            
            if (currentUserId == null)
            {
                _logger.LogWarning("⚠️ [ExpenseService.UpdateAsync] Could not determine current user ID from HttpContext");
            }

            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Final expense values before save:");
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   ExpenseCategoryId: {ExpenseCategoryId}", expense.ExpenseCategoryId);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TotalAmount: {TotalAmount}", expense.TotalAmount);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TaxAmount: {TaxAmount}", expense.TaxAmount);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync]   TaxRate: {TaxRate}", expense.TaxRate);

            // Expense is already tracked from the load above — avoid Update() which marks every column (including IDENTITY old_expense_id) as modified.
            DetachExpenseIdentityColumns(expense);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Expense tracked for save (identity columns excluded)");

            // ✅ Update expense rooms if provided (same logic as CreateAsync)
            if (dto.ExpenseRooms != null && dto.ExpenseRooms.Any())
            {
                // Delete existing expense rooms first
                var existingRooms = await _context.ExpenseRooms
                    .Where(er => er.ExpenseId == expense.ExpenseId)
                    .ToListAsync();

                if (existingRooms.Any())
                {
                    foreach (var existingRoom in existingRooms)
                    {
                        await _unitOfWork.ExpenseRooms.DeleteAsync(existingRoom);
                    }
                    // ✅ Save changes after deleting old rooms before adding new ones
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("✅ [UpdateAsync] Deleted {Count} existing expense rooms", existingRooms.Count);
                }

                // ✅ Get all HotelIds with the same HotelCode (like in CreateAsync)
                // Reuse the tenant variable from the outer scope
                if (tenant == null)
                {
                    throw new InvalidOperationException("Tenant not resolved. Cannot update expense rooms.");
                }
                
                var hotelSettings = await _unitOfWork.HotelSettings
                    .FindSingleAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower());
                
                var hotelCode = hotelSettings?.HotelCode ?? tenant.Code;
                
                // Get all HotelIds with the same HotelCode
                var allHotelIdsWithSameCode = await _context.HotelSettings
                    .AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower())
                    .Select(h => h.HotelId)
                    .ToListAsync();

                // Add new expense rooms (same logic as CreateAsync)
                foreach (var roomDto in dto.ExpenseRooms)
                {
                    // ✅ Check if it's a category (CAT_BUILDING, CAT_RECEPTION, CAT_CORRIDORS) or actual room
                    if (!string.IsNullOrEmpty(roomDto.CategoryCode) && roomDto.CategoryCode.StartsWith("CAT_"))
                    {
                        // ✅ It's a room category
                        var categoryExpenseRoom = new ExpenseRoomModel
                        {
                            ExpenseId = expense.ExpenseId,
                            ZaaerId = null, // ✅ Use null for categories (ZaaerId is nullable)
                            Purpose = roomDto.CategoryCode + (string.IsNullOrEmpty(roomDto.Purpose) ? "" : " - " + roomDto.Purpose),
                            Amount = roomDto.Amount,
                            CreatedAt = KsaTime.Now
                        };

                        await _unitOfWork.ExpenseRooms.AddAsync(categoryExpenseRoom);
                        _logger.LogInformation("✅ [UpdateAsync] Added ExpenseRoom with Category: ExpenseId={ExpenseId}, CategoryCode={CategoryCode}, Purpose={Purpose}, Amount={Amount}", 
                            expense.ExpenseId, roomDto.CategoryCode, roomDto.Purpose, roomDto.Amount);
                        continue;
                    }

                    // ✅ Search for Apartment using ApartmentId or ZaaerId
                    Apartment? apartment = null;
                    
                    if (roomDto.ApartmentId.HasValue)
                    {
                        apartment = await _context.Apartments
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.ApartmentId == roomDto.ApartmentId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));
                    }
                    else if (roomDto.ZaaerId.HasValue)
                    {
                        _logger.LogInformation("🔍 [UpdateAsync] Searching for apartment with ZaaerId={ZaaerId}, HotelIds={HotelIds}", 
                            roomDto.ZaaerId.Value, string.Join(", ", allHotelIdsWithSameCode));
                        
                        apartment = await _context.Apartments
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));
                        
                        if (apartment == null)
                        {
                            // ✅ Try searching without HotelId filter as fallback
                            _logger.LogWarning("⚠️ [UpdateAsync] Apartment not found with HotelId filter, trying without filter...");
                            apartment = await _context.Apartments
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value);
                        }
                    }

                    if (apartment == null)
                    {
                        _logger.LogError("❌ [UpdateAsync] Apartment not found: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, HotelIds={HotelIds}", 
                            roomDto.ApartmentId, roomDto.ZaaerId, string.Join(", ", allHotelIdsWithSameCode));
                        continue;
                    }

                    _logger.LogInformation("✅ [UpdateAsync] Found apartment: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, Name={Name}, HotelId={HotelId}", 
                        apartment.ApartmentId, apartment.ZaaerId, apartment.ApartmentName, apartment.HotelId);

                    // ✅ Save zaaerId directly (Foreign Key to apartments.zaaer_id)
                    if (!apartment.ZaaerId.HasValue)
                    {
                        _logger.LogWarning("⚠️ [UpdateAsync] Apartment found but ZaaerId is null: ApartmentId={ApartmentId}, Name={Name}", 
                            apartment.ApartmentId, apartment.ApartmentName);
                        continue; // Skip if apartment doesn't have zaaerId
                    }

                    var roomExpenseRoom = new ExpenseRoomModel
                    {
                        ExpenseId = expense.ExpenseId,
                        ZaaerId = apartment.ZaaerId.Value, // ✅ حفظ zaaerId مباشرة (Foreign Key to apartments.zaaer_id)
                        Purpose = roomDto.Purpose,
                        Amount = roomDto.Amount,
                        CreatedAt = KsaTime.Now
                    };

                    await _unitOfWork.ExpenseRooms.AddAsync(roomExpenseRoom);
                    _logger.LogInformation("✅ [UpdateAsync] Added ExpenseRoom: ExpenseId={ExpenseId}, ZaaerId={ZaaerId}, Purpose={Purpose}, Amount={Amount}", 
                        expense.ExpenseId, apartment.ZaaerId.Value, roomDto.Purpose, roomDto.Amount);
                }
                
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Saved expense rooms changes");
            }

            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Calling SaveChangesAsync for expense update...");
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] SaveChangesAsync completed");

            // ✅ Commit transaction - all operations succeeded
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Committing transaction...");
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Transaction committed successfully");

            _logger.LogInformation("✅ [ExpenseService.UpdateAsync] Expense updated successfully: ExpenseId={ExpenseId}", expense.ExpenseId);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync] Final saved expense values:");
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   ExpenseCategoryId: {ExpenseCategoryId}", expense.ExpenseCategoryId);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   TotalAmount: {TotalAmount}", expense.TotalAmount);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   TaxAmount: {TaxAmount}", expense.TaxAmount);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   TaxRate: {TaxRate}", expense.TaxRate);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   ApprovalStatus: {ApprovalStatus}", expense.ApprovalStatus);
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   RejectionReason: {RejectionReason}", expense.RejectionReason ?? "null");
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   ApprovedBy: {ApprovedBy}", expense.ApprovedBy?.ToString() ?? "null");
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   ApprovedAt: {ApprovedAt}", expense.ApprovedAt?.ToString() ?? "null");
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync]   PaymentSource: {PaymentSource}", expense.PaymentSource ?? "null");

            // ✅ Get category name from Master DB for response
            string? categoryName = null;
            if (expense.ExpenseCategoryId.HasValue)
            {
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Fetching category name from Master DB: CategoryId={CategoryId}", expense.ExpenseCategoryId);
                var masterCategory = await _masterDbContext.ExpenseCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ec => ec.Id == expense.ExpenseCategoryId.Value);
                categoryName = masterCategory?.MainCategory;
                _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Category name: {CategoryName}", categoryName);
            }

            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] Fetching updated expense via GetByIdAsync...");
            var result = await GetByIdAsync(expense.ExpenseId);
            _logger.LogInformation("🟢 [ExpenseService.UpdateAsync] GetByIdAsync returned - ExpenseCategoryId: {ExpenseCategoryId}, TotalAmount: {TotalAmount}, TaxAmount: {TaxAmount}, ApprovalStatus: {ApprovalStatus}, RejectionReason: {RejectionReason}",
                result?.ExpenseCategoryId, result?.TotalAmount, result?.TaxAmount, result?.ApprovalStatus ?? "null", result?.RejectionReason ?? "null");
            _logger.LogInformation("✅ [ExpenseService.UpdateAsync] ========== SUCCESS ==========");
            return result;
            }
            catch (Exception ex)
            {
                // ✅ Rollback transaction on any error - ensures data consistency
                _logger.LogError("❌ [ExpenseService.UpdateAsync] ========== ERROR ==========");
                _logger.LogError("❌ [ExpenseService.UpdateAsync] Error updating expense: ExpenseId={ExpenseId}, Message={Message}", id, ex.Message);
                _logger.LogError("❌ [ExpenseService.UpdateAsync] StackTrace: {StackTrace}", ex.StackTrace);
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError("❌ [ExpenseService.UpdateAsync] Transaction rolled back");
                _logger.LogError("❌ [ExpenseService.UpdateAsync] ========== END ERROR ==========");
                throw; // Re-throw to let caller handle
            }
        }

        /// <summary>
        /// حذف نفقة
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<bool> DeleteAsync(long id)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            var expense = await _context.Expenses
                .Include(e => e.ExpenseRooms)
                .FirstOrDefaultAsync(e => e.ExpenseId == id && e.HotelId == hotelId);

            if (expense == null)
            {
                return false;
            }

            var approvalStatus = expense.ApprovalStatus?.Trim();
            if (!IsEditableApprovalStatus(approvalStatus))
            {
                _logger.LogWarning("⚠️ [ExpenseService.DeleteAsync] Attempt to delete locked expense: ExpenseId={ExpenseId}, Status={Status}", id, approvalStatus);
                throw new InvalidOperationException(
                    $"لا يمكن حذف هذا المصروف لأن حالته الحالية هي '{approvalStatus ?? "غير معروفة"}'. يرجى تحديث الصفحة قبل إعادة المحاولة.");
            }

            // حذف expense_rooms أولاً (Cascade delete)
            if (expense.ExpenseRooms != null && expense.ExpenseRooms.Any())
            {
                foreach (var expenseRoom in expense.ExpenseRooms)
                {
                    await _unitOfWork.ExpenseRooms.DeleteAsync(expenseRoom);
                }
            }

            await _unitOfWork.Expenses.DeleteAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("✅ Expense deleted successfully: ExpenseId={ExpenseId}", id);

            return true;
        }

        /// <summary>
        /// الحصول على جميع expense_rooms لنفقة محددة
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<IEnumerable<ExpenseRoomResponseDto>> GetExpenseRoomsAsync(long expenseId)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            // التحقق من أن Expense موجود في نفس الفندق
            var expense = await _unitOfWork.Expenses
                .FindSingleAsync(e => e.ExpenseId == expenseId && e.HotelId == hotelId);

            if (expense == null)
            {
                throw new KeyNotFoundException($"Expense with id {expenseId} not found");
            }

            // Use context for complex query with Include
            var expenseRooms = await _context.ExpenseRooms
                .AsNoTracking()
                .Include(er => er.Apartment)
                .Where(er => er.ExpenseId == expenseId)
                .OrderBy(er => er.CreatedAt)
                .ToListAsync();

            return expenseRooms.Select(MapExpenseRoomToDto);
        }

        /// <summary>
        /// إضافة غرفة إلى نفقة
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<ExpenseRoomResponseDto> AddExpenseRoomAsync(long expenseId, CreateExpenseRoomDto dto)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            // التحقق من أن Expense موجود في نفس الفندق
            var expense = await _unitOfWork.Expenses
                .FindSingleAsync(e => e.ExpenseId == expenseId && e.HotelId == hotelId);

            if (expense == null)
            {
                throw new KeyNotFoundException($"Expense with id {expenseId} not found");
            }

            // ✅ البحث عن Apartment باستخدام ApartmentId أو ZaaerId
            Apartment? apartment = null;
            
            if (dto.ApartmentId.HasValue)
            {
                // البحث باستخدام ApartmentId
                apartment = await _unitOfWork.Apartments
                    .FindSingleAsync(a => a.ApartmentId == dto.ApartmentId.Value && a.HotelId == hotelId);
            }
            else if (dto.ZaaerId.HasValue)
            {
                // ✅ البحث باستخدام ZaaerId (من الـ frontend)
                apartment = await _unitOfWork.Apartments
                    .FindSingleAsync(a => a.ZaaerId == dto.ZaaerId.Value && a.HotelId == hotelId);
                
                _logger.LogInformation("🔍 [AddExpenseRoomAsync] Searching for apartment with ZaaerId={ZaaerId}, HotelId={HotelId}", 
                    dto.ZaaerId.Value, hotelId);
            }

            if (apartment == null)
            {
                throw new KeyNotFoundException($"Apartment not found: ApartmentId={dto.ApartmentId}, ZaaerId={dto.ZaaerId}, HotelId={hotelId}");
            }

            _logger.LogInformation("✅ [AddExpenseRoomAsync] Found apartment: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, Name={Name}", 
                apartment.ApartmentId, apartment.ZaaerId, apartment.ApartmentName);

            // ✅ Save zaaerId directly (Foreign Key to apartments.zaaer_id)
            if (!apartment.ZaaerId.HasValue)
            {
                throw new InvalidOperationException($"Apartment found but ZaaerId is null: ApartmentId={apartment.ApartmentId}, Name={apartment.ApartmentName}");
            }

            var expenseRoom = new ExpenseRoomModel
            {
                ExpenseId = expenseId,
                ZaaerId = apartment.ZaaerId.Value, // ✅ حفظ zaaerId مباشرة (Foreign Key to apartments.zaaer_id)
                Purpose = dto.Purpose,
                Amount = dto.Amount,
                CreatedAt = KsaTime.Now
            };

            await _unitOfWork.ExpenseRooms.AddAsync(expenseRoom);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("✅ ExpenseRoom added successfully: ExpenseRoomId={ExpenseRoomId}, ExpenseId={ExpenseId}, ZaaerId={ZaaerId}", 
                expenseRoom.ExpenseRoomId, expenseId, apartment.ZaaerId.Value);

            return await MapExpenseRoomToDtoWithLoadAsync(expenseRoom.ExpenseRoomId);
        }

        /// <summary>
        /// تحديث expense_room
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<ExpenseRoomResponseDto?> UpdateExpenseRoomAsync(int expenseRoomId, UpdateExpenseRoomDto dto)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            // Use context for complex query with Include
            var expenseRoom = await _context.ExpenseRooms
                .Include(er => er.Expense)
                .FirstOrDefaultAsync(er => er.ExpenseRoomId == expenseRoomId);

            if (expenseRoom == null || expenseRoom.Expense.HotelId != hotelId)
            {
                return null;
            }

            // ✅ التحقق من Apartment إذا تم تحديثه (باستخدام ZaaerId)
            if (dto.ZaaerId.HasValue)
            {
                var apartment = await _unitOfWork.Apartments
                    .FindSingleAsync(a => a.ZaaerId == dto.ZaaerId.Value && a.HotelId == hotelId);

                if (apartment == null)
                {
                    throw new KeyNotFoundException($"Apartment with ZaaerId {dto.ZaaerId.Value} not found");
                }

                if (!apartment.ZaaerId.HasValue)
                {
                    throw new InvalidOperationException($"Apartment found but ZaaerId is null: ApartmentId={apartment.ApartmentId}");
                }

                expenseRoom.ZaaerId = apartment.ZaaerId.Value; // ✅ تحديث zaaerId (Foreign Key to apartments.zaaer_id)
            }
            else if (dto.ApartmentId.HasValue)
            {
                // ✅ Fallback: البحث باستخدام ApartmentId ثم استخدام ZaaerId
                var apartment = await _unitOfWork.Apartments
                    .FindSingleAsync(a => a.ApartmentId == dto.ApartmentId.Value && a.HotelId == hotelId);

                if (apartment == null)
                {
                    throw new KeyNotFoundException($"Apartment with id {dto.ApartmentId.Value} not found");
                }

                if (!apartment.ZaaerId.HasValue)
                {
                    throw new InvalidOperationException($"Apartment found but ZaaerId is null: ApartmentId={apartment.ApartmentId}");
                }

                expenseRoom.ZaaerId = apartment.ZaaerId.Value; // ✅ تحديث zaaerId (Foreign Key to apartments.zaaer_id)
            }

            if (dto.Purpose != null)
                expenseRoom.Purpose = dto.Purpose;

            // ✅ تحديث Amount إذا كان موجوداً
            if (dto.Amount.HasValue)
                expenseRoom.Amount = dto.Amount.Value;

            await _unitOfWork.ExpenseRooms.UpdateAsync(expenseRoom);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("✅ ExpenseRoom updated successfully: ExpenseRoomId={ExpenseRoomId}", expenseRoomId);

            return await MapExpenseRoomToDtoWithLoadAsync(expenseRoomId);
        }

        /// <summary>
        /// حذف expense_room
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        public async Task<bool> DeleteExpenseRoomAsync(int expenseRoomId)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            // Use context for complex query with Include
            var expenseRoom = await _context.ExpenseRooms
                .Include(er => er.Expense)
                .FirstOrDefaultAsync(er => er.ExpenseRoomId == expenseRoomId);

            if (expenseRoom == null || expenseRoom.Expense.HotelId != hotelId)
            {
                return false;
            }

            await _unitOfWork.ExpenseRooms.DeleteAsync(expenseRoom);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("✅ ExpenseRoom deleted successfully: ExpenseRoomId={ExpenseRoomId}", expenseRoomId);

            return true;
        }

        /// <summary>
        /// الموافقة أو الرفض على مصروف
        /// Approve or reject an expense
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        /// <param name="id">معرف المصروف</param>
        /// <param name="status">حالة الموافقة (accepted أو rejected)</param>
        /// <param name="approvedBy">معرف المستخدم الذي وافق/رفض</param>
        /// <param name="rejectionReason">سبب الرفض (في حالة الرفض)</param>
        /// <param name="recommendation">التوصية (اختياري)</param>
        /// <param name="recommendationToUserId">معرف المستخدم المستهدف للتوصية (NULL = للجميع)</param>
        /// <returns>المصروف المُحدّث</returns>
        public async Task<ExpenseResponseDto?> ApproveExpenseAsync(long id, string status, int approvedBy, string? rejectionReason = null, string? recommendation = null, int? recommendationToUserId = null)
        {
            await _unitOfWork.BeginTransactionAsync();

            try
            {
            // ✅ محاولة الحصول على hotelId، لكن إذا فشل، نبحث بدون filter
            // هذا يسمح للمشرفين بالموافقة/الرفض بدون تسجيل دخول
            int? hotelId = null;
            try
            {
                hotelId = await GetCurrentHotelIdAsync();
                _logger.LogInformation("🔍 [ApproveExpenseAsync] Searching expense for ExpenseId: {ExpenseId} using HotelId (ZaaerId): {HotelId}", 
                    id, hotelId);
            }
            catch (InvalidOperationException)
            {
                // ✅ إذا لم يكن هناك X-Hotel-Code header، نبحث بدون hotel filter
                _logger.LogInformation("⚠️ No X-Hotel-Code header found for approval, searching expense without hotel filter (for public approval access)");
            }

            var expense = hotelId.HasValue
                ? await _unitOfWork.Expenses
                    .FindSingleAsync(e => e.ExpenseId == id && e.HotelId == hotelId.Value)
                : await _unitOfWork.Expenses
                    .FindSingleAsync(e => e.ExpenseId == id);

            if (expense == null)
            {
                _logger.LogWarning("⚠️ Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId}", 
                    id, hotelId?.ToString() ?? "null");
                await _unitOfWork.RollbackTransactionAsync();
                return null;
            }

            var oldExpenseId = await EnsureExpenseOldExpenseIdAsync(expense);

            // ✅ SECURITY: Use database rules to determine next status (ignore frontend status for approvals)
            string previousStatus = expense.ApprovalStatus ?? "";
            string finalStatus = status; // Store final status for history
            
            // ✅ If status is "rejected", allow it directly (security exception for explicit rejection)
            if (status == "rejected")
            {
                expense.ApprovalStatus = status;
                finalStatus = status;
                _logger.LogInformation("✅ Direct rejection allowed: Status={Status}", status);
            }
            else
            {
                // ✅ Get user roles to determine which rule to apply
                var userRoles = await _masterDbContext.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == approvedBy)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var primaryRole = userRoles.FirstOrDefault() ?? "";
                
                // ✅ VALIDATION: Verifier can ONLY approve expenses with Categories 169, 170, or 172
                if (primaryRole == "verifier" && previousStatus.ToLower() == "awaiting-verifier")
                {
                    if (!expense.ExpenseCategoryId.HasValue || 
                        (expense.ExpenseCategoryId.Value != 169 && expense.ExpenseCategoryId.Value != 170 && expense.ExpenseCategoryId.Value != 172))
                    {
                        _logger.LogWarning("⚠️ [ApproveExpenseAsync] Verifier attempted to approve expense with invalid category: ExpenseId={ExpenseId}, CategoryId={CategoryId}", 
                            id, expense.ExpenseCategoryId);
                        throw new InvalidOperationException(
                            $"Verifier can only approve expenses with Categories 169, 170, or 172. Current category: {expense.ExpenseCategoryId?.ToString() ?? "null"}");
                    }
                }
                
                _logger.LogInformation("✅ [ApproveExpenseAsync] Using rule service: Role={Role}, FromStatus={FromStatus}, Amount={Amount}, Category={Category}",
                    primaryRole, previousStatus, expense.TotalAmount, expense.ExpenseCategoryId);

                // ✅ Use database rules to determine next status
                var ruleDeterminedStatus = await _ruleService.GetNextStatusAsync(
                    primaryRole,
                    previousStatus,
                    expense.TotalAmount,
                    expense.ExpenseCategoryId);

                if (!string.IsNullOrWhiteSpace(ruleDeterminedStatus))
                {
                    finalStatus = ruleDeterminedStatus;
                    expense.ApprovalStatus = ruleDeterminedStatus;
                    _logger.LogInformation("✅ [ApproveExpenseAsync] Rule determined status: {Status} (frontend requested: {FrontendStatus})",
                        ruleDeterminedStatus, status);
                }
                else
                {
                    // Fallback to frontend status if no rule matches (should not happen, but for safety)
                expense.ApprovalStatus = status;
                finalStatus = status;
                    _logger.LogWarning("⚠️ [ApproveExpenseAsync] No rule found, using frontend status as fallback: {Status}", status);
                }

                // ✅ Use finalStatus (rule-determined) to set ApprovedBy/ApprovedAt
                bool awaitingNextLevel = finalStatus == "awaiting-manager" || finalStatus == "awaiting-accountant" || finalStatus == "awaiting-admin" || finalStatus == "awaiting-verifier" || finalStatus == "pending";
                if (awaitingNextLevel)
                {
                    // لا يتم تعيين بيانات الموافقة عند الانتقال لمستوى أعلى
                    expense.ApprovedBy = null;
                    expense.ApprovedAt = null;
                }
                else if (finalStatus == "accepted")
                {
                    // ✅ Set ApprovedBy/ApprovedAt only when status is "accepted"
                    expense.ApprovedBy = approvedBy;
                    expense.ApprovedAt = KsaTime.Now;
                }
            }

            expense.UpdatedAt = KsaTime.Now;
            
            // ✅ تحديث سبب الرفض إذا كان موجوداً
            if (status == "rejected" && !string.IsNullOrWhiteSpace(rejectionReason))
            {
                expense.RejectionReason = rejectionReason;
            }
            else if (status != "rejected")
            {
                // ✅ مسح سبب الرفض إذا تمت الموافقة
                expense.RejectionReason = null;
            }

            await _unitOfWork.Expenses.UpdateAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            // حفظ سجل الموافقة/الرفض في ExpenseApprovalHistory
            string? actionByFullName = null;
            if (approvedBy > 0)
            {
                var masterUser = await _masterDbContext.MasterUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == approvedBy);
                actionByFullName = masterUser?.FullName ?? masterUser?.Username;
            }

            string action = finalStatus switch
            {
                "pending" when previousStatus.Equals("awaiting-officer", StringComparison.OrdinalIgnoreCase) => "approved-officer",
                "accepted" when previousStatus.Equals("awaiting-verifier", StringComparison.OrdinalIgnoreCase) => "approved-verifier",
                "awaiting-admin" when previousStatus.Equals("awaiting-verifier", StringComparison.OrdinalIgnoreCase) => "forwarded-verifier",
                "accepted" => "approved",
                "rejected" => "rejected",
                "awaiting-manager" => "awaiting-manager",
                "awaiting-accountant" => "awaiting-accountant",
                "awaiting-admin" => "awaiting-admin",
                "awaiting-verifier" => "awaiting-verifier",
                _ => "updated"
            };

            string comments = finalStatus switch
            {
                "pending" when previousStatus.Equals("awaiting-officer", StringComparison.OrdinalIgnoreCase) => 
                    "تمت موافقة مسؤول المشتريات، في انتظار موافقة المشرف",
                "accepted" when previousStatus.Equals("awaiting-verifier", StringComparison.OrdinalIgnoreCase) => 
                    "تمت موافقة مسؤول الحساب الجاري على المصروف (المبلغ أقل من أو يساوي 100 ريال)",
                "awaiting-admin" when previousStatus.Equals("awaiting-verifier", StringComparison.OrdinalIgnoreCase) => 
                    "تم تحويل المصروف من مسؤول الحساب الجاري إلى المدير العام (المبلغ أكبر من 100 ريال)",
                "accepted" => "تم الموافقة على المصروف",
                "rejected" => $"تم رفض المصروف{(string.IsNullOrWhiteSpace(rejectionReason) ? "" : $": {rejectionReason}")}",
                "awaiting-manager" => "في انتظار موافقة مدير العمليات",
                "awaiting-accountant" => "في انتظار موافقة المحاسب",
                "awaiting-admin" => "في انتظار موافقة المدير العام",
                "awaiting-verifier" => "في انتظار مسؤول الجاري",
                _ => "تم تحديث حالة المصروف"
            };

            await AddApprovalHistoryAsync(
                expense.ExpenseId,
                oldExpenseId,
                action: action,
                status: finalStatus,
                actionBy: approvedBy > 0 ? approvedBy : null,
                actionByFullName: actionByFullName,
                rejectionReason: finalStatus == "rejected" ? rejectionReason : null,
                comments: comments,
                recommendation: recommendation,
                recommendationToUserId: recommendationToUserId);

            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("✅ Expense approval updated: ExpenseId={ExpenseId}, Status={Status}, ApprovedBy={ApprovedBy}, ApprovedAt={ApprovedAt}", 
                id, status, approvedBy, expense.ApprovedAt);

            return await GetByIdAsync(expense.ExpenseId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "❌ ApproveExpenseAsync failed for ExpenseId={ExpenseId}", id);
                throw;
            }
        }

        /// <summary>
        /// الحصول على سجل موافقات المصروف
        /// Get expense approval history
        /// ✅ Uses zaaer_id from hotel_settings (which matches Tenants.ZaaerId in Master DB) as hotel_id
        /// </summary>
        /// <param name="expenseId">معرف المصروف</param>
        /// <returns>قائمة سجلات الموافقات</returns>
        public async Task<IEnumerable<ExpenseApprovalHistoryDto>> GetApprovalHistoryAsync(long expenseId)
        {
            var hotelId = await GetCurrentHotelIdAsync();

            // التحقق من أن Expense موجود في نفس الفندق
            var expense = await _unitOfWork.Expenses
                .FindSingleAsync(e => e.ExpenseId == expenseId && e.HotelId == hotelId);

            if (expense == null)
            {
                throw new KeyNotFoundException($"Expense with id {expenseId} not found");
            }

            var history = await _context.ExpenseApprovalHistories
                .AsNoTracking()
                .Where(h => h.ExpenseId == expenseId)
                .OrderBy(h => h.ActionAt)
                .ToListAsync();

            // Get current user ID for checking recommendation read status
            var (currentUserId, _) = await ResolveCurrentUserForAuditAsync();

            // Get unique user IDs to fetch role and tenant info (for ActionBy and RecommendationToUserId)
            var userIds = history
                .Where(h => h.ActionBy.HasValue || h.RecommendationToUserId.HasValue)
                .SelectMany(h => new[] { h.ActionBy, h.RecommendationToUserId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            
            var userInfoDict = new Dictionary<int, (string? fullName, string? role, string? tenantName)>();
            
            if (userIds.Any())
            {
                var users = await _masterDbContext.MasterUsers
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Include(u => u.Tenant)
                    .Where(u => userIds.Contains(u.Id))
                    .ToListAsync();

                foreach (var user in users)
                {
                    // Get primary role (first role or most relevant)
                    var primaryRole = user.UserRoles?.FirstOrDefault()?.Role;
                    var roleName = GetRoleDisplayName(primaryRole?.Code);
                    var tenantName = user.Tenant?.Name;
                    var fullName = user.FullName ?? user.Username;
                    userInfoDict[user.Id] = (fullName, roleName, tenantName);
                }
            }

            return history.Select(h =>
            {
                var dto = new ExpenseApprovalHistoryDto
                {
                    Id = h.Id,
                    ExpenseId = h.ExpenseId,
                    Action = h.Action,
                    ActionBy = h.ActionBy,
                    ActionByFullName = h.ActionByFullName,
                    ActionAt = h.ActionAt,
                    Status = h.Status,
                    RejectionReason = h.RejectionReason,
                    Comments = h.Comments,
                    Recommendation = h.Recommendation,
                    RecommendationToUserId = h.RecommendationToUserId
                };

                if (h.ActionBy.HasValue && userInfoDict.TryGetValue(h.ActionBy.Value, out var actionByInfo))
                {
                    dto.ActionByRole = actionByInfo.role;
                    dto.ActionByTenantName = actionByInfo.tenantName;
                }

                // Get recommendation target user name
                if (h.RecommendationToUserId.HasValue && userInfoDict.TryGetValue(h.RecommendationToUserId.Value, out var targetUserInfo))
                {
                    dto.RecommendationToUserName = targetUserInfo.fullName;
                }

                // Parse recommendation read by list and check if current user read it
                if (!string.IsNullOrWhiteSpace(h.RecommendationReadBy))
                {
                    try
                    {
                        var readByList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(h.RecommendationReadBy);
                        dto.RecommendationReadBy = readByList ?? new List<int>();
                        dto.IsRecommendationReadByCurrentUser = currentUserId.HasValue && dto.RecommendationReadBy.Contains(currentUserId.Value);
                    }
                    catch
                    {
                        dto.RecommendationReadBy = new List<int>();
                        dto.IsRecommendationReadByCurrentUser = false;
                    }
                }
                else
                {
                    dto.RecommendationReadBy = new List<int>();
                    dto.IsRecommendationReadByCurrentUser = false;
                }

                return dto;
            });
        }

        /// <summary>
        /// تحويل Expense إلى ExpenseResponseDto مع hotelName محدد مسبقاً
        /// Used when HotelSettings navigation property doesn't work (FK relationship issue)
        /// </summary>
        private async Task<ExpenseResponseDto> MapToDtoWithHotelNameAsync(ExpenseModel expense, string? categoryName = null, string? hotelName = null)
        {
            // ✅ إنشاء رابط الموافقة فقط للمصروفات في حالة pending
            string? approvalLink = null;
            if (expense.ApprovalStatus == "pending")
            {
                // ✅ استخدام ApprovalBaseUrl من appsettings.json
                var approvalBaseUrl = _configuration["AppSettings:ApprovalBaseUrl"] ?? "https://aleery.tryasp.net";
                // إزالة "/" من النهاية إذا كان موجوداً
                approvalBaseUrl = approvalBaseUrl.TrimEnd('/');
                approvalLink = $"{approvalBaseUrl}/approve-expense.html?id={expense.ExpenseId}";
            }

            // ✅ Get approved by user full name, role, and tenant from Master DB
            string? approvedByFullName = null;
            string? approvedByRole = null;
            string? approvedByTenantName = null;
            if (expense.ApprovedBy.HasValue)
            {
                try
                {
                    var masterUser = await _masterDbContext.MasterUsers
                        .AsNoTracking()
                        .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                        .Include(u => u.Tenant)
                        .FirstOrDefaultAsync(u => u.Id == expense.ApprovedBy.Value);
                    
                    if (masterUser != null)
                    {
                        approvedByFullName = masterUser.FullName ?? masterUser.Username;
                        
                        // Get primary role (first role or most relevant)
                        var primaryRole = masterUser.UserRoles?.FirstOrDefault()?.Role;
                        approvedByRole = GetRoleDisplayName(primaryRole?.Code);
                        
                        approvedByTenantName = masterUser.Tenant?.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch approved by user info for user ID {UserId}", expense.ApprovedBy.Value);
                }
            }

            return new ExpenseResponseDto
            {
                ExpenseId = expense.ExpenseId,
                ExpenseNo = expense.ExpenseNo,
                ExpenseSeq = expense.ExpenseSeq,
                HotelId = expense.HotelId,
                HotelName = hotelName, // ✅ Use provided hotelName
                DateTime = expense.DateTime,
                DueDate = expense.DueDate,
                Comment = expense.Comment,
                ExpenseCategoryId = expense.ExpenseCategoryId,
                ExpenseCategoryName = categoryName, // ✅ Use category name from Master DB
                TaxRate = expense.TaxRate,
                TaxAmount = expense.TaxAmount,
                BeforeTaxAmount = expense.BeforeTaxAmount,
                TotalAmount = expense.TotalAmount,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt,
                UpdatedBy = expense.UpdatedBy,
                ApprovalStatus = expense.ApprovalStatus,
                ApprovedBy = expense.ApprovedBy,
                ApprovedByFullName = approvedByFullName,
                ApprovedByRole = approvedByRole,
                ApprovedByTenantName = approvedByTenantName,
                ApprovedAt = expense.ApprovedAt,
                RejectionReason = expense.RejectionReason,
                ApprovalLink = approvalLink,
                PaymentSource = expense.PaymentSource, // ✅ Add payment source
                ExpenseRooms = expense.ExpenseRooms != null && expense.ExpenseRooms.Any()
                    ? expense.ExpenseRooms.Select(er => {
                        try {
                            return MapExpenseRoomToDto(er);
                        } catch (Exception ex) {
                            _logger.LogError(ex, "❌ [MapToDtoWithHotelNameAsync] Error mapping ExpenseRoom: ExpenseRoomId={ExpenseRoomId}, ExpenseId={ExpenseId}", 
                                er.ExpenseRoomId, er.ExpenseId);
                            // Return a safe default DTO to prevent complete failure
                            return new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                Purpose = er.Purpose ?? "Unknown",
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt
                            };
                        }
                    }).ToList()
                    : new List<ExpenseRoomResponseDto>()
            };
        }

        /// <summary>
        /// تحويل Expense إلى ExpenseResponseDto
        /// </summary>
        private async Task<ExpenseResponseDto> MapToDtoAsync(ExpenseModel expense, string? categoryName = null)
        {
            // ✅ الحصول على اسم الفندق من HotelSettings
            string? hotelName = null;
            if (expense.HotelSettings != null)
            {
                hotelName = expense.HotelSettings.HotelName;
            }
            else if (expense.HotelId > 0)
            {
                // محاولة تحميل HotelSettings إذا لم تكن محملة باستخدام HotelId الداخلي
                var hotelSettings = _context.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefault(h => h.HotelId == expense.HotelId);
                hotelName = hotelSettings?.HotelName;
            }

            // ✅ إنشاء رابط الموافقة فقط للمصروفات في حالة pending
            string? approvalLink = null;
            if (expense.ApprovalStatus == "pending")
            {
                // ✅ استخدام ApprovalBaseUrl من appsettings.json
                var approvalBaseUrl = _configuration["AppSettings:ApprovalBaseUrl"] ?? "https://aleery.tryasp.net";
                // إزالة "/" من النهاية إذا كان موجوداً
                approvalBaseUrl = approvalBaseUrl.TrimEnd('/');
                approvalLink = $"{approvalBaseUrl}/approve-expense.html?id={expense.ExpenseId}";
            }

            // ✅ Get approved by user full name, role, and tenant from Master DB
            string? approvedByFullName = null;
            string? approvedByRole = null;
            string? approvedByTenantName = null;
            if (expense.ApprovedBy.HasValue)
            {
                try
                {
                    var masterUser = await _masterDbContext.MasterUsers
                        .AsNoTracking()
                        .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                        .Include(u => u.Tenant)
                        .FirstOrDefaultAsync(u => u.Id == expense.ApprovedBy.Value);
                    
                    if (masterUser != null)
                    {
                        approvedByFullName = masterUser.FullName ?? masterUser.Username;
                        
                        // Get primary role (first role or most relevant)
                        var primaryRole = masterUser.UserRoles?.FirstOrDefault()?.Role;
                        approvedByRole = GetRoleDisplayName(primaryRole?.Code);
                        
                        approvedByTenantName = masterUser.Tenant?.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch approved by user info for user ID {UserId}", expense.ApprovedBy.Value);
                }
            }

            return new ExpenseResponseDto
            {
                ExpenseId = expense.ExpenseId,
                HotelId = expense.HotelId,
                HotelName = hotelName,
                DateTime = expense.DateTime,
                DueDate = expense.DueDate,
                Comment = expense.Comment,
                ExpenseCategoryId = expense.ExpenseCategoryId,
                ExpenseCategoryName = categoryName, // ✅ Use category name from Master DB
                TaxRate = expense.TaxRate,
                TaxAmount = expense.TaxAmount,
                BeforeTaxAmount = expense.BeforeTaxAmount,
                TotalAmount = expense.TotalAmount,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt,
                UpdatedBy = expense.UpdatedBy,
                ApprovalStatus = expense.ApprovalStatus,
                ApprovedBy = expense.ApprovedBy,
                ApprovedByFullName = approvedByFullName,
                ApprovedByRole = approvedByRole,
                ApprovedByTenantName = approvedByTenantName,
                ApprovedAt = expense.ApprovedAt,
                RejectionReason = expense.RejectionReason,
                ApprovalLink = approvalLink,
                PaymentSource = expense.PaymentSource, // ✅ Add payment source
                ExpenseRooms = expense.ExpenseRooms != null && expense.ExpenseRooms.Any()
                    ? expense.ExpenseRooms.Select(er => {
                        try {
                            return MapExpenseRoomToDto(er);
                        } catch (Exception ex) {
                            _logger.LogError(ex, "❌ [MapToDtoAsync] Error mapping ExpenseRoom: ExpenseRoomId={ExpenseRoomId}, ExpenseId={ExpenseId}", 
                                er.ExpenseRoomId, er.ExpenseId);
                            // Return a safe default DTO to prevent complete failure
                            return new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                Purpose = er.Purpose ?? "Unknown",
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt
                            };
                        }
                    }).ToList()
                    : new List<ExpenseRoomResponseDto>()
            };
        }

        /// <summary>
        /// تحويل Role Code إلى اسم عربي للعرض
        /// Convert Role Code to Arabic display name
        /// </summary>
        private string? GetRoleDisplayName(string? roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
                return null;

            return roleCode.ToLower() switch
            {
                "staff" or "reception staff" => "موظف",
                "supervisor" => "مشرف فرع",
                "manager" => "مدير العمليات",
                "accountant" => "المحاسب",
                "admin" or "administrator" => "المدير العام",
                _ => roleCode
            };
        }

        /// <summary>
        /// تحويل ExpenseRoom إلى ExpenseRoomResponseDto
        /// </summary>
        private ExpenseRoomResponseDto MapExpenseRoomToDto(ExpenseRoomModel expenseRoom)
        {
            if (expenseRoom == null)
            {
                throw new ArgumentNullException(nameof(expenseRoom), "ExpenseRoom cannot be null");
            }
            
            // ✅ Extract category code from purpose if it exists (format: "CAT_XXX - purpose text")
            // أو ZaaerId = null يعني أنه فئة
            string? categoryCode = null;
            string? actualPurpose = expenseRoom.Purpose;
            
            // ✅ Check if ZaaerId is null (for categories) OR purpose starts with CAT_
            if (expenseRoom.ZaaerId == null || (!string.IsNullOrWhiteSpace(expenseRoom.Purpose) && expenseRoom.Purpose.StartsWith("CAT_")))
            {
                // It's a category - extract category code from purpose
                if (!string.IsNullOrWhiteSpace(expenseRoom.Purpose) && expenseRoom.Purpose.StartsWith("CAT_"))
                {
                    var parts = expenseRoom.Purpose.Split(new[] { " - " }, 2, StringSplitOptions.None);
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        categoryCode = parts[0].Trim(); // CAT_BUILDING, CAT_RECEPTION, etc.
                        actualPurpose = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null; // Actual purpose text (after " - ")
                    }
                }
            }
            
            return new ExpenseRoomResponseDto
            {
                ExpenseRoomId = expenseRoom.ExpenseRoomId,
                ExpenseId = expenseRoom.ExpenseId,
                ApartmentId = expenseRoom.Apartment?.ApartmentId, // ✅ For backward compatibility
                ZaaerId = expenseRoom.ZaaerId, // ✅ ZaaerId from expense_rooms.zaaer_id (Foreign Key)
                CategoryCode = categoryCode, // ✅ Category code (null for actual rooms)
                ApartmentCode = expenseRoom.Apartment?.ApartmentCode, // ✅ null for categories
                ApartmentName = expenseRoom.Apartment?.ApartmentName, // ✅ null for categories
                Purpose = actualPurpose, // ✅ Actual purpose without category code
                Amount = expenseRoom.Amount,
                CreatedAt = expenseRoom.CreatedAt
            };
        }

        /// <summary>
        /// تحميل ExpenseRoom من DB وتحويله إلى DTO
        /// </summary>
        private async Task<ExpenseRoomResponseDto> MapExpenseRoomToDtoWithLoadAsync(int expenseRoomId)
        {
            var expenseRoom = await _context.ExpenseRooms
                .AsNoTracking()
                .Include(er => er.Apartment)
                .FirstOrDefaultAsync(er => er.ExpenseRoomId == expenseRoomId);

            if (expenseRoom == null)
            {
                throw new KeyNotFoundException($"ExpenseRoom with id {expenseRoomId} not found");
            }

            return MapExpenseRoomToDto(expenseRoom);
        }
    }
}

