using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Expense;
using zaaerIntegration.Models;
using zaaerIntegration.Services.Expense;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using ExpenseRoomModel = FinanceLedgerAPI.Models.ExpenseRoom;
using Apartment = FinanceLedgerAPI.Models.Apartment;
using Expense = FinanceLedgerAPI.Models.Expense;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Controller لإدارة النفقات (Expenses)
    /// جميع Endpoints تستخدم X-Hotel-Code header للحصول على HotelId
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ExpenseController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly TenantDbContextResolver _dbContextResolver;
        private readonly ITenantService _tenantService;
        private readonly ILogger<ExpenseController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IExpenseApprovalRuleService _ruleService;

        /// <summary>
        /// Constructor for ExpenseController
        /// </summary>
        /// <param name="expenseService">Expense service</param>
        /// <param name="dbContextResolver">Tenant database context resolver</param>
        /// <param name="tenantService">Tenant service</param>
        /// <param name="logger">Logger</param>
        /// <param name="configuration">Configuration for reading app settings</param>
        /// <param name="ruleService">Expense approval rule service</param>
        public ExpenseController(
            IExpenseService expenseService,
            TenantDbContextResolver dbContextResolver,
            ITenantService tenantService,
            ILogger<ExpenseController> logger,
            IConfiguration configuration,
            IExpenseApprovalRuleService ruleService)
        {
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _dbContextResolver = dbContextResolver ?? throw new ArgumentNullException(nameof(dbContextResolver));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        }

        /// <summary>
        /// الحصول على جميع النفقات للفندق الحالي
        /// </summary>
        /// <returns>قائمة النفقات</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetAll()
        {
            try
            {
                var tenant = _tenantService.GetTenant();
                _logger.LogInformation("📋 [ExpenseController.GetAll] Fetching all expenses for current hotel. TenantCode: {TenantCode}", tenant?.Code ?? "Unknown");

                var expenses = await _expenseService.GetAllAsync();
                var expensesList = expenses.ToList();

                _logger.LogInformation("✅ [ExpenseController.GetAll] Successfully retrieved {Count} expenses", expensesList.Count);
                
                // ✅ DEBUG: Log detailed information about each expense being returned
                _logger.LogInformation("🔍 [ExpenseController.GetAll] Expenses being returned to frontend:");
                foreach (var expense in expensesList)
                {
                    _logger.LogInformation("🔍 [ExpenseController.GetAll] Expense: ExpenseId={ExpenseId}, HotelId={HotelId}, DateTime={DateTime}, TotalAmount={TotalAmount}, Status={Status}, CategoryId={CategoryId}, CategoryName={CategoryName}",
                        expense.ExpenseId, expense.HotelId, expense.DateTime, expense.TotalAmount, expense.ApprovalStatus, expense.ExpenseCategoryId, expense.ExpenseCategoryName);
                }
                
                // ✅ DEBUG: Log JSON serialization
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(expensesList, new JsonSerializerOptions { WriteIndented = false });
                    _logger.LogInformation("🔍 [ExpenseController.GetAll] JSON Response (first 500 chars): {Json}", json.Length > 500 ? json.Substring(0, 500) + "..." : json);
                }
                catch (Exception jsonEx)
                {
                    _logger.LogWarning("⚠️ [ExpenseController.GetAll] Failed to serialize expenses to JSON: {Error}", jsonEx.Message);
                }

                // ✅ DEBUG: Add debug info to response headers (for troubleshooting)
                Response.Headers.Add("X-Debug-Expense-Count", expensesList.Count.ToString());
                Response.Headers.Add("X-Debug-Tenant-Code", tenant?.Code ?? "Unknown");

                return Ok(expensesList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExpenseController.GetAll] Error fetching expenses: {Message}", ex.Message);
                _logger.LogError("❌ [ExpenseController.GetAll] StackTrace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { error = "Failed to fetch expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على نفقة محددة بالمعرف
        /// </summary>
        /// <param name="id">معرف النفقة</param>
        /// <param name="hotelCode">كود الفندق (اختياري - من query parameter أو header)</param>
        /// <returns>معلومات النفقة</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseResponseDto>> GetById(long id, [FromQuery] string? hotelCode = null)
        {
            try
            {
                _logger.LogInformation("🔍 Fetching expense with id: {ExpenseId}", id);

                // ✅ الحصول على hotelCode من query parameter أولاً، ثم من header إذا لم يكن موجوداً
                if (string.IsNullOrWhiteSpace(hotelCode))
                {
                if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                    !string.IsNullOrWhiteSpace(hotelCodeValues))
                {
                    hotelCode = hotelCodeValues.ToString().Trim();
                    _logger.LogInformation("✅ [GetById] X-Hotel-Code header found: {HotelCode}", hotelCode);
                    }
                }
                else
                {
                    _logger.LogInformation("✅ [GetById] HotelCode from query parameter: {HotelCode}", hotelCode);
                }

                ExpenseResponseDto? expense = null;

                // ✅ Check if user is supervisor/manager/accountant/admin
                var userIdClaim = HttpContext.Items["UserId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                    var rolesList = await masterDb.UserRoles
                        .AsNoTracking()
                        .Include(ur => ur.Role)
                        .Where(ur => ur.UserId == userId)
                        .Select(ur => ur.Role!.Code.ToLower())
                        .ToListAsync();

                    var isSupervisorOrManagerOrAdminOrAccountant = rolesList.Contains("supervisor") || 
                                                                   rolesList.Contains("manager") || 
                                                                   rolesList.Contains("admin") || 
                                                                   rolesList.Contains("accountant");

                    if (isSupervisorOrManagerOrAdminOrAccountant)
                    {
                        // ✅ For supervisors/managers/admins/accountants: search across all accessible hotels
                if (!string.IsNullOrWhiteSpace(hotelCode))
                {
                            // ✅ If X-Hotel-Code header is provided, use it to target specific hotel
                            _logger.LogInformation("✅ [GetById] Supervisor/Manager/Admin/Accountant with X-Hotel-Code header: {HotelCode}", hotelCode);
                    expense = await GetExpenseByIdForSupervisorAsync(id, hotelCode);
                }
                else
                {
                            // ✅ Search across all accessible hotels
                            _logger.LogInformation("✅ [GetById] Supervisor/Manager/Admin/Accountant - searching across all accessible hotels");
                            expense = await GetExpenseByIdForSupervisorAcrossAllHotelsAsync(id, userId);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(hotelCode))
                    {
                        // ✅ Regular user with X-Hotel-Code header
                        expense = await GetExpenseByIdForSupervisorAsync(id, hotelCode);
                    }
                    else
                    {
                        // ✅ Regular user - use standard service method
                        expense = await _expenseService.GetByIdAsync(id);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(hotelCode))
                {
                    // ✅ No userId but X-Hotel-Code header provided
                    expense = await GetExpenseByIdForSupervisorAsync(id, hotelCode);
                }
                else
                {
                    // ✅ Regular user - use standard service method
                    expense = await _expenseService.GetByIdAsync(id);
                }

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ Expense not found with id: {ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                _logger.LogInformation("✅ Expense found: ExpenseId={ExpenseId}", id);

                return Ok(expense);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching expense: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch expense", details = ex.Message });
            }
        }

        /// <summary>
        /// تقرير ملخص المصروفات لكل الفنادق في قاعدة بيانات الـ Tenant الحالي (مخصص للمشرف)
        /// </summary>
        /// <param name="fromDate">تاريخ البداية (yyyy-MM-dd)</param>
        /// <param name="toDate">تاريخ النهاية (yyyy-MM-dd)</param>
        /// <param name="expenseCategoryId">معرّف فئة المصروف (اختياري)</param>
        /// <param name="approvalStatus">حالة الموافقة (all/pending/accepted/rejected/...)</param>
        /// <returns>قائمة بملخص المصروفات لكل فندق</returns>
        [HttpGet("SupervisorHotelSummary")]
        [ProducesResponseType(typeof(IEnumerable<ExpenseAnalyticsHotelTableDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsHotelTableDto>>> GetSupervisorHotelSummary(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] int? expenseCategoryId = null,
            [FromQuery] string? approvalStatus = null)
        {
            try
            {
                _logger.LogInformation("📊 [GetSupervisorHotelSummary] Request received: From={From}, To={To}, CategoryId={CategoryId}, Status={Status}",
                    fromDate, toDate, expenseCategoryId, approvalStatus);

                var result = await _expenseService.GetSupervisorHotelSummaryAsync(fromDate, toDate, expenseCategoryId, approvalStatus);
                return Ok(new { data = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorHotelSummary] Business error: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorSummary] Unexpected error");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "حدث خطأ أثناء تحميل تقرير المصروفات", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على تفاصيل المصروفات لفندق محدد في سياق المشرف
        /// Get expense details for a specific hotel in supervisor context
        /// </summary>
        [HttpGet("SupervisorHotelExpenses")]
        [ProducesResponseType(typeof(IEnumerable<ExpenseResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetSupervisorHotelExpenses(
            [FromQuery] string hotelCode,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] int? expenseCategoryId = null,
            [FromQuery] string? approvalStatus = null)
        {
            try
            {
                _logger.LogInformation("📊 [GetSupervisorHotelExpenses] Request received: HotelCode={HotelCode}, From={From}, To={To}, CategoryId={CategoryId}, Status={Status}",
                    hotelCode, fromDate, toDate, expenseCategoryId, approvalStatus);

                if (string.IsNullOrWhiteSpace(hotelCode))
                {
                    return BadRequest(new { error = "HotelCode is required" });
                }

                var result = await _expenseService.GetSupervisorHotelExpensesAsync(hotelCode, fromDate, toDate, expenseCategoryId, approvalStatus);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorHotelExpenses] Business error: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorHotelExpenses] Unexpected error");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "حدث خطأ أثناء تحميل تفاصيل المصروفات", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على تفاصيل مصروف للمشرف (مع تحديد قاعدة البيانات الصحيحة)
        /// Get expense details for supervisor (with correct database identification)
        /// </summary>
        private async Task<ExpenseResponseDto?> GetExpenseByIdForSupervisorAsync(long expenseId, string hotelCode)
        {
            try
            {
                _logger.LogInformation("🔍 [GetExpenseByIdForSupervisor] Fetching expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                    expenseId, hotelCode);

                // ✅ الحصول على معلومات Tenant من Master DB
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var tenant = await masterDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code.ToLower() == hotelCode.ToLower());

                if (tenant == null)
                {
                    _logger.LogError("❌ [GetExpenseByIdForSupervisor] Tenant not found for HotelCode: {HotelCode}", hotelCode);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                {
                    _logger.LogError("❌ [GetExpenseByIdForSupervisor] DatabaseName not set for Tenant: {Code}", tenant.Code);
                    return null;
                }

                // ✅ بناء connection string للـ tenant
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [GetExpenseByIdForSupervisor] TenantDatabase settings not found in configuration");
                    return null;
                }

                var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                // ✅ إنشاء DbContext للـ tenant
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);
                using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                // ✅ الحصول على HotelId من HotelSettings
                var hotelSettings = await tenantContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower());

                if (hotelSettings == null)
                {
                    _logger.LogError("❌ [GetExpenseByIdForSupervisor] HotelSettings not found for HotelCode: {HotelCode}", hotelCode);
                    return null;
                }

                // ✅ البحث عن المصروف في قاعدة البيانات الصحيحة
                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic
                int? searchHotelId = hotelSettings.ZaaerId ?? hotelSettings.HotelId;
                // ✅ FIX: Load expense WITHOUT Include HotelSettings because FK relationship is broken
                var expense = await tenantContext.Expenses
                    .AsNoTracking()
                    .Include(e => e.ExpenseRooms)
                        .ThenInclude(er => er.Apartment)
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == searchHotelId);

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [GetExpenseByIdForSupervisor] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId={ZaaerId}), HotelCode={HotelCode}", 
                        expenseId, searchHotelId, hotelSettings.ZaaerId, hotelCode);
                    return null;
                }

                // ✅ Get category name from Master DB
                string? categoryName = null;
                if (expense.ExpenseCategoryId.HasValue)
                {
                    var masterCategory = await masterDb.ExpenseCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ec => ec.Id == expense.ExpenseCategoryId.Value);
                    categoryName = masterCategory?.MainCategory;
                }

                // ✅ Get approved by user info (full name, role, tenant) from Master DB
                string? approvedByFullName = null;
                string? approvedByRole = null;
                string? approvedByTenantName = null;
                if (expense.ApprovedBy.HasValue)
                {
                    var masterUser = await masterDb.MasterUsers
                        .AsNoTracking()
                        .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                        .Include(u => u.Tenant)
                        .FirstOrDefaultAsync(u => u.Id == expense.ApprovedBy.Value);
                    
                    if (masterUser != null)
                    {
                        approvedByFullName = masterUser.FullName ?? masterUser.Username;
                        var primaryRole = masterUser.UserRoles?.FirstOrDefault()?.Role;
                        approvedByRole = GetRoleDisplayName(primaryRole?.Code);
                        approvedByTenantName = masterUser.Tenant?.Name;
                    }
                }

                // ✅ تحويل إلى DTO
                var expenseRooms = expense.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                {
                    ExpenseRoomId = er.ExpenseRoomId,
                    ExpenseId = er.ExpenseId,
                    ZaaerId = er.ZaaerId,
                    Purpose = er.Purpose,
                    Amount = er.Amount,
                    CreatedAt = er.CreatedAt,
                    ApartmentId = er.Apartment?.ApartmentId,
                    ApartmentCode = er.Apartment?.ApartmentCode,
                    ApartmentName = er.Apartment?.ApartmentName
                }).ToList();

                return new ExpenseResponseDto
                {
                    ExpenseId = expense.ExpenseId,
                    HotelId = expense.HotelId,
                    HotelName = hotelSettings.HotelName, // ✅ Use hotelSettings loaded separately
                    HotelCode = hotelCode,
                    DateTime = expense.DateTime,
                    DueDate = expense.DueDate,
                    Comment = expense.Comment,
                    ExpenseCategoryId = expense.ExpenseCategoryId,
                    ExpenseCategoryName = categoryName, // ✅ From Master DB
                    TaxRate = expense.TaxRate,
                    TaxAmount = expense.TaxAmount,
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
                    ExpenseRooms = expenseRooms
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetExpenseByIdForSupervisor] Error fetching expense: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Get expense by ID for supervisor across all accessible hotels (searches all tenant databases)
        /// </summary>
        private async Task<ExpenseResponseDto?> GetExpenseByIdForSupervisorAcrossAllHotelsAsync(long expenseId, int userId)
        {
            try
            {
                _logger.LogInformation("🔍 [GetExpenseByIdForSupervisorAcrossAllHotels] Searching for expense: ExpenseId={ExpenseId}, UserId={UserId}", 
                    expenseId, userId);

                // ✅ Get all tenants the user has access to
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var userTenants = await masterDb.UserTenants
                    .AsNoTracking()
                    .Include(ut => ut.Tenant)
                    .Where(ut => ut.UserId == userId)
                    .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName, ut.Tenant.Name })
                    .ToListAsync();

                // ✅ Get user roles to check if manager/admin/accountant (should see all tenants)
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isManagerOrAdminOrAccountant = rolesList.Contains("manager") || 
                                                   rolesList.Contains("admin") || 
                                                   rolesList.Contains("accountant");

                if (isManagerOrAdminOrAccountant)
                {
                    _logger.LogInformation("✅ [GetExpenseByIdForSupervisorAcrossAllHotels] Manager/Admin/Accountant - loading all tenants");
                    userTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                        .ToListAsync();
                }

                if (!userTenants.Any())
                {
                    _logger.LogWarning("⚠️ [GetExpenseByIdForSupervisorAcrossAllHotels] No tenants found for user: UserId={UserId}", userId);
                    return null;
                }

                // ✅ Get configuration
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [GetExpenseByIdForSupervisorAcrossAllHotels] TenantDatabase settings not found");
                    return null;
                }

                // ✅ Search across all tenant databases
                foreach (var userTenant in userTenants)
                {
                    try
                    {
                        var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        // ✅ Check if expense exists in this tenant database
                        // ✅ FIX: Load expense WITHOUT Include HotelSettings because FK relationship is broken
                        var expense = await tenantContext.Expenses
                            .AsNoTracking()
                            .Include(e => e.ExpenseRooms)
                                .ThenInclude(er => er.Apartment)
                            .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                        if (expense != null)
                        {
                            // ✅ Found the expense - get its details
                            _logger.LogInformation("✅ [GetExpenseByIdForSupervisorAcrossAllHotels] Found expense in tenant: {Code}", userTenant.Code);

                            // ✅ Load HotelSettings separately (by ZaaerId, not by FK)
                            string? hotelName = null;
                            if (expense.HotelId > 0)
                            {
                                var hotelSettings = await tenantContext.HotelSettings
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(h => h.ZaaerId == expense.HotelId);
                                hotelName = hotelSettings?.HotelName;
                            }

                            // ✅ Get category name from Master DB
                            string? categoryName = null;
                            if (expense.ExpenseCategoryId.HasValue)
                            {
                                var masterCategory = await masterDb.ExpenseCategories
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(ec => ec.Id == expense.ExpenseCategoryId.Value);
                                categoryName = masterCategory?.MainCategory;
                            }

                            // ✅ Convert to DTO
                            var expenseRooms = expense.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                ZaaerId = er.ZaaerId,
                                Purpose = er.Purpose,
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt,
                                ApartmentId = er.Apartment?.ApartmentId,
                                ApartmentCode = er.Apartment?.ApartmentCode,
                                ApartmentName = er.Apartment?.ApartmentName
                            }).ToList();

                            return new ExpenseResponseDto
                            {
                                ExpenseId = expense.ExpenseId,
                                HotelId = expense.HotelId,
                                HotelName = hotelName, // ✅ Use hotelName loaded separately
                                HotelCode = userTenant.Code,
                                DateTime = expense.DateTime,
                                DueDate = expense.DueDate,
                                Comment = expense.Comment,
                                ExpenseCategoryId = expense.ExpenseCategoryId,
                                ExpenseCategoryName = categoryName, // ✅ From Master DB
                                TaxRate = expense.TaxRate,
                                TaxAmount = expense.TaxAmount,
                                TotalAmount = expense.TotalAmount,
                                CreatedAt = expense.CreatedAt,
                                UpdatedAt = expense.UpdatedAt,
                                ApprovalStatus = expense.ApprovalStatus,
                                ApprovedBy = expense.ApprovedBy,
                                ApprovedAt = expense.ApprovedAt,
                                RejectionReason = expense.RejectionReason,
                                ExpenseRooms = expenseRooms
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [GetExpenseByIdForSupervisorAcrossAllHotels] Error searching tenant {Code}: {Message}", 
                            userTenant.Code, ex.Message);
                        // Continue searching other tenants
                    }
                }

                _logger.LogWarning("⚠️ [GetExpenseByIdForSupervisorAcrossAllHotels] Expense not found in any tenant database: ExpenseId={ExpenseId}", expenseId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetExpenseByIdForSupervisorAcrossAllHotels] Error: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// إنشاء نفقة جديدة
        /// </summary>
        /// <param name="dto">بيانات النفقة</param>
        /// <returns>النفقة المُنشأة</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseResponseDto>> Create([FromBody] CreateExpenseDto dto)
        {
            try
            {
                // Log received DTO for debugging
                _logger.LogInformation("📥 Creating expense - TaxRate: {TaxRate}, TaxAmount: {TaxAmount}, TotalAmount: {TotalAmount}", 
                    dto.TaxRate, dto.TaxAmount, dto.TotalAmount);
                
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // ✅ Execute directly without queue - create expense immediately
                _logger.LogInformation(" Creating new expense");

                var expense = await _expenseService.CreateAsync(dto);

                _logger.LogInformation("✅ Expense created successfully: ExpenseId={ExpenseId}, ApprovalStatus={ApprovalStatus}", 
                    expense.ExpenseId, expense.ApprovalStatus);

                // ✅ إضافة رابط الموافقة إذا كان المصروف في حالة pending
                if (expense.ApprovalStatus == "pending")
                {
                    // ✅ استخدام ApprovalBaseUrl من appsettings.json
                    var approvalBaseUrl = _configuration["AppSettings:ApprovalBaseUrl"] ?? "https://aleery.tryasp.net";
                    // إزالة "/" من النهاية إذا كان موجوداً
                    approvalBaseUrl = approvalBaseUrl.TrimEnd('/');
                    var approvalLink = $"{approvalBaseUrl}/approve-expense.html?id={expense.ExpenseId}";
                    
                    _logger.LogInformation("🔗 Approval link generated: {ApprovalLink} (BaseUrl: {BaseUrl})", approvalLink, approvalBaseUrl);
                    
                    // ✅ إرجاع كائن مخصص يحتوي على approvalLink
                    var responseObject = new
                    {
                        expense.ExpenseId,
                        expense.HotelId,
                        expense.DateTime,
                        expense.Comment,
                        expense.ExpenseCategoryId,
                        expenseCategoryName = expense.ExpenseCategoryName,
                        expense.TaxRate,
                        expense.TaxAmount,
                        expense.TotalAmount,
                        expense.CreatedAt,
                        expense.UpdatedAt,
                        expense.ApprovalStatus,
                        expense.ApprovedBy,
                        expense.ApprovedAt,
                        expense.HotelName,
                        approvalLink = approvalLink, // ✅ رابط الموافقة
                        expense.ExpenseRooms
                    };
                    
                    _logger.LogInformation("📤 Returning response with approvalLink: {ApprovalLink}", approvalLink);
                    return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, responseObject);
                }

                _logger.LogInformation("✅ Expense auto-approved (amount <= 50), no approval link needed");
                return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, expense);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating expense: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to create expense", details = ex.Message });
            }
        }

        /// <summary>
        /// تحديث نفقة موجودة
        /// </summary>
        /// <param name="id">معرف النفقة</param>
        /// <param name="dto">بيانات التحديث</param>
        /// <returns>النفقة المُحدّثة</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseResponseDto>> Update(long id, [FromBody] UpdateExpenseDto dto)
        {
            _logger.LogInformation("🔵 [ExpenseController.Update] ========== START ==========");
            _logger.LogInformation("🔵 [ExpenseController.Update] ExpenseId: {ExpenseId}", id);
            _logger.LogInformation("🔵 [ExpenseController.Update] UpdateExpenseDto: {@UpdateExpenseDto}", dto);
            _logger.LogInformation("🔵 [ExpenseController.Update] DTO Properties - ExpenseCategoryId: {ExpenseCategoryId}, TotalAmount: {TotalAmount}, TaxAmount: {TaxAmount}, TaxRate: {TaxRate}, DateTime: {DateTime}, DueDate: {DueDate}, Comment: {Comment}",
                dto.ExpenseCategoryId, dto.TotalAmount, dto.TaxAmount, dto.TaxRate, dto.DateTime, dto.DueDate, dto.Comment);
            
            try
            {
                // Log request headers
                var hotelCodeHeader = Request.Headers["X-Hotel-Code"].FirstOrDefault();
                _logger.LogInformation("🔵 [ExpenseController.Update] X-Hotel-Code header: {HotelCode}", hotelCodeHeader);
                
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("⚠️ [ExpenseController.Update] ModelState is invalid: {@ModelState}", ModelState);
                    return BadRequest(ModelState);
                }

                // ✅ Execute directly without queue - update expense immediately
                // ✅ CRITICAL FIX: Use X-Hotel-Code header to target the specific tenant database directly
                // This ensures we update the expense in the correct hotel, not the one from JWT token
                ExpenseResponseDto? expense = null;
                
                if (!string.IsNullOrWhiteSpace(hotelCodeHeader))
                {
                    // ✅ Use X-Hotel-Code header to target specific tenant database
                    _logger.LogInformation("🔵 [ExpenseController.Update] Using X-Hotel-Code header to target tenant database: {HotelCode}", hotelCodeHeader);
                    expense = await UpdateExpenseForSupervisorAsync(id, dto, hotelCodeHeader);
                }
                else
                {
                    // ✅ Fallback: Use current tenant context
                    _logger.LogInformation("🔵 [ExpenseController.Update] No X-Hotel-Code header, using current tenant context");
                    expense = await _expenseService.UpdateAsync(id, dto);
                }

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [ExpenseController.Update] Expense not found with id: {ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                _logger.LogInformation("✅ [ExpenseController.Update] Expense updated successfully: ExpenseId={ExpenseId}", id);
                _logger.LogInformation("✅ [ExpenseController.Update] Updated expense details - ExpenseCategoryId: {ExpenseCategoryId}, TotalAmount: {TotalAmount}, TaxAmount: {TaxAmount}, TaxRate: {TaxRate}",
                    expense.ExpenseCategoryId, expense.TotalAmount, expense.TaxAmount, expense.TaxRate);
                _logger.LogInformation("✅ [ExpenseController.Update] ========== SUCCESS ==========");

                return Ok(expense);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "⚠️ [ExpenseController.Update] Conflict updating expense: {Message}", ex.Message);
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExpenseController.Update] ========== ERROR ==========");
                _logger.LogError("❌ [ExpenseController.Update] Error updating expense: ExpenseId={ExpenseId}, Message={Message}, StackTrace={StackTrace}",
                    id, ex.Message, ex.StackTrace);
                _logger.LogError("❌ [ExpenseController.Update] ========== END ERROR ==========");
                return StatusCode(500, new { error = "Failed to update expense", details = ex.Message });
            }
        }

        /// <summary>
        /// حذف نفقة
        /// </summary>
        /// <param name="id">معرف النفقة</param>
        /// <returns>نتيجة الحذف</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                // ✅ Execute directly without queue - delete expense immediately
                _logger.LogInformation("🗑️ Deleting expense with id: {ExpenseId}", id);

                var deleted = await _expenseService.DeleteAsync(id);

                if (!deleted)
                {
                    _logger.LogWarning("⚠️ Expense not found with id: {ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                _logger.LogInformation("✅ Expense deleted successfully: ExpenseId={ExpenseId}", id);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "⚠️ [ExpenseController.Delete] Conflict deleting expense: {Message}", ex.Message);
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting expense: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to delete expense", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع expense_rooms لنفقة محددة
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <returns>قائمة expense_rooms</returns>
        [HttpGet("{expenseId}/rooms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseRoomResponseDto>>> GetExpenseRooms(long expenseId)
        {
            try
            {
                _logger.LogInformation("🔍 Fetching expense rooms for expense: {ExpenseId}", expenseId);

                var expenseRooms = await _expenseService.GetExpenseRoomsAsync(expenseId);

                return Ok(expenseRooms);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "⚠️ Expense not found: {Message}", ex.Message);
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching expense rooms: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch expense rooms", details = ex.Message });
            }
        }

        /// <summary>
        /// إضافة غرفة إلى نفقة
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <param name="dto">بيانات expense_room</param>
        /// <returns>expense_room المُنشأ</returns>
        [HttpPost("{expenseId}/rooms")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseRoomResponseDto>> AddExpenseRoom(long expenseId, [FromBody] CreateExpenseRoomDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("➕ Adding room to expense: ExpenseId={ExpenseId}, ApartmentId={ApartmentId}", 
                    expenseId, dto.ApartmentId);

                var expenseRoom = await _expenseService.AddExpenseRoomAsync(expenseId, dto);

                _logger.LogInformation("✅ ExpenseRoom added successfully: ExpenseRoomId={ExpenseRoomId}", 
                    expenseRoom.ExpenseRoomId);

                return CreatedAtAction(nameof(GetExpenseRooms), new { expenseId }, expenseRoom);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "⚠️ Resource not found: {Message}", ex.Message);
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error adding expense room: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to add expense room", details = ex.Message });
            }
        }

        /// <summary>
        /// تحديث expense_room
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <param name="roomId">معرف expense_room</param>
        /// <param name="dto">بيانات التحديث</param>
        /// <returns>expense_room المُحدّث</returns>
        [HttpPut("{expenseId}/rooms/{roomId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseRoomResponseDto>> UpdateExpenseRoom(long expenseId, int roomId, [FromBody] UpdateExpenseRoomDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("✏️ Updating expense room: ExpenseRoomId={ExpenseRoomId}", roomId);

                var expenseRoom = await _expenseService.UpdateExpenseRoomAsync(roomId, dto);

                if (expenseRoom == null)
                {
                    _logger.LogWarning("⚠️ ExpenseRoom not found with id: {ExpenseRoomId}", roomId);
                    return NotFound(new { error = $"ExpenseRoom with id {roomId} not found" });
                }

                _logger.LogInformation("✅ ExpenseRoom updated successfully: ExpenseRoomId={ExpenseRoomId}", roomId);

                return Ok(expenseRoom);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "⚠️ Resource not found: {Message}", ex.Message);
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating expense room: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to update expense room", details = ex.Message });
            }
        }

        /// <summary>
        /// حذف expense_room
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <param name="roomId">معرف expense_room</param>
        /// <returns>نتيجة الحذف</returns>
        [HttpDelete("{expenseId}/rooms/{roomId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExpenseRoom(long expenseId, int roomId)
        {
            try
            {
                _logger.LogInformation("🗑️ Deleting expense room: ExpenseRoomId={ExpenseRoomId}", roomId);

                var deleted = await _expenseService.DeleteExpenseRoomAsync(roomId);

                if (!deleted)
                {
                    _logger.LogWarning("⚠️ ExpenseRoom not found with id: {ExpenseRoomId}", roomId);
                    return NotFound(new { error = $"ExpenseRoom with id {roomId} not found" });
                }

                _logger.LogInformation("✅ ExpenseRoom deleted successfully: ExpenseRoomId={ExpenseRoomId}", roomId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting expense room: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to delete expense room", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع فئات المصروفات من Master DB
        /// Get all expense categories from Master DB (ignoring tenant DB expense_categories table)
        /// </summary>
        /// <returns>قائمة فئات المصروفات</returns>
        [HttpGet("categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetExpenseCategories()
        {
            try
            {
                _logger.LogInformation("📋 [GetExpenseCategories] Fetching expense categories from Master DB");

                // ✅ Get categories from Master DB (not tenant DB)
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                
                var categories = await masterDb.ExpenseCategories
                    .AsNoTracking()
                    .Where(ec => ec.IsActive)
                    .OrderBy(ec => ec.Id)
                    .Select(ec => new
                    {
                        id = ec.Id,
                        expenseCategoryId = ec.Id, // ✅ For backward compatibility
                        categoryName = ec.MainCategory,
                        mainCategory = ec.MainCategory,
                        details = ec.Details,
                        categoryCode = ec.CategoryCode,
                        isActive = ec.IsActive,
                        accountId = ec.AccountId // ✅ VoM Account ID from Chart of Accounts
                    })
                    .ToListAsync<object>();

                _logger.LogInformation("✅ [GetExpenseCategories] Successfully retrieved {Count} expense categories from Master DB", categories.Count);

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching expense categories: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch expense categories", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على نسبة الضريبة للفندق الحالي
        /// Get tax rate for current hotel
        /// </summary>
        /// <returns>نسبة الضريبة</returns>
        [HttpGet("tax-rate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetTaxRate()
        {
            try
            {
                _logger.LogInformation("📊 Fetching tax rate for current hotel");

                var tenant = _tenantService.GetTenant();
                if (tenant == null)
                {
                    return Unauthorized(new { error = "Tenant not resolved. Please provide X-Hotel-Code header." });
                }

                var dbContext = _dbContextResolver.GetCurrentDbContext();

                // Get all hotel settings with the same HotelCode (case-insensitive)
                var allHotelSettings = await dbContext.HotelSettings
                    .AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower())
                    .Select(h => h.HotelId)
                    .ToListAsync();

                if (allHotelSettings == null || allHotelSettings.Count == 0)
                {
                    _logger.LogWarning("⚠️ No HotelSettings found for hotel code: {HotelCode}", tenant.Code);
                    return NotFound(new { error = $"HotelSettings not found for hotel code: {tenant.Code}" });
                }

                _logger.LogInformation("🔍 Found {Count} HotelSettings with HotelCode '{HotelCode}': HotelIds = {HotelIds}", 
                    allHotelSettings.Count, tenant.Code, string.Join(", ", allHotelSettings));

                // ✅ IMPORTANT:
                // In the tenant databases, apartments.hotel_id and taxes.hotel_id store the external ZaaerId value
                // from hotel_settings.zaaer_id, not the internal hotel_id PK.
                // Therefore, for tax lookup we must:
                //  1) read all ZaaerId values for this HotelCode
                //  2) filter taxes by taxes.hotel_id IN (those ZaaerIds)
                var zaaerIds = await dbContext.HotelSettings
                    .AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower() && h.ZaaerId.HasValue)
                    .Select(h => h.ZaaerId!.Value)
                    .ToListAsync();

                _logger.LogInformation("🔍 ZaaerIds for HotelCode '{HotelCode}': {ZaaerIds}", tenant.Code, string.Join(", ", zaaerIds));

                // Get enabled tax for any of these ZaaerIds (prefer VAT type, or first enabled tax)
                var tax = await dbContext.Taxes
                    .AsNoTracking()
                    .Where(t => zaaerIds.Contains(t.HotelId) && t.Enabled)
                    .OrderByDescending(t => t.TaxType == "VAT" || t.TaxType == "vat")
                    .ThenBy(t => t.Id)
                    .FirstOrDefaultAsync();

                if (tax == null)
                {
                    // Log all available taxes for debugging
                    var allTaxes = await dbContext.Taxes
                        .AsNoTracking()
                        .Where(t => zaaerIds.Contains(t.HotelId))
                        .Select(t => new { t.Id, t.HotelId, t.TaxName, t.TaxRate, t.Enabled, t.TaxType })
                        .ToListAsync();
                    
                    _logger.LogWarning("⚠️ No enabled tax found for ZaaerIds (taxes.hotel_id): {ZaaerIds}. Available taxes: {Taxes}", 
                        string.Join(", ", zaaerIds),
                        string.Join("; ", allTaxes.Select(t => $"Id={t.Id}, HotelZaaerId={t.HotelId}, Name={t.TaxName}, Rate={t.TaxRate}, Enabled={t.Enabled}, Type={t.TaxType}")));
                    
                    return Ok(new { taxRate = 0m, hasTax = false });
                }

                _logger.LogInformation("✅ Tax rate found: {TaxRate}% for HotelZaaerId (taxes.hotel_id) = {HotelZaaerId} (TaxId: {TaxId}, Name: {TaxName}, Type: {TaxType})", 
                    tax.TaxRate, tax.HotelId, tax.Id, tax.TaxName, tax.TaxType);

                return Ok(new { 
                    taxRate = tax.TaxRate, 
                    hasTax = true,
                    taxName = tax.TaxName,
                    taxType = tax.TaxType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching tax rate: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch tax rate", details = ex.Message });
            }
        }

        /// <summary>
        /// رفع صور لنفقة موجودة
        /// Upload images for an existing expense
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <param name="images">الصور المرفوعة</param>
        /// <returns>قائمة الصور المُرفوعة</returns>
        [HttpPost("{expenseId}/images")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> UploadImages(long expenseId, [FromForm] List<IFormFile> images)
        {
            try
            {
                _logger.LogInformation("📸 Uploading images for expense: ExpenseId={ExpenseId}, ImageCount={ImageCount}", expenseId, images?.Count ?? 0);

                if (images == null || images.Count == 0)
                {
                    return BadRequest(new { error = "No images provided" });
                }

                var tenant = _tenantService.GetTenant();
                if (tenant == null)
                {
                    return Unauthorized(new { error = "Tenant not resolved. Please provide X-Hotel-Code header." });
                }

                var dbContext = _dbContextResolver.GetCurrentDbContext();

                // Verify expense exists and belongs to current hotel
                var hotelSettings = await dbContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode == tenant.Code);

                if (hotelSettings == null)
                {
                    return NotFound(new { error = $"HotelSettings not found for hotel code: {tenant.Code}" });
                }

                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic
                // Expenses are created with HotelId = hotelSettings.ZaaerId, so we must search using ZaaerId
                if (!hotelSettings.ZaaerId.HasValue)
                {
                    _logger.LogError("❌ [UploadImages] ZaaerId not configured for hotel code: {HotelCode}", tenant.Code);
                    return NotFound(new { error = $"ZaaerId is not configured for hotel code: {tenant.Code}" });
                }

                var expense = await dbContext.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == hotelSettings.ZaaerId.Value);

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [UploadImages] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId), HotelCode={HotelCode}", 
                        expenseId, hotelSettings.ZaaerId.Value, tenant.Code);
                    return NotFound(new { error = $"Expense with id {expenseId} not found" });
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "expenses");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var uploadedImages = new List<object>();
                var displayOrder = await dbContext.ExpenseImages
                    .Where(ei => ei.ExpenseId == expenseId)
                    .OrderByDescending(ei => ei.DisplayOrder)
                    .Select(ei => ei.DisplayOrder)
                    .FirstOrDefaultAsync();

                foreach (var image in images)
                {
                    if (image.Length > 0)
                    {
                        // Generate unique filename
                        var fileName = $"{expenseId}_{KsaTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                        var filePath = Path.Combine(uploadsPath, fileName);
                        var relativePath = $"/uploads/expenses/{fileName}";

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }

                        // Save image record to database
                        var expenseImage = new ExpenseImage
                        {
                            ExpenseId = expenseId,
                            ImagePath = relativePath,
                            OriginalFilename = image.FileName,
                            FileSize = image.Length,
                            ContentType = image.ContentType,
                            DisplayOrder = displayOrder + 1,
                            CreatedAt = KsaTime.Now
                        };

                        dbContext.ExpenseImages.Add(expenseImage);
                        await dbContext.SaveChangesAsync();

                        displayOrder++;

                        uploadedImages.Add(new
                        {
                            expenseImageId = expenseImage.ExpenseImageId,
                            imagePath = expenseImage.ImagePath,
                            originalFilename = expenseImage.OriginalFilename,
                            fileSize = expenseImage.FileSize,
                            contentType = expenseImage.ContentType,
                            displayOrder = expenseImage.DisplayOrder
                        });
                    }
                }

                _logger.LogInformation("✅ Successfully uploaded {Count} images for expense: ExpenseId={ExpenseId}", uploadedImages.Count, expenseId);

                return Ok(uploadedImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error uploading images: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to upload images", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على صور نفقة محددة
        /// Get images for a specific expense
        /// </summary>
        /// <param name="expenseId">معرف النفقة</param>
        /// <returns>قائمة الصور</returns>
        [HttpGet("{expenseId}/images")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetExpenseImages(long expenseId)
        {
            try
            {
                _logger.LogInformation("📸 Fetching images for expense: ExpenseId={ExpenseId}", expenseId);

                // ✅ الحصول على X-Hotel-Code header إذا كان موجوداً (للمشرفين)
                string? hotelCode = null;
                if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                    !string.IsNullOrWhiteSpace(hotelCodeValues))
                {
                    hotelCode = hotelCodeValues.ToString().Trim();
                    _logger.LogInformation("✅ [GetExpenseImages] X-Hotel-Code header found: {HotelCode}", hotelCode);
                }

                // ✅ إذا كان هناك X-Hotel-Code header، نستخدمه لتحديد قاعدة البيانات الصحيحة
                if (!string.IsNullOrWhiteSpace(hotelCode))
                {
                    // ✅ للمشرفين: البحث في قاعدة البيانات الصحيحة بناءً على HotelCode
                    var supervisorImages = await GetExpenseImagesForSupervisorAsync(expenseId, hotelCode);
                    if (supervisorImages != null)
                    {
                        return Ok(supervisorImages);
                    }
                    // If not found, return NotFound
                    return NotFound(new { error = $"Expense with id {expenseId} not found in tenant: {hotelCode}" });
                }

                // ✅ للمستخدمين العاديين: استخدام الطريقة العادية
                var tenant = _tenantService.GetTenant();
                if (tenant == null)
                {
                    return Unauthorized(new { error = "Tenant not resolved. Please provide X-Hotel-Code header." });
                }

                var dbContext = _dbContextResolver.GetCurrentDbContext();

                // Verify expense exists and belongs to current hotel
                var hotelSettings = await dbContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode == tenant.Code);

                if (hotelSettings == null)
                {
                    return NotFound(new { error = $"HotelSettings not found for hotel code: {tenant.Code}" });
                }

                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic
                int? searchHotelId = hotelSettings.ZaaerId ?? hotelSettings.HotelId;
                var expense = await dbContext.Expenses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == searchHotelId);

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [GetExpenseImages] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId={ZaaerId}), HotelCode={HotelCode}", 
                        expenseId, searchHotelId, hotelSettings.ZaaerId, tenant.Code);
                    return NotFound(new { error = $"Expense with id {expenseId} not found" });
                }

                // Get all images for this expense
                var images = await dbContext.ExpenseImages
                    .AsNoTracking()
                    .Where(ei => ei.ExpenseId == expenseId)
                    .OrderBy(ei => ei.DisplayOrder)
                    .ThenBy(ei => ei.CreatedAt)
                    .Select(ei => new
                    {
                        expenseImageId = ei.ExpenseImageId,
                        imageUrl = ei.ImagePath.StartsWith("http") ? ei.ImagePath : $"{Request.Scheme}://{Request.Host}{ei.ImagePath}",
                        imagePath = ei.ImagePath,
                        originalFilename = ei.OriginalFilename,
                        fileSize = ei.FileSize,
                        contentType = ei.ContentType,
                        displayOrder = ei.DisplayOrder,
                        createdAt = ei.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("✅ Successfully retrieved {Count} images for expense: ExpenseId={ExpenseId}", images.Count, expenseId);

                return Ok(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching expense images: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch expense images", details = ex.Message });
            }
        }

        /// <summary>
        /// الموافقة أو الرفض على مصروف
        /// Approve or reject an expense
        /// </summary>
        /// <param name="id">معرف المصروف</param>
        /// <param name="status">حالة الموافقة (accepted, rejected, awaiting-manager, awaiting-accountant, أو awaiting-admin)</param>
        /// <param name="rejectionReason">سبب الرفض (اختياري، يُستخدم فقط في حالة rejected)</param>
        /// <param name="recommendation">التوصية (اختياري)</param>
        /// <param name="recommendationToUserId">معرف المستخدم المستهدف للتوصية (NULL = للجميع)</param>
        /// <returns>نتيجة العملية</returns>
        [HttpPut("approve/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveExpense(long id, [FromQuery] string status, [FromQuery] string? rejectionReason = null, [FromQuery] string? recommendation = null, [FromQuery] int? recommendationToUserId = null)
        {
            try
            {
                _logger.LogInformation("🔐 Approving/Rejecting expense: ExpenseId={ExpenseId}, Status={Status}", id, status);

                // التحقق من صحة الحالة
                if (status != "accepted" && status != "rejected" && status != "pending" && status != "awaiting-manager" && status != "awaiting-accountant" && status != "awaiting-admin" && status != "awaiting-officer" && status != "awaiting-verifier")
                {
                    return BadRequest(new { error = "Invalid status. Must be 'accepted', 'rejected', 'pending', 'awaiting-manager', 'awaiting-accountant', 'awaiting-admin', 'awaiting-officer', or 'awaiting-verifier'" });
                }

                // ✅ الحصول على X-Hotel-Code header إذا كان موجوداً (للمشرفين)
                string? hotelCode = null;
                if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                    !string.IsNullOrWhiteSpace(hotelCodeValues))
                {
                    hotelCode = hotelCodeValues.ToString().Trim();
                    _logger.LogInformation("✅ X-Hotel-Code header found: {HotelCode}", hotelCode);
                }

                // الحصول على UserId من JWT Token
                int? userId = null;
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                {
                    if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                    {
                        userId = parsedUserId;
                        _logger.LogInformation("✅ UserId from JWT Token: {UserId}", userId);
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("⚠️ UserId not found in JWT Token - using default value 0");
                    userId = 0; // Default value if not found
                }

                // ✅ Check if user is supervisor/manager/accountant/admin
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId.Value)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isSupervisorOrManagerOrAdminOrAccountant = rolesList.Contains("supervisor") || 
                                                               rolesList.Contains("manager") || 
                                                               rolesList.Contains("admin") || 
                                                               rolesList.Contains("accountant");

                ExpenseResponseDto? expense = null;
                
                if (isSupervisorOrManagerOrAdminOrAccountant)
                {
                    // ✅ For supervisors/managers/admins/accountants: search across all accessible hotels
                if (!string.IsNullOrWhiteSpace(hotelCode))
                {
                        // ✅ If X-Hotel-Code header is provided, use it to target specific hotel
                        _logger.LogInformation("✅ [ApproveExpense] Supervisor/Manager/Admin/Accountant with X-Hotel-Code header: {HotelCode}", hotelCode);
                    expense = await ApproveExpenseForSupervisorAsync(id, status, userId.Value, rejectionReason, hotelCode, recommendation, recommendationToUserId);
                }
                else
                {
                        // ✅ Search across all accessible hotels
                        _logger.LogInformation("✅ [ApproveExpense] Supervisor/Manager/Admin/Accountant - searching across all accessible hotels");
                        expense = await ApproveExpenseForSupervisorAcrossAllHotelsAsync(id, status, userId.Value, rejectionReason, recommendation, recommendationToUserId);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(hotelCode))
                {
                    // ✅ Regular user with X-Hotel-Code header (for supervisors accessing specific hotel)
                    expense = await ApproveExpenseForSupervisorAsync(id, status, userId.Value, rejectionReason, hotelCode, recommendation, recommendationToUserId);
                }
                else
                {
                    // ✅ Regular user - use standard service method
                    expense = await _expenseService.ApproveExpenseAsync(id, status, userId.Value, rejectionReason, recommendation, recommendationToUserId);
                }

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ Expense not found with id: {ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                _logger.LogInformation("✅ Expense approval updated successfully: ExpenseId={ExpenseId}, Status={Status}, ApprovedBy={ApprovedBy}", 
                    id, status, userId);

                return Ok(new { 
                    message = "Expense status updated successfully", 
                    expenseId = expense.ExpenseId,
                    status = expense.ApprovalStatus,
                    approvedBy = expense.ApprovedBy,
                    approvedAt = expense.ApprovedAt,
                    rejectionReason = expense.RejectionReason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error approving/rejecting expense: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to update expense status", details = ex.Message });
            }
        }

        /// <summary>
        /// حذف المصروف (Soft Delete) - تحديث approval_status إلى "cancelled"
        /// Cancel/Delete expense (Soft Delete) - Updates approval_status to "cancelled"
        /// ✅ Only allowed for accepted expenses
        /// ✅ Adds record to expense_approval_history
        /// </summary>
        /// <param name="id">معرف المصروف</param>
        /// <param name="hotelCode">كود الفندق (اختياري - من query parameter أو header)</param>
        /// <returns>نتيجة العملية</returns>
        [HttpPut("cancel/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelExpense(long id, [FromQuery] string? hotelCode = null)
        {
            try
            {
                _logger.LogInformation("🗑️ Cancelling expense: ExpenseId={ExpenseId}", id);

                // ✅ الحصول على X-Hotel-Code header إذا كان موجوداً
                if (string.IsNullOrWhiteSpace(hotelCode))
                {
                    if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                        !string.IsNullOrWhiteSpace(hotelCodeValues))
                    {
                        hotelCode = hotelCodeValues.ToString().Trim();
                        _logger.LogInformation("✅ X-Hotel-Code header found: {HotelCode}", hotelCode);
                    }
                }

                // الحصول على UserId من JWT Token
                int? userId = null;
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                {
                    if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                    {
                        userId = parsedUserId;
                        _logger.LogInformation("✅ UserId from JWT Token: {UserId}", userId);
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("⚠️ UserId not found in JWT Token");
                    return Unauthorized(new { error = "User ID not found in token" });
                }

                // ✅ Get tenant information
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                
                // ✅ Check if user is admin (only admin can cancel expenses)
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId.Value)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isAdmin = rolesList.Contains("admin");
                
                if (!isAdmin)
                {
                    _logger.LogWarning("⚠️ User {UserId} is not admin - cannot cancel expense", userId.Value);
                    return Forbid("Only admin users can cancel expenses");
                }

                // ✅ Get user full name
                var masterUser = await masterDb.MasterUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);
                var actionByFullName = masterUser?.FullName ?? masterUser?.Username ?? "Unknown";

                // ✅ Get all accessible tenants for admin
                var userTenants = await masterDb.UserTenants
                    .AsNoTracking()
                    .Where(ut => ut.UserId == userId.Value)
                    .Include(ut => ut.Tenant)
                    .ToListAsync();

                if (!userTenants.Any())
                {
                    _logger.LogWarning("⚠️ User {UserId} has no accessible tenants", userId.Value);
                    return NotFound(new { error = "No accessible tenants found" });
                }

                // ✅ Search for expense across all accessible tenant databases
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ TenantDatabase settings not found in configuration");
                    return StatusCode(500, new { error = "Database configuration not found" });
                }

                Expense? foundExpense = null;
                string? foundDatabaseName = null;
                FinanceLedgerAPI.Models.Tenant? foundTenant = null;

                foreach (var userTenant in userTenants)
                {
                    if (userTenant.Tenant == null || string.IsNullOrWhiteSpace(userTenant.Tenant.DatabaseName))
                        continue;

                    // ✅ If hotelCode is provided, only search in that tenant
                    if (!string.IsNullOrWhiteSpace(hotelCode) && 
                        !userTenant.Tenant.Code.Equals(hotelCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var connectionString = $"Server={server}; Database={userTenant.Tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        var expense = await tenantContext.Expenses
                            .FirstOrDefaultAsync(e => e.ExpenseId == id);

                        if (expense != null)
                        {
                            // ✅ Check if expense is accepted (only accepted expenses can be cancelled)
                            var currentStatus = (expense.ApprovalStatus ?? "").ToLower();
                            if (currentStatus != "accepted" && currentStatus != "auto-approved")
                            {
                                _logger.LogWarning("⚠️ Expense {ExpenseId} is not accepted (Status: {Status}) - cannot cancel", id, currentStatus);
                                return BadRequest(new { error = $"Cannot cancel expense. Only accepted expenses can be cancelled. Current status: {currentStatus}" });
                            }

                            foundExpense = expense;
                            foundDatabaseName = userTenant.Tenant.DatabaseName;
                            foundTenant = userTenant.Tenant;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error searching tenant {Code}: {Message}", 
                            userTenant.Tenant.Code, ex.Message);
                        continue;
                    }
                }

                if (foundExpense == null || string.IsNullOrWhiteSpace(foundDatabaseName))
                {
                    _logger.LogWarning("⚠️ Expense not found: ExpenseId={ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                // ✅ Create new context for the found tenant database to perform updates
                var foundConnectionString = $"Server={server}; Database={foundDatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
                var foundOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                foundOptionsBuilder.UseSqlServer(foundConnectionString);
                using var foundContext = new ApplicationDbContext(foundOptionsBuilder.Options);

                // ✅ Reload expense in the new context
                var expenseToUpdate = await foundContext.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == id);

                if (expenseToUpdate == null)
                {
                    _logger.LogWarning("⚠️ Expense not found in context: ExpenseId={ExpenseId}", id);
                    return NotFound(new { error = $"Expense with id {id} not found" });
                }

                // ✅ Check if expense was already sent to VoM
                bool wasSentToVoM = expenseToUpdate.StatusVoM == "sent";
                string? reversalStatus = null;

                // ✅ Update expense status to "cancelled"
                expenseToUpdate.ApprovalStatus = "cancelled";
                expenseToUpdate.UpdatedAt = KsaTime.Now;
                expenseToUpdate.UpdatedBy = userId.Value;

                // ✅ Add record to expense_approval_history
                var history = new FinanceLedgerAPI.Models.ExpenseApprovalHistory
                {
                    ExpenseId = expenseToUpdate.ExpenseId,
                    Action = "cancelled",
                    ActionBy = userId.Value,
                    ActionByFullName = actionByFullName,
                    ActionAt = KsaTime.Now,
                    Status = "cancelled",
                    RejectionReason = null,
                    Comments = $"تم إلغاء المصروف من قبل {actionByFullName}",
                    Recommendation = null,
                    RecommendationToUserId = null,
                    RecommendationReadBy = null
                };

                await foundContext.ExpenseApprovalHistories.AddAsync(history);
                await foundContext.SaveChangesAsync();

                // VoM integration removed: keep cancellation workflow only.
                if (wasSentToVoM)
                {
                    _logger.LogInformation(
                        "ℹ️ Expense had previous VoM status but reversal is skipped because VoM integration was removed. ExpenseId={ExpenseId}",
                        id);
                }

                _logger.LogInformation("✅ Expense cancelled successfully: ExpenseId={ExpenseId}, CancelledBy={CancelledBy}, HotelCode={HotelCode}, WasSentToVoM={WasSentToVoM}, ReversalStatus={ReversalStatus}",
                    id, userId.Value, foundTenant?.Code ?? "Unknown", wasSentToVoM, reversalStatus ?? "N/A");

                return Ok(new { 
                    message = "Expense cancelled successfully", 
                    expenseId = expenseToUpdate.ExpenseId,
                    status = expenseToUpdate.ApprovalStatus,
                    cancelledBy = userId.Value,
                    cancelledAt = history.ActionAt,
                    wasSentToVoM = wasSentToVoM,
                    reversalStatus = reversalStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cancelling expense: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to cancel expense", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على سجل موافقات المصروف
        /// Get expense approval history
        /// ✅ Supports supervisors/managers/accountants/admins accessing history from any hotel
        /// </summary>
        /// <param name="id">معرف المصروف</param>
        /// <param name="hotelCode">كود الفندق (اختياري - من query parameter أو header)</param>
        /// <returns>قائمة سجلات الموافقات</returns>
        [HttpGet("{id}/history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetApprovalHistory(long id, [FromQuery] string? hotelCode = null)
        {
            try
            {
                _logger.LogInformation("📋 Fetching approval history for expense: ExpenseId={ExpenseId}", id);

                // ✅ الحصول على hotelCode من query parameter أولاً، ثم من header إذا لم يكن موجوداً
                if (string.IsNullOrWhiteSpace(hotelCode))
                {
                    if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                        !string.IsNullOrWhiteSpace(hotelCodeValues))
                    {
                        hotelCode = hotelCodeValues.ToString().Trim();
                        _logger.LogInformation("✅ [GetApprovalHistory] X-Hotel-Code header found: {HotelCode}", hotelCode);
                    }
                }
                else
                {
                    _logger.LogInformation("✅ [GetApprovalHistory] HotelCode from query parameter: {HotelCode}", hotelCode);
                }

                // ✅ Check if user is supervisor/manager/accountant/admin
                var userIdClaim = HttpContext.Items["UserId"]?.ToString();
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    // Regular user - use standard service method
                var history = await _expenseService.GetApprovalHistoryAsync(id);
                _logger.LogInformation("✅ Approval history fetched successfully: ExpenseId={ExpenseId}, Count={Count}", 
                    id, history.Count());
                    return Ok(history);
                }

                // ✅ Get user roles
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isSupervisorOrManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier = rolesList.Contains("supervisor") || 
                                                               rolesList.Contains("manager") || 
                                                               rolesList.Contains("admin") || 
                                                                                rolesList.Contains("accountant") ||
                                                                                rolesList.Contains("officer") ||
                                                                                rolesList.Contains("owner") ||
                                                                                rolesList.Contains("verifier");

                if (isSupervisorOrManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier)
                {
                    // ✅ For supervisors/managers/admins/accountants/officers/owners/verifiers: search across all tenant databases
                    _logger.LogInformation("✅ [GetApprovalHistory] Supervisor/Manager/Admin/Accountant/Officer/Owner/Verifier detected - searching across all hotels");
                    
                    var history = await GetApprovalHistoryForSupervisorAsync(id, userId, hotelCode);
                    if (history != null)
                    {
                        _logger.LogInformation("✅ Approval history fetched successfully: ExpenseId={ExpenseId}, Count={Count}", 
                            id, history.Count());
                return Ok(history);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Approval history not found for expense: ExpenseId={ExpenseId}", id);
                        return NotFound(new { error = $"Approval history not found for expense {id}" });
                    }
                }
                else
                {
                    // Regular user - use standard service method
                    var history = await _expenseService.GetApprovalHistoryAsync(id);
                    _logger.LogInformation("✅ Approval history fetched successfully: ExpenseId={ExpenseId}, Count={Count}", 
                        id, history.Count());
                    return Ok(history);
                }
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("⚠️ Expense not found: ExpenseId={ExpenseId}", id);
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching approval history: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch approval history", details = ex.Message });
            }
        }

        /// <summary>
        /// Get approval history for supervisor (searching across all tenant databases)
        /// </summary>
        private async Task<List<ExpenseApprovalHistoryDto>?> GetApprovalHistoryForSupervisorAsync(long expenseId, int userId, string? preferredHotelCode = null)
        {
            try
            {
                _logger.LogInformation("🔍 [GetApprovalHistoryForSupervisor] Searching for expense history: ExpenseId={ExpenseId}, UserId={UserId}", 
                    expenseId, userId);

                // ✅ Get all tenants the user has access to
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var userTenants = await masterDb.UserTenants
                    .AsNoTracking()
                    .Include(ut => ut.Tenant)
                    .Where(ut => ut.UserId == userId)
                    .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName, ut.Tenant.Name })
                    .ToListAsync();

                // ✅ Get user roles to check if manager/admin/accountant (should see all tenants)
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier = rolesList.Contains("manager") || 
                                                   rolesList.Contains("admin") || 
                                                                    rolesList.Contains("accountant") ||
                                                                    rolesList.Contains("officer") ||
                                                                    rolesList.Contains("owner") ||
                                                                    rolesList.Contains("verifier");

                if (isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier)
                {
                    _logger.LogInformation("✅ [GetApprovalHistoryForSupervisor] Manager/Admin/Accountant/Officer/Owner/Verifier - loading all tenants");
                    userTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                        .ToListAsync();
                }

                if (!userTenants.Any())
                {
                    _logger.LogWarning("⚠️ [GetApprovalHistoryForSupervisor] No tenants found for user: UserId={UserId}", userId);
                    return null;
                }

                // ✅ If preferredHotelCode is provided, prioritize searching in that hotel first
                if (!string.IsNullOrWhiteSpace(preferredHotelCode))
                {
                    var preferredTenant = userTenants.FirstOrDefault(t => 
                        t.Code.Equals(preferredHotelCode, StringComparison.OrdinalIgnoreCase));
                    
                    if (preferredTenant != null)
                    {
                        // ✅ Move preferred tenant to the front of the list
                        userTenants = userTenants
                            .OrderByDescending(t => t.Code.Equals(preferredHotelCode, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        _logger.LogInformation("✅ [GetApprovalHistoryForSupervisor] Prioritizing search in hotel: {HotelCode}", preferredHotelCode);
                    }
                }

                // ✅ Get configuration
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [GetApprovalHistoryForSupervisor] TenantDatabase settings not found");
                    return null;
                }

                // ✅ Search across all tenant databases
                foreach (var userTenant in userTenants)
                {
                    try
                    {
                        var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        // ✅ Check if expense exists in this tenant database
                        var expense = await tenantContext.Expenses
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                        if (expense != null)
                        {
                            // ✅ Found the expense - get its history
                            _logger.LogInformation("✅ [GetApprovalHistoryForSupervisor] Found expense in tenant: {Code}", userTenant.Code);
                            
                            var history = await tenantContext.ExpenseApprovalHistories
                                .AsNoTracking()
                                .Where(h => h.ExpenseId == expenseId)
                                .OrderBy(h => h.ActionAt)
                                .ToListAsync();

                            // Get current user ID for checking recommendation read status
                            int? currentUserId = null;
                            if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                            {
                                if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                                {
                                    currentUserId = parsedUserId;
                                }
                            }

                            // Get unique user IDs to fetch role and tenant info (for ActionBy, RecommendationToUserId, and RecommendationReadBy)
                            var userIds = history
                                .Where(h => h.ActionBy.HasValue || h.RecommendationToUserId.HasValue || !string.IsNullOrWhiteSpace(h.RecommendationReadBy))
                                .SelectMany(h => {
                                    var ids = new List<int?>();
                                    if (h.ActionBy.HasValue) ids.Add(h.ActionBy);
                                    if (h.RecommendationToUserId.HasValue) ids.Add(h.RecommendationToUserId);
                                    
                                    // Parse RecommendationReadBy to get user IDs
                                    if (!string.IsNullOrWhiteSpace(h.RecommendationReadBy))
                                    {
                                        try
                                        {
                                            var readByList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(h.RecommendationReadBy);
                                            if (readByList != null)
                                            {
                                                ids.AddRange(readByList.Select(id => (int?)id));
                                            }
                                        }
                                        catch
                                        {
                                            // Ignore parse errors
                                        }
                                    }
                                    
                                    return ids;
                                })
                                .Where(id => id.HasValue)
                                .Select(id => id!.Value)
                                .Distinct()
                                .ToList();
                            
                            var userInfoDict = new Dictionary<int, (string? fullName, string? role, string? tenantName)>();
                            
                            if (userIds.Any())
                            {
                                var users = await masterDb.MasterUsers
                                    .AsNoTracking()
                                    .Include(u => u.UserRoles)
                                        .ThenInclude(ur => ur.Role)
                                    .Include(u => u.Tenant)
                                    .Where(u => userIds.Contains(u.Id))
                                    .ToListAsync();

                                foreach (var user in users)
                                {
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
                                        
                                        // ✅ Get full names of users who read the recommendation
                                        var readByFullNames = new List<string>();
                                        foreach (var readByUserId in dto.RecommendationReadBy)
                                        {
                                            if (userInfoDict.TryGetValue(readByUserId, out var readByUserInfo))
                                            {
                                                readByFullNames.Add(readByUserInfo.fullName ?? "غير محدد");
                                            }
                                        }
                                        dto.RecommendationReadByFullNames = readByFullNames;
                                    }
                                    catch
                                    {
                                        dto.RecommendationReadBy = new List<int>();
                                        dto.RecommendationReadByFullNames = new List<string>();
                                        dto.IsRecommendationReadByCurrentUser = false;
                                    }
                                }
                                else
                                {
                                    dto.RecommendationReadBy = new List<int>();
                                    dto.RecommendationReadByFullNames = new List<string>();
                                    dto.IsRecommendationReadByCurrentUser = false;
                                }

                                return dto;
                            }).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [GetApprovalHistoryForSupervisor] Error searching tenant {Code}: {Message}", 
                            userTenant.Code, ex.Message);
                        // Continue searching other tenants
                    }
                }

                _logger.LogWarning("⚠️ [GetApprovalHistoryForSupervisor] Expense not found in any tenant database: ExpenseId={ExpenseId}", expenseId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetApprovalHistoryForSupervisor] Error: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// تحديد التوصية كمقروءة من المستخدم الحالي
        /// Mark recommendation as read by current user
        /// </summary>
        /// <param name="expenseId">معرف المصروف</param>
        /// <param name="historyId">معرف سجل الموافقة (ExpenseApprovalHistory Id)</param>
        /// <returns>نتيجة العملية</returns>
        [HttpPost("{expenseId}/history/{historyId}/mark-recommendation-read")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MarkRecommendationAsRead(long expenseId, int historyId)
        {
            try
            {
                _logger.LogInformation("📖 Marking recommendation as read: ExpenseId={ExpenseId}, HistoryId={HistoryId}", expenseId, historyId);

                // Get current user ID
                int? userId = null;
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                {
                    if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID not found in token" });
                }

                // Check if user is supervisor/manager/accountant/admin
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId.Value)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isSupervisorOrManagerOrAdminOrAccountantOrOfficerOrOwner = rolesList.Contains("supervisor") || 
                                                               rolesList.Contains("manager") || 
                                                               rolesList.Contains("admin") || 
                                                                                rolesList.Contains("accountant") ||
                                                                                rolesList.Contains("officer") ||
                                                                                rolesList.Contains("owner");

                // Get X-Hotel-Code header if present
                string? hotelCode = null;
                if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                    !string.IsNullOrWhiteSpace(hotelCodeValues))
                {
                    hotelCode = hotelCodeValues.ToString().Trim();
                }

                // Get configuration
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [MarkRecommendationAsRead] TenantDatabase settings not found");
                    return StatusCode(500, new { error = "Database configuration not found" });
                }

                // If supervisor/manager/admin/accountant/officer/owner, search across all accessible hotels
                if (isSupervisorOrManagerOrAdminOrAccountantOrOfficerOrOwner)
                {
                    var userTenants = await masterDb.UserTenants
                        .AsNoTracking()
                        .Include(ut => ut.Tenant)
                        .Where(ut => ut.UserId == userId.Value)
                        .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName })
                        .ToListAsync();

                    var isManagerOrAdminOrAccountant = rolesList.Contains("manager") || 
                                                       rolesList.Contains("admin") || 
                                                       rolesList.Contains("accountant");

                    if (isManagerOrAdminOrAccountant)
                    {
                        userTenants = await masterDb.Tenants
                            .AsNoTracking()
                            .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName })
                            .ToListAsync();
                    }

                    // Search across all tenant databases
                    foreach (var userTenant in userTenants)
                    {
                        try
                        {
                            var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
                            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                            optionsBuilder.UseSqlServer(connectionString);
                            using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                            var history = await tenantContext.ExpenseApprovalHistories
                                .FirstOrDefaultAsync(h => h.Id == historyId && h.ExpenseId == expenseId);

                            if (history != null && !string.IsNullOrWhiteSpace(history.Recommendation))
                            {
                                // Parse existing read by list
                                List<int> readByList = new List<int>();
                                if (!string.IsNullOrWhiteSpace(history.RecommendationReadBy))
                                {
                                    try
                                    {
                                        readByList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(history.RecommendationReadBy) ?? new List<int>();
                                    }
                                    catch
                                    {
                                        readByList = new List<int>();
                                    }
                                }

                                // Add current user if not already in list
                                if (!readByList.Contains(userId.Value))
                                {
                                    readByList.Add(userId.Value);
                                    history.RecommendationReadBy = System.Text.Json.JsonSerializer.Serialize(readByList);
                                    await tenantContext.SaveChangesAsync();
                                    _logger.LogInformation("✅ [MarkRecommendationAsRead] Recommendation marked as read: ExpenseId={ExpenseId}, HistoryId={HistoryId}, UserId={UserId}", 
                                        expenseId, historyId, userId);
                                }
                                
                                // ✅ Get full names of users who read the recommendation
                                var readByUsers = await masterDb.MasterUsers
                                    .AsNoTracking()
                                    .Where(u => readByList.Contains(u.Id))
                                    .Select(u => new { u.Id, u.FullName, u.Username })
                                    .ToListAsync();
                                
                                var readByFullNames = readByUsers
                                    .Select(u => u.FullName ?? u.Username)
                                    .ToList();
                                
                                return Ok(new { 
                                    message = readByList.Contains(userId.Value) && readByList.Count == 1 
                                        ? "Recommendation marked as read" 
                                        : "Recommendation already marked as read", 
                                    expenseId, 
                                    historyId,
                                    readByUserIds = readByList,
                                    readByFullNames = readByFullNames
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ [MarkRecommendationAsRead] Error searching tenant {Code}: {Message}", 
                                userTenant.Code, ex.Message);
                            // Continue searching other tenants
                        }
                    }
                }
                else
                {
                    // Regular user - use current tenant
                    var tenant = _tenantService.GetTenant();
                    if (tenant == null || string.IsNullOrWhiteSpace(tenant.DatabaseName))
                    {
                        return BadRequest(new { error = "Tenant not resolved" });
                    }

                    var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlServer(connectionString);
                    using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                    var history = await tenantContext.ExpenseApprovalHistories
                        .FirstOrDefaultAsync(h => h.Id == historyId && h.ExpenseId == expenseId);

                    if (history == null)
                    {
                        return NotFound(new { error = $"Approval history with id {historyId} not found for expense {expenseId}" });
                    }

                    if (string.IsNullOrWhiteSpace(history.Recommendation))
                    {
                        return BadRequest(new { error = "This approval history does not have a recommendation" });
                    }

                    // Parse existing read by list
                    List<int> readByList = new List<int>();
                    if (!string.IsNullOrWhiteSpace(history.RecommendationReadBy))
                    {
                        try
                        {
                            readByList = System.Text.Json.JsonSerializer.Deserialize<List<int>>(history.RecommendationReadBy) ?? new List<int>();
                        }
                        catch
                        {
                            readByList = new List<int>();
                        }
                    }

                        // Add current user if not already in list
                        if (!readByList.Contains(userId.Value))
                        {
                            readByList.Add(userId.Value);
                            history.RecommendationReadBy = System.Text.Json.JsonSerializer.Serialize(readByList);
                            await tenantContext.SaveChangesAsync();
                            _logger.LogInformation("✅ [MarkRecommendationAsRead] Recommendation marked as read: ExpenseId={ExpenseId}, HistoryId={HistoryId}, UserId={UserId}", 
                                expenseId, historyId, userId);
                        }
                        
                        // ✅ Get full names of users who read the recommendation
                        var readByUsers = await masterDb.MasterUsers
                            .AsNoTracking()
                            .Where(u => readByList.Contains(u.Id))
                            .Select(u => new { u.Id, u.FullName, u.Username })
                            .ToListAsync();
                        
                        var readByFullNames = readByUsers
                            .Select(u => u.FullName ?? u.Username)
                            .ToList();
                        
                        return Ok(new { 
                            message = readByList.Contains(userId.Value) && readByList.Count == 1 
                                ? "Recommendation marked as read" 
                                : "Recommendation already marked as read", 
                            expenseId, 
                            historyId,
                            readByUserIds = readByList,
                            readByFullNames = readByFullNames
                        });
                }

                return NotFound(new { error = $"Approval history with id {historyId} not found for expense {expenseId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetApprovalHistoryForSupervisor] Error: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// الموافقة/الرفض على مصروف للمشرف (مع تحديد قاعدة البيانات الصحيحة)
        /// Approve/Reject expense for supervisor (with correct database identification)
        /// </summary>
        private async Task<ExpenseResponseDto?> ApproveExpenseForSupervisorAsync(long expenseId, string status, int approvedBy, string? rejectionReason, string hotelCode, string? recommendation = null, int? recommendationToUserId = null)
        {
            try
            {
                _logger.LogInformation("🔐 [ApproveExpenseForSupervisor] Approving expense: ExpenseId={ExpenseId}, Status={Status}, HotelCode={HotelCode}", 
                    expenseId, status, hotelCode);

                // ✅ الحصول على معلومات Tenant من Master DB
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var tenant = await masterDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code.ToLower() == hotelCode.ToLower());

                if (tenant == null)
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] Tenant not found for HotelCode: {HotelCode}", hotelCode);
                    throw new InvalidOperationException($"Tenant not found for hotel code: {hotelCode}");
                }

                if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] DatabaseName not set for Tenant: {Code}", tenant.Code);
                    throw new InvalidOperationException($"DatabaseName not configured for tenant: {tenant.Code}");
                }

                // ✅ بناء connection string للـ tenant
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] TenantDatabase settings not found in configuration");
                    throw new InvalidOperationException("TenantDatabase settings not found in configuration");
                }

                var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                // ✅ إنشاء DbContext للـ tenant
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);
                using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                // ✅ الحصول على HotelId من HotelSettings
                var hotelSettings = await tenantContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower());

                if (hotelSettings == null)
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] HotelSettings not found for HotelCode: {HotelCode}", hotelCode);
                    throw new InvalidOperationException($"HotelSettings not found for hotel code: {hotelCode}");
                }

                // ✅ البحث عن المصروف في قاعدة البيانات الصحيحة
                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic
                int? searchHotelId = hotelSettings.ZaaerId ?? hotelSettings.HotelId;
                var expense = await tenantContext.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == searchHotelId);

                // ✅ إذا لم يتم العثور عليه، نبحث بدون HotelId filter (في حالة وجود مشكلة في التطابق)
                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [ApproveExpenseForSupervisor] Expense not found with HotelId filter. Trying without filter: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId={ZaaerId}), HotelCode={HotelCode}", 
                        expenseId, searchHotelId, hotelSettings.ZaaerId, hotelCode);
                    
                    expense = await tenantContext.Expenses
                        .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);
                    
                    if (expense != null)
                    {
                        _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Expense found without HotelId filter: ExpenseId={ExpenseId}, ActualHotelId={ActualHotelId}, ExpectedHotelId={ExpectedHotelId}", 
                            expenseId, expense.HotelId, hotelSettings.HotelId);
                    }
                }

                if (expense == null)
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId}, HotelCode={HotelCode}", 
                        expenseId, hotelSettings.HotelId, hotelCode);
                    throw new InvalidOperationException($"Expense with id {expenseId} not found in tenant database for hotel code {hotelCode}");
                }

                // ✅ تحديث حالة الموافقة - استخدام قاعدة البيانات لتحديد الحالة التالية
                string actualStatus = status;
                string previousStatus = expense.ApprovalStatus ?? "";
                
                // ✅ SECURITY: إذا لم تكن الحالة "rejected"، استخدم قاعدة البيانات لتحديد الحالة التالية
                if (status != "rejected")
                {
                    // ✅ الحصول على دور المستخدم لتحديد القاعدة المناسبة
                    var userRoles = await masterDb.UserRoles
                        .AsNoTracking()
                        .Include(ur => ur.Role)
                        .Where(ur => ur.UserId == approvedBy)
                        .Select(ur => ur.Role!.Code.ToLower())
                        .ToListAsync();

                    // ✅ الحصول على الدور الأساسي للمستخدم (الأول في القائمة)
                    var primaryRole = userRoles.FirstOrDefault() ?? "";
                    
                    // ✅ VALIDATION: Verifier can ONLY approve expenses with Categories 169, 170, or 172
                    if (primaryRole == "verifier" && previousStatus.ToLower() == "awaiting-verifier")
                    {
                        if (!expense.ExpenseCategoryId.HasValue || 
                            (expense.ExpenseCategoryId.Value != 169 && expense.ExpenseCategoryId.Value != 170 && expense.ExpenseCategoryId.Value != 172))
                        {
                            _logger.LogWarning("⚠️ [ApproveExpenseForSupervisor] Verifier attempted to approve expense with invalid category: ExpenseId={ExpenseId}, CategoryId={CategoryId}", 
                                expenseId, expense.ExpenseCategoryId);
                            throw new InvalidOperationException(
                                $"Verifier can only approve expenses with Categories 169, 170, or 172. Current category: {expense.ExpenseCategoryId?.ToString() ?? "null"}");
                        }
                    }
                    
                    _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Using rule service: Role={Role}, FromStatus={FromStatus}, Amount={Amount}, Category={Category}",
                        primaryRole, previousStatus, expense.TotalAmount, expense.ExpenseCategoryId);

                    // ✅ استخدام قاعدة البيانات لتحديد الحالة التالية
                    var ruleDeterminedStatus = await _ruleService.GetNextStatusAsync(
                        primaryRole,
                        previousStatus,
                        expense.TotalAmount,
                        expense.ExpenseCategoryId);

                    if (!string.IsNullOrWhiteSpace(ruleDeterminedStatus))
                    {
                        actualStatus = ruleDeterminedStatus;
                        _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Rule determined status: {Status} (frontend requested: {FrontendStatus})",
                            actualStatus, status);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [ApproveExpenseForSupervisor] No rule found, using frontend status: {Status}", status);
                        // Keep frontend status as fallback if no rule matches
                    }
                }
                else
                        {
                    // ✅ "rejected" is always allowed directly (security exception for explicit rejection)
                    _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Direct rejection allowed: Status={Status}", status);
                }
                
                expense.ApprovalStatus = actualStatus;

                bool awaitingNextLevel = actualStatus == "awaiting-manager" || actualStatus == "awaiting-accountant" || actualStatus == "awaiting-admin" || actualStatus == "awaiting-verifier";
                if (awaitingNextLevel)
                {
                    expense.ApprovedBy = null;
                    expense.ApprovedAt = null;
                }
                else
                {
                    expense.ApprovedBy = approvedBy;
                    expense.ApprovedAt = KsaTime.Now;
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

                await tenantContext.SaveChangesAsync();

                // حفظ سجل الموافقة/الرفض في ExpenseApprovalHistory
                string? actionByFullName = null;
                if (approvedBy > 0)
                {
                    var masterUser = await masterDb.MasterUsers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == approvedBy);
                    actionByFullName = masterUser?.FullName ?? masterUser?.Username;
                }

                string action = actualStatus switch
                {
                    "accepted" => "approved",
                    "rejected" => "rejected",
                    "awaiting-manager" => "awaiting-manager",
                    "awaiting-accountant" => "awaiting-accountant",
                    "awaiting-admin" => "awaiting-admin",
                    "awaiting-verifier" => "awaiting-verifier",
                    _ => "updated"
                };

                string comments = actualStatus switch
                {
                    "accepted" => "تم الموافقة على المصروف",
                    "rejected" => $"تم رفض المصروف{(string.IsNullOrWhiteSpace(rejectionReason) ? "" : $": {rejectionReason}")}",
                    "awaiting-manager" => "في انتظار موافقة مدير العمليات",
                    "awaiting-accountant" => "في انتظار موافقة المحاسب",
                    "awaiting-admin" => "في انتظار موافقة المدير العام",
                    "awaiting-verifier" => "في انتظار مسؤول الجاري",
                    _ => "تم تحديث حالة المصروف"
                };

                var history = new FinanceLedgerAPI.Models.ExpenseApprovalHistory
                {
                    ExpenseId = expense.ExpenseId,
                    Action = action,
                    ActionBy = approvedBy > 0 ? approvedBy : null,
                    ActionByFullName = actionByFullName,
                    ActionAt = KsaTime.Now,
                    Status = actualStatus,
                    RejectionReason = actualStatus == "rejected" ? rejectionReason : null,
                    Comments = comments,
                    Recommendation = !string.IsNullOrWhiteSpace(recommendation) ? recommendation.Trim() : null,
                    RecommendationToUserId = recommendationToUserId > 0 ? recommendationToUserId : null,
                    RecommendationReadBy = null // Initialize as empty - will be JSON array when users read it
                };
                await tenantContext.ExpenseApprovalHistories.AddAsync(history);
                await tenantContext.SaveChangesAsync();
                _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Expense approval history saved: ExpenseId={ExpenseId}, Action={Action}, Status={Status}, ActionBy={ActionBy}", 
                    expense.ExpenseId, action, status, approvedBy);

                _logger.LogInformation("✅ [ApproveExpenseForSupervisor] Expense approval updated: ExpenseId={ExpenseId}, Status={Status}, ApprovedBy={ApprovedBy}, HotelCode={HotelCode}", 
                    expenseId, actualStatus, approvedBy, hotelCode);

                // ✅ تحميل المصروف مع العلاقات لعرضه
                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic (reuse searchHotelId from above)
                // ✅ FIX: Load expense WITHOUT Include HotelSettings because FK relationship is broken
                var updatedExpense = await tenantContext.Expenses
                    .AsNoTracking()
                    .Include(e => e.ExpenseRooms)
                        .ThenInclude(er => er.Apartment)
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == searchHotelId);

                // ✅ إذا لم يتم العثور عليه، نبحث بدون HotelId filter
                if (updatedExpense == null)
                {
                    _logger.LogWarning("⚠️ [ApproveExpenseForSupervisor] Updated expense not found with HotelId filter. Trying without filter: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId={ZaaerId})", 
                        expenseId, searchHotelId, hotelSettings.ZaaerId);
                    updatedExpense = await tenantContext.Expenses
                        .AsNoTracking()
                        .Include(e => e.ExpenseRooms)
                            .ThenInclude(er => er.Apartment)
                        .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);
                }

                if (updatedExpense == null)
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisor] Updated expense not found after save: ExpenseId={ExpenseId}", expenseId);
                    throw new InvalidOperationException($"Failed to retrieve updated expense with id {expenseId}");
                }

                // ✅ Load HotelSettings separately (by HotelCode, not by FK)
                string? hotelName = hotelSettings.HotelName;

                // ✅ Get category name from Master DB
                string? categoryName = null;
                if (updatedExpense.ExpenseCategoryId.HasValue)
                {
                    var masterCategory = await masterDb.ExpenseCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ec => ec.Id == updatedExpense.ExpenseCategoryId.Value);
                    categoryName = masterCategory?.MainCategory;
                }

                // ✅ Get approved by user info (full name, role, tenant) from Master DB
                string? approvedByFullName = actionByFullName; // Already fetched above
                string? approvedByRole = null;
                string? approvedByTenantName = null;
                if (approvedBy > 0)
                {
                    var masterUser = await masterDb.MasterUsers
                        .AsNoTracking()
                        .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                        .Include(u => u.Tenant)
                        .FirstOrDefaultAsync(u => u.Id == approvedBy);
                    
                    if (masterUser != null)
                    {
                        var primaryRole = masterUser.UserRoles?.FirstOrDefault()?.Role;
                        approvedByRole = GetRoleDisplayName(primaryRole?.Code);
                        approvedByTenantName = masterUser.Tenant?.Name;
                    }
                }

                // ✅ تحويل إلى DTO
                var expenseRooms = updatedExpense.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                {
                    ExpenseRoomId = er.ExpenseRoomId,
                    ExpenseId = er.ExpenseId,
                    ZaaerId = er.ZaaerId,
                    Purpose = er.Purpose,
                    Amount = er.Amount,
                    CreatedAt = er.CreatedAt,
                    ApartmentId = er.Apartment?.ApartmentId,
                    ApartmentCode = er.Apartment?.ApartmentCode,
                    ApartmentName = er.Apartment?.ApartmentName
                }).ToList();

                return new ExpenseResponseDto
                {
                    ExpenseId = updatedExpense.ExpenseId,
                    HotelId = updatedExpense.HotelId,
                    HotelName = hotelName, // ✅ Use hotelName loaded separately
                    HotelCode = hotelCode,
                    DateTime = updatedExpense.DateTime,
                    DueDate = updatedExpense.DueDate,
                    Comment = updatedExpense.Comment,
                    ExpenseCategoryId = updatedExpense.ExpenseCategoryId,
                    ExpenseCategoryName = categoryName, // ✅ From Master DB
                    TaxRate = updatedExpense.TaxRate,
                    TaxAmount = updatedExpense.TaxAmount,
                    TotalAmount = updatedExpense.TotalAmount,
                    CreatedAt = updatedExpense.CreatedAt,
                    UpdatedAt = updatedExpense.UpdatedAt,
                    ApprovalStatus = updatedExpense.ApprovalStatus,
                    ApprovedBy = updatedExpense.ApprovedBy,
                    ApprovedByFullName = approvedByFullName,
                    ApprovedByRole = approvedByRole,
                    ApprovedByTenantName = approvedByTenantName,
                    ApprovedAt = updatedExpense.ApprovedAt,
                    RejectionReason = updatedExpense.RejectionReason,
                    ExpenseRooms = expenseRooms
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ApproveExpenseForSupervisor] Error approving expense: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Approve/Reject expense for supervisor across all accessible hotels (searches all tenant databases)
        /// </summary>
        private async Task<ExpenseResponseDto?> ApproveExpenseForSupervisorAcrossAllHotelsAsync(long expenseId, string status, int approvedBy, string? rejectionReason, string? recommendation = null, int? recommendationToUserId = null)
        {
            try
            {
                _logger.LogInformation("🔐 [ApproveExpenseForSupervisorAcrossAllHotels] Approving expense: ExpenseId={ExpenseId}, Status={Status}, UserId={UserId}", 
                    expenseId, status, approvedBy);

                // ✅ Get all tenants the user has access to
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var userTenants = await masterDb.UserTenants
                    .AsNoTracking()
                    .Include(ut => ut.Tenant)
                    .Where(ut => ut.UserId == approvedBy)
                    .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName, ut.Tenant.Name })
                    .ToListAsync();

                // ✅ Get user roles to check if manager/admin/accountant (should see all tenants)
                var rolesList = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == approvedBy)
                    .Select(ur => ur.Role!.Code.ToLower())
                    .ToListAsync();

                var isManagerOrAdminOrAccountant = rolesList.Contains("manager") || 
                                                   rolesList.Contains("admin") || 
                                                   rolesList.Contains("accountant");

                if (isManagerOrAdminOrAccountant)
                {
                    _logger.LogInformation("✅ [ApproveExpenseForSupervisorAcrossAllHotels] Manager/Admin/Accountant - loading all tenants");
                    userTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                        .ToListAsync();
                }

                if (!userTenants.Any())
                {
                    _logger.LogWarning("⚠️ [ApproveExpenseForSupervisorAcrossAllHotels] No tenants found for user: UserId={UserId}", approvedBy);
                    return null;
                }

                // ✅ Get configuration
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [ApproveExpenseForSupervisorAcrossAllHotels] TenantDatabase settings not found");
                    return null;
                }

                // ✅ Search across all tenant databases
                foreach (var userTenant in userTenants)
                {
                    try
                    {
                        var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        // ✅ Check if expense exists in this tenant database
                        var expense = await tenantContext.Expenses
                            .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                        if (expense != null)
                        {
                            // ✅ Found the expense - approve/reject it
                            _logger.LogInformation("✅ [ApproveExpenseForSupervisorAcrossAllHotels] Found expense in tenant: {Code}", userTenant.Code);

                            // ✅ Update approval status
                            expense.ApprovalStatus = status;

                            bool awaitingNextLevel = status == "awaiting-manager" || status == "awaiting-accountant" || status == "awaiting-admin";
                            if (awaitingNextLevel)
                            {
                                expense.ApprovedBy = null;
                                expense.ApprovedAt = null;
                            }
                            else
                            {
                                expense.ApprovedBy = approvedBy;
                                expense.ApprovedAt = KsaTime.Now;
                            }
                            expense.UpdatedAt = KsaTime.Now;

                            // ✅ Update rejection reason if provided
                            if (status == "rejected" && !string.IsNullOrWhiteSpace(rejectionReason))
                            {
                                expense.RejectionReason = rejectionReason;
                            }
                            else if (status != "rejected")
                            {
                                expense.RejectionReason = null;
                            }

                            await tenantContext.SaveChangesAsync();

                            // ✅ Save approval history
                            string? actionByFullName = null;
                            if (approvedBy > 0)
                            {
                                var masterUser = await masterDb.MasterUsers
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(u => u.Id == approvedBy);
                                actionByFullName = masterUser?.FullName ?? masterUser?.Username;
                            }

                            string action = status switch
                            {
                                "accepted" => "approved",
                                "rejected" => "rejected",
                                "awaiting-manager" => "awaiting-manager",
                                "awaiting-accountant" => "awaiting-accountant",
                                "awaiting-admin" => "awaiting-admin",
                                _ => "updated"
                            };

                            string comments = status switch
                            {
                                "accepted" => "تم الموافقة على المصروف",
                                "rejected" => $"تم رفض المصروف{(string.IsNullOrWhiteSpace(rejectionReason) ? "" : $": {rejectionReason}")}",
                                "awaiting-manager" => "في انتظار موافقة مدير العمليات",
                                "awaiting-accountant" => "في انتظار موافقة المحاسب",
                                "awaiting-admin" => "في انتظار موافقة المدير العام",
                                _ => "تم تحديث حالة المصروف"
                            };

                            var history = new FinanceLedgerAPI.Models.ExpenseApprovalHistory
                            {
                                ExpenseId = expense.ExpenseId,
                                Action = action,
                                ActionBy = approvedBy > 0 ? approvedBy : null,
                                ActionByFullName = actionByFullName,
                                ActionAt = KsaTime.Now,
                                Status = status,
                                RejectionReason = status == "rejected" ? rejectionReason : null,
                                Comments = comments,
                                Recommendation = !string.IsNullOrWhiteSpace(recommendation) ? recommendation.Trim() : null,
                                RecommendationToUserId = recommendationToUserId > 0 ? recommendationToUserId : null,
                                RecommendationReadBy = null // Initialize as empty - will be JSON array when users read it
                            };
                            await tenantContext.ExpenseApprovalHistories.AddAsync(history);
                            await tenantContext.SaveChangesAsync();

                            // ✅ Load updated expense with relationships
                            // ✅ FIX: Load expense WITHOUT Include HotelSettings because FK relationship is broken
                            var updatedExpense = await tenantContext.Expenses
                                .AsNoTracking()
                                .Include(e => e.ExpenseRooms)
                                    .ThenInclude(er => er.Apartment)
                                .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                            if (updatedExpense == null)
                            {
                                _logger.LogError("❌ [ApproveExpenseForSupervisorAcrossAllHotels] Updated expense not found after save: ExpenseId={ExpenseId}", expenseId);
                                return null;
                            }

                            // ✅ Load HotelSettings separately (by ZaaerId, not by FK)
                            string? hotelName = null;
                            if (updatedExpense.HotelId > 0)
                            {
                                var hotelSettings = await tenantContext.HotelSettings
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(h => h.ZaaerId == updatedExpense.HotelId);
                                hotelName = hotelSettings?.HotelName ?? userTenant.Name;
                            }
                            else
                            {
                                hotelName = userTenant.Name;
                            }

                            // ✅ Get category name from Master DB
                            string? categoryName = null;
                            if (updatedExpense.ExpenseCategoryId.HasValue)
                            {
                                var masterCategory = await masterDb.ExpenseCategories
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(ec => ec.Id == updatedExpense.ExpenseCategoryId.Value);
                                categoryName = masterCategory?.MainCategory;
                            }

                            // ✅ Get approved by user info (full name, role, tenant)
                            string? approvedByFullName = actionByFullName;
                            string? approvedByRole = null;
                            string? approvedByTenantName = null;
                            if (approvedBy > 0)
                            {
                                var masterUser = await masterDb.MasterUsers
                                    .AsNoTracking()
                                    .Include(u => u.UserRoles)
                                        .ThenInclude(ur => ur.Role)
                                    .Include(u => u.Tenant)
                                    .FirstOrDefaultAsync(u => u.Id == approvedBy);
                                
                                if (masterUser != null)
                                {
                                    var primaryRole = masterUser.UserRoles?.FirstOrDefault()?.Role;
                                    approvedByRole = GetRoleDisplayName(primaryRole?.Code);
                                    approvedByTenantName = masterUser.Tenant?.Name;
                                }
                            }

                            // ✅ Convert to DTO
                            var expenseRooms = updatedExpense.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                ZaaerId = er.ZaaerId,
                                Purpose = er.Purpose,
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt,
                                ApartmentId = er.Apartment?.ApartmentId,
                                ApartmentCode = er.Apartment?.ApartmentCode,
                                ApartmentName = er.Apartment?.ApartmentName
                            }).ToList();

                            _logger.LogInformation("✅ [ApproveExpenseForSupervisorAcrossAllHotels] Expense approved successfully: ExpenseId={ExpenseId}, Status={Status}, Tenant={Code}", 
                                expenseId, status, userTenant.Code);

                            return new ExpenseResponseDto
                            {
                                ExpenseId = updatedExpense.ExpenseId,
                                HotelId = updatedExpense.HotelId,
                                HotelName = hotelName, // ✅ Use hotelName loaded separately
                                HotelCode = userTenant.Code,
                                DateTime = updatedExpense.DateTime,
                                DueDate = updatedExpense.DueDate,
                                Comment = updatedExpense.Comment,
                                ExpenseCategoryId = updatedExpense.ExpenseCategoryId,
                                ExpenseCategoryName = categoryName, // ✅ From Master DB
                                TaxRate = updatedExpense.TaxRate,
                                TaxAmount = updatedExpense.TaxAmount,
                                TotalAmount = updatedExpense.TotalAmount,
                                CreatedAt = updatedExpense.CreatedAt,
                                UpdatedAt = updatedExpense.UpdatedAt,
                                ApprovalStatus = updatedExpense.ApprovalStatus,
                                ApprovedBy = updatedExpense.ApprovedBy,
                                ApprovedByFullName = approvedByFullName,
                                ApprovedByRole = approvedByRole,
                                ApprovedByTenantName = approvedByTenantName,
                                ApprovedAt = updatedExpense.ApprovedAt,
                                RejectionReason = updatedExpense.RejectionReason,
                                ExpenseRooms = expenseRooms
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [ApproveExpenseForSupervisorAcrossAllHotels] Error searching tenant {Code}: {Message}", 
                            userTenant.Code, ex.Message);
                        // Continue searching other tenants
                    }
                }

                _logger.LogWarning("⚠️ [ApproveExpenseForSupervisorAcrossAllHotels] Expense not found in any tenant database: ExpenseId={ExpenseId}", expenseId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ApproveExpenseForSupervisorAcrossAllHotels] Error: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Update expense for supervisor in specific tenant database (targeted by hotelCode)
        /// Uses X-Hotel-Code header to target the correct tenant database directly
        /// Similar to GetExpenseByIdForSupervisorAsync - targets one specific database, not all
        /// </summary>
        private async Task<ExpenseResponseDto?> UpdateExpenseForSupervisorAsync(long expenseId, UpdateExpenseDto dto, string hotelCode)
        {
            try
            {
                _logger.LogInformation("🔐 [UpdateExpenseForSupervisor] Updating expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                    expenseId, hotelCode);

                // ✅ Get tenant information from Master DB using hotelCode
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var tenant = await masterDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code.ToLower() == hotelCode.ToLower());

                if (tenant == null)
                {
                    _logger.LogError("❌ [UpdateExpenseForSupervisor] Tenant not found for HotelCode: {HotelCode}", hotelCode);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                {
                    _logger.LogError("❌ [UpdateExpenseForSupervisor] DatabaseName not set for Tenant: {Code}", tenant.Code);
                    return null;
                }

                // ✅ Build connection string for the specific tenant database
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [UpdateExpenseForSupervisor] TenantDatabase settings not found in configuration");
                    return null;
                }

                var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                // ✅ Create DbContext for the specific tenant database
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);
                using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                // ✅ Find expense in the specific tenant database (no hotel_id filter - tenant DB isolation is enough)
                var expense = await tenantContext.Expenses
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [UpdateExpenseForSupervisor] Expense not found: ExpenseId={ExpenseId}, HotelCode={HotelCode}, DatabaseName={DatabaseName}", 
                        expenseId, hotelCode, tenant.DatabaseName);
                    return null;
                }

                // ✅ Found the expense - update it
                _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Found expense in tenant database: ExpenseId={ExpenseId}, HotelCode={HotelCode}, DatabaseName={DatabaseName}", 
                    expenseId, hotelCode, tenant.DatabaseName);

                // ✅ Handle resubmission of rejected expenses FIRST (before editability check)
                // This allows rejected expenses to be updated when resubmitting
                var approvalStatus = expense.ApprovalStatus?.Trim();
                _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] Checking resubmission - CurrentStatus: {CurrentStatus}, DTO.ApprovalStatus: {DtoApprovalStatus}, DTO.PaymentSource: {DtoPaymentSource}", 
                    approvalStatus ?? "null", dto.ApprovalStatus ?? "null", dto.PaymentSource ?? "null");
                
                if (string.Equals(approvalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] Expense is rejected, automatically resubmitting...");
                    
                    // ✅ Always resubmit rejected expenses when they are updated
                    // Determine new status based on ApprovalStatus from DTO, or PaymentSource, or existing PaymentSource
                    string? newApprovalStatus = null;
                    
                    // Priority 1: Use ApprovalStatus from DTO if explicitly provided
                    if (!string.IsNullOrWhiteSpace(dto.ApprovalStatus))
                    {
                        newApprovalStatus = dto.ApprovalStatus.Trim();
                        _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] Using ApprovalStatus from DTO: {NewStatus}", newApprovalStatus);
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
                            _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] Determined new status from PaymentSource '{PaymentSource}': {NewStatus}", 
                                paymentSourceToCheck, newApprovalStatus);
                        }
                        else
                        {
                            // Priority 3: Default to pending
                            newApprovalStatus = "pending";
                            _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] No PaymentSource found, defaulting to: {NewStatus}", newApprovalStatus);
                        }
                    }
                    
                    // Always update status and reset rejection fields for rejected expenses
                    _logger.LogInformation("🔄 [UpdateExpenseForSupervisor] Resubmitting rejected expense: ExpenseId={ExpenseId}, NewStatus={NewStatus}", 
                        expenseId, newApprovalStatus);
                    
                    // Update approval status BEFORE editability check
                    expense.ApprovalStatus = newApprovalStatus;
                    
                    // Reset rejection-related fields
                    expense.RejectionReason = null;
                    expense.ApprovedBy = null;
                    expense.ApprovedAt = null;
                    
                    _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Reset rejection fields and updated status to: {NewStatus}", 
                        expense.ApprovalStatus);
                }

                // ✅ Now check editability using the UPDATED status (if resubmitting, status is already changed)
                var currentStatusForCheck = expense.ApprovalStatus?.Trim();
                var editableStatuses = new[] { "pending", "awaiting-manager", "awaiting-accountant", "awaiting-admin", "awaiting-officer", "awaiting-verifier", "rejected", "auto-approved" };
                if (!string.IsNullOrWhiteSpace(currentStatusForCheck) && !editableStatuses.Contains(currentStatusForCheck, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("⚠️ [UpdateExpenseForSupervisor] Attempt to update locked expense: ExpenseId={ExpenseId}, Status={Status}", expenseId, currentStatusForCheck);
                    throw new InvalidOperationException(
                        $"لا يمكن تعديل هذا المصروف لأن حالته الحالية هي '{currentStatusForCheck}'. يرجى تحديث الصفحة قبل إعادة المحاولة.");
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
                    _logger.LogInformation("🟢 [UpdateExpenseForSupervisor] Updated PaymentSource: {PaymentSource}", expense.PaymentSource);
                }

                // Update fields
                if (dto.DateTime.HasValue)
                {
                    DateTime updatedDateTime = dto.DateTime.Value.Kind == DateTimeKind.Utc 
                        ? KsaTime.ConvertFromUtc(dto.DateTime.Value) 
                        : KsaTime.ConvertFromUtc(dto.DateTime.Value.ToUniversalTime());
                    expense.DateTime = updatedDateTime;
                }
                if (dto.DueDate.HasValue)
                {
                    DateTime updatedDueDate = dto.DueDate.Value.Kind == DateTimeKind.Utc 
                        ? KsaTime.ConvertFromUtc(dto.DueDate.Value).Date 
                        : KsaTime.ConvertFromUtc(dto.DueDate.Value.ToUniversalTime()).Date;
                    expense.DueDate = updatedDueDate;
                }
                if (dto.Comment != null)
                {
                    expense.Comment = dto.Comment;
                }
                if (dto.ExpenseCategoryId.HasValue)
                {
                    expense.ExpenseCategoryId = dto.ExpenseCategoryId;
                }
                if (dto.TaxRate.HasValue)
                {
                    expense.TaxRate = dto.TaxRate.Value;
                }
                if (dto.TaxAmount.HasValue)
                {
                    expense.TaxAmount = dto.TaxAmount.Value;
                }
                if (dto.TotalAmount.HasValue)
                {
                    expense.TotalAmount = dto.TotalAmount.Value;
                }
                
                expense.UpdatedAt = KsaTime.Now;
                
                // ✅ Capture current user ID who is updating the expense
                int? currentUserId = null;
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                {
                    if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                    {
                        currentUserId = parsedUserId;
                        expense.UpdatedBy = currentUserId;
                        _logger.LogInformation("🔐 [UpdateExpenseForSupervisor] Set UpdatedBy: {UpdatedBy}", expense.UpdatedBy);
                    }
                }
                
                if (currentUserId == null)
                {
                    _logger.LogWarning("⚠️ [UpdateExpenseForSupervisor] Could not determine current user ID from HttpContext");
                }
                
                // ✅ Update expense rooms if provided (mirror logic from ExpenseService.UpdateAsync)
                if (dto.ExpenseRooms != null && dto.ExpenseRooms.Any())
                {
                    _logger.LogInformation("🟢 [UpdateExpenseForSupervisor] Updating expense rooms for ExpenseId={ExpenseId}", expenseId);

                    // 1) Delete existing rooms
                    var existingRooms = await tenantContext.ExpenseRooms
                        .Where(er => er.ExpenseId == expense.ExpenseId)
                        .ToListAsync();

                    if (existingRooms.Any())
                    {
                        tenantContext.ExpenseRooms.RemoveRange(existingRooms);
                        await tenantContext.SaveChangesAsync();
                        _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Deleted {Count} existing expense rooms", existingRooms.Count);
                    }

                    // 2) Get all HotelIds with the same HotelCode
                    var allHotelIdsWithSameCode = await tenantContext.HotelSettings
                        .AsNoTracking()
                        .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower())
                        .Select(h => h.HotelId)
                        .ToListAsync();

                    // 3) Add new rooms
                    foreach (var roomDto in dto.ExpenseRooms)
                    {
                        // Category-based room (CAT_BUILDING, CAT_RECEPTION, CAT_CORRIDORS)
                        if (!string.IsNullOrEmpty(roomDto.CategoryCode) && roomDto.CategoryCode.StartsWith("CAT_"))
                        {
                            var categoryExpenseRoom = new ExpenseRoomModel
                            {
                                ExpenseId = expense.ExpenseId,
                                // ✅ Use a negative placeholder ZaaerId per category code to avoid UNIQUE KEY (ExpenseId, ZaaerId) violations
                                // while still treating these rows as category rows (no matching apartment)
                                ZaaerId = GetCategoryPlaceholderZaaerId(roomDto.CategoryCode),
                                Purpose = roomDto.CategoryCode + (string.IsNullOrEmpty(roomDto.Purpose) ? "" : " - " + roomDto.Purpose),
                                Amount = roomDto.Amount,
                                CreatedAt = KsaTime.Now
                            };

                            await tenantContext.ExpenseRooms.AddAsync(categoryExpenseRoom);
                            _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Added category room: ExpenseId={ExpenseId}, CategoryCode={CategoryCode}, Purpose={Purpose}, Amount={Amount}",
                                expense.ExpenseId, roomDto.CategoryCode, roomDto.Purpose, roomDto.Amount);
                            continue;
                        }

                        // Actual apartment-based room
                        Apartment? apartment = null;

                        if (roomDto.ApartmentId.HasValue)
                        {
                            apartment = await tenantContext.Apartments
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.ApartmentId == roomDto.ApartmentId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));
                        }
                        else if (roomDto.ZaaerId.HasValue)
                        {
                            _logger.LogInformation("🔍 [UpdateExpenseForSupervisor] Searching for apartment with ZaaerId={ZaaerId}, HotelIds={HotelIds}",
                                roomDto.ZaaerId.Value, string.Join(", ", allHotelIdsWithSameCode));

                            apartment = await tenantContext.Apartments
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value && allHotelIdsWithSameCode.Contains(a.HotelId));

                            if (apartment == null)
                            {
                                // Fallback without HotelId filter
                                _logger.LogWarning("⚠️ [UpdateExpenseForSupervisor] Apartment not found with HotelId filter, trying without filter...");
                                apartment = await tenantContext.Apartments
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(a => a.ZaaerId == roomDto.ZaaerId.Value);
                            }
                        }

                        if (apartment == null)
                        {
                            _logger.LogError("❌ [UpdateExpenseForSupervisor] Apartment not found: ApartmentId={ApartmentId}, ZaaerId={ZaaerId}, HotelIds={HotelIds}",
                                roomDto.ApartmentId, roomDto.ZaaerId, string.Join(", ", allHotelIdsWithSameCode));
                            continue;
                        }

                        if (!apartment.ZaaerId.HasValue)
                        {
                            _logger.LogWarning("⚠️ [UpdateExpenseForSupervisor] Apartment found but ZaaerId is null: ApartmentId={ApartmentId}, Name={Name}",
                                apartment.ApartmentId, apartment.ApartmentName);
                            continue;
                        }

                        var roomExpenseRoom = new ExpenseRoomModel
                        {
                            ExpenseId = expense.ExpenseId,
                            ZaaerId = apartment.ZaaerId.Value,
                            Purpose = roomDto.Purpose,
                            Amount = roomDto.Amount,
                            CreatedAt = KsaTime.Now
                        };

                        await tenantContext.ExpenseRooms.AddAsync(roomExpenseRoom);
                        _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Added room: ExpenseId={ExpenseId}, ZaaerId={ZaaerId}, Purpose={Purpose}, Amount={Amount}",
                            expense.ExpenseId, apartment.ZaaerId.Value, roomDto.Purpose, roomDto.Amount);
                    }

                    await tenantContext.SaveChangesAsync();
                    _logger.LogInformation("🟢 [UpdateExpenseForSupervisor] Saved expense rooms changes for ExpenseId={ExpenseId}", expense.ExpenseId);
                }

                // Save main expense changes (rooms already saved if any)
                await tenantContext.SaveChangesAsync();

                // ✅ Reload expense with relationships for response
                // ✅ FIX: Load expense WITHOUT Include HotelSettings because FK relationship is broken
                var updatedExpense = await tenantContext.Expenses
                    .AsNoTracking()
                    .Include(e => e.ExpenseRooms)
                        .ThenInclude(er => er.Apartment)
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId);

                if (updatedExpense == null)
                {
                    _logger.LogError("❌ [UpdateExpenseForSupervisor] Updated expense not found after save: ExpenseId={ExpenseId}", expenseId);
                    return null;
                }

                // ✅ Load HotelSettings separately (by HotelCode, not by FK)
                var hotelSettingsForTenant = await tenantContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower());
                string? hotelName = hotelSettingsForTenant?.HotelName ?? tenant.Name;

                // ✅ Get category name from Master DB
                string? categoryName = null;
                if (updatedExpense.ExpenseCategoryId.HasValue)
                {
                    var masterCategory = await masterDb.ExpenseCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ec => ec.Id == updatedExpense.ExpenseCategoryId.Value);
                    categoryName = masterCategory?.MainCategory;
                }

                // ✅ Map expense rooms
                var expenseRooms = updatedExpense.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                {
                    ExpenseRoomId = er.ExpenseRoomId,
                    ExpenseId = er.ExpenseId,
                    ZaaerId = er.ZaaerId,
                    Purpose = er.Purpose,
                    Amount = er.Amount,
                    CreatedAt = er.CreatedAt,
                    ApartmentId = er.Apartment?.ApartmentId,
                    ApartmentCode = er.Apartment?.ApartmentCode,
                    ApartmentName = er.Apartment?.ApartmentName
                }).ToList();

                _logger.LogInformation("✅ [UpdateExpenseForSupervisor] Expense updated successfully: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                    expenseId, hotelCode);

                return new ExpenseResponseDto
                {
                    ExpenseId = updatedExpense.ExpenseId,
                    HotelId = updatedExpense.HotelId,
                    HotelName = hotelName, // ✅ Use hotelName loaded separately
                    HotelCode = hotelCode,
                    DateTime = updatedExpense.DateTime,
                    DueDate = updatedExpense.DueDate,
                    Comment = updatedExpense.Comment,
                    ExpenseCategoryId = updatedExpense.ExpenseCategoryId,
                    ExpenseCategoryName = categoryName,
                    TaxRate = updatedExpense.TaxRate,
                    TaxAmount = updatedExpense.TaxAmount,
                    TotalAmount = updatedExpense.TotalAmount,
                    CreatedAt = updatedExpense.CreatedAt,
                    UpdatedAt = updatedExpense.UpdatedAt,
                    UpdatedBy = updatedExpense.UpdatedBy,
                    ApprovalStatus = updatedExpense.ApprovalStatus,
                    ApprovedBy = updatedExpense.ApprovedBy,
                    ApprovedAt = updatedExpense.ApprovedAt,
                    RejectionReason = updatedExpense.RejectionReason,
                    ExpenseRooms = expenseRooms
                };
            }
            catch (InvalidOperationException)
            {
                // Re-throw InvalidOperationException (e.g., locked expense)
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [UpdateExpenseForSupervisor] Error: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Returns a stable negative placeholder ZaaerId for room categories to avoid UNIQUE KEY (ExpenseId, ZaaerId) conflicts.
        /// These IDs do NOT correspond to real apartments and are only used to satisfy the unique index.
        /// </summary>
        private static int GetCategoryPlaceholderZaaerId(string categoryCode)
        {
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                return -9999;
            }

            return categoryCode.ToUpperInvariant() switch
            {
                "CAT_BUILDING" => -1001,
                "CAT_RECEPTION" => -1002,
                "CAT_CORRIDORS" => -1003,
                _ => -1999
            };
        }

        /// <summary>
        /// الحصول على صور مصروف للمشرف (مع تحديد قاعدة البيانات الصحيحة)
        /// Get expense images for supervisor (with correct database identification)
        /// </summary>
        private async Task<List<object>?> GetExpenseImagesForSupervisorAsync(long expenseId, string hotelCode)
        {
            try
            {
                _logger.LogInformation("📸 [GetExpenseImagesForSupervisor] Fetching images for expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                    expenseId, hotelCode);

                // ✅ الحصول على معلومات Tenant من Master DB
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var tenant = await masterDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code.ToLower() == hotelCode.ToLower());

                if (tenant == null)
                {
                    _logger.LogError("❌ [GetExpenseImagesForSupervisor] Tenant not found for HotelCode: {HotelCode}", hotelCode);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                {
                    _logger.LogError("❌ [GetExpenseImagesForSupervisor] DatabaseName not set for Tenant: {Code}", tenant.Code);
                    return null;
                }

                // ✅ بناء connection string للـ tenant
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [GetExpenseImagesForSupervisor] TenantDatabase settings not found in configuration");
                    return null;
                }

                var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                // ✅ إنشاء DbContext للـ tenant
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);
                using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                // ✅ الحصول على HotelId من HotelSettings
                var hotelSettings = await tenantContext.HotelSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == hotelCode.ToLower());

                if (hotelSettings == null)
                {
                    _logger.LogError("❌ [GetExpenseImagesForSupervisor] HotelSettings not found for HotelCode: {HotelCode}", hotelCode);
                    return null;
                }

                // ✅ التحقق من وجود المصروف في قاعدة البيانات الصحيحة
                // ✅ Use ZaaerId instead of HotelId to match the expense creation logic
                int? searchHotelId = hotelSettings.ZaaerId ?? hotelSettings.HotelId;
                var expense = await tenantContext.Expenses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ExpenseId == expenseId && e.HotelId == searchHotelId);

                if (expense == null)
                {
                    _logger.LogWarning("⚠️ [GetExpenseImagesForSupervisor] Expense not found: ExpenseId={ExpenseId}, HotelId={HotelId} (ZaaerId={ZaaerId}), HotelCode={HotelCode}", 
                        expenseId, searchHotelId, hotelSettings.ZaaerId, hotelCode);
                    return null;
                }

                // ✅ الحصول على الصور
                var images = await tenantContext.ExpenseImages
                    .AsNoTracking()
                    .Where(ei => ei.ExpenseId == expenseId)
                    .OrderBy(ei => ei.DisplayOrder)
                    .ThenBy(ei => ei.CreatedAt)
                    .Select(ei => new
                    {
                        expenseImageId = ei.ExpenseImageId,
                        imageUrl = ei.ImagePath.StartsWith("http") ? ei.ImagePath : $"{Request.Scheme}://{Request.Host}{ei.ImagePath}",
                        imagePath = ei.ImagePath,
                        originalFilename = ei.OriginalFilename,
                        fileSize = ei.FileSize,
                        contentType = ei.ContentType,
                        displayOrder = ei.DisplayOrder,
                        createdAt = ei.CreatedAt
                    })
                    .ToListAsync<object>();

                _logger.LogInformation("✅ [GetExpenseImagesForSupervisor] Successfully retrieved {Count} images for expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                    images.Count, expenseId, hotelCode);

                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetExpenseImagesForSupervisor] Error fetching expense images: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// الحصول على جميع المصروفات من عدة tenants للمشرف
        /// Get all expenses from multiple tenants for supervisor
        /// </summary>
        /// <returns>قائمة المصروفات من جميع الفنادق التابعة للمشرف</returns>
        [HttpGet("supervisor/all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetSupervisorExpenses()
        {
            try
            {
                // ✅ استخراج معلومات المستخدم من JWT Token
                var userIdClaim = HttpContext.Items["UserId"]?.ToString();
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("⚠️ [GetSupervisorExpenses] UserId not found in JWT token");
                    return Unauthorized(new { error = "User information not found in token" });
                }

                _logger.LogInformation("📋 [GetSupervisorExpenses] Fetching expenses for supervisor UserId: {UserId}", userId);

                // ✅ الحصول على قائمة الفنادق التابعة للمشرف من UserTenants
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                
                // ✅ محاولة استخراج الأدوار من HttpContext أولاً
                var roleCsv = HttpContext.Items["Roles"]?.ToString() ?? string.Empty;
                _logger.LogInformation("🔍 [GetSupervisorExpenses] Raw roles CSV from HttpContext for UserId {UserId}: '{RoleCsv}'", userId, roleCsv);
                
                var rolesList = roleCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                       .Select(r => r.Trim().ToLower())
                                       .Where(r => !string.IsNullOrWhiteSpace(r))
                                       .ToList();
                
                // ✅ إذا لم تكن الأدوار متوفرة في HttpContext، جلبها مباشرة من قاعدة البيانات
                if (!rolesList.Any())
                {
                    _logger.LogWarning("⚠️ [GetSupervisorExpenses] No roles found in HttpContext for UserId {UserId}. Fetching from database.", userId);
                    var dbRoles = await masterDb.UserRoles
                        .AsNoTracking()
                        .Include(ur => ur.Role)
                        .Where(ur => ur.UserId == userId)
                        .Select(ur => ur.Role!.Code)
                        .ToListAsync();
                    
                    _logger.LogInformation("📋 [GetSupervisorExpenses] Raw roles from database for UserId {UserId}: {RawRoles}", userId, string.Join(", ", dbRoles));
                    
                    rolesList = dbRoles.Where(r => !string.IsNullOrWhiteSpace(r))
                                      .Select(r => r.Trim().ToLower())
                                      .ToList();
                    _logger.LogInformation("📋 [GetSupervisorExpenses] Fetched and normalized roles from database for UserId {UserId}: {Roles}", userId, string.Join(", ", rolesList));
                }
                else
                {
                    _logger.LogInformation("📋 [GetSupervisorExpenses] Roles from HttpContext (normalized) for UserId {UserId}: {Roles}", userId, string.Join(", ", rolesList));
                }
                
                var isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier = rolesList.Contains("manager") || rolesList.Contains("admin") || rolesList.Contains("accountant") || rolesList.Contains("officer") || rolesList.Contains("owner") || rolesList.Contains("verifier");
                _logger.LogInformation("🔍 [GetSupervisorExpenses] UserId {UserId} - isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier: {IsFullAccess} (checked for 'manager', 'admin', 'accountant', 'officer', 'owner', or 'verifier' in: [{Roles}])", 
                    userId, isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier, string.Join(", ", rolesList));

                var userTenants = await masterDb.UserTenants
                    .AsNoTracking()
                    .Include(ut => ut.Tenant)
                    .Where(ut => ut.UserId == userId)
                    .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName, ut.Tenant.Name })
                    .ToListAsync();

                _logger.LogInformation("📊 [GetSupervisorExpenses] UserId {UserId} - Found {Count} tenants from UserTenants table", userId, userTenants.Count);

                if (isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier)
                {
                    _logger.LogInformation("✅ [GetSupervisorExpenses] Manager/Admin/Accountant/Officer/Owner/Verifier role detected for UserId {UserId}. Loading all tenants.", userId);
                    userTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                        .ToListAsync();
                    _logger.LogInformation("✅ [GetSupervisorExpenses] Loaded {Count} tenants for Manager/Admin/Accountant/Officer/Owner/Verifier", userTenants.Count);
                }
                else if (!userTenants.Any())
                {
                    _logger.LogWarning("⚠️ [GetSupervisorExpenses] No tenants linked to user {UserId}. Loading all tenants (fallback).", userId);
                    userTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                        .ToListAsync();

                    if (!userTenants.Any())
                    {
                        return Ok(new List<ExpenseResponseDto>());
                    }
                }

                _logger.LogInformation("✅ [GetSupervisorExpenses] Found {Count} tenants for supervisor", userTenants.Count);

                var allExpenses = new List<ExpenseResponseDto>();

                // ✅ Performance Optimization: Get configuration once
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();
                
                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [GetSupervisorExpenses] TenantDatabase settings not found in configuration");
                    return Ok(new List<ExpenseResponseDto>());
                }

                // ✅ Performance Optimization: Use Parallel processing to fetch from all tenants simultaneously
                var tenantExpensesTasks = userTenants.Select(async userTenant =>
                {
                    try
                    {
                        var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                        // ✅ إنشاء DbContext للـ tenant (using for proper disposal)
                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        await using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        // ✅ الحصول على HotelIds من هذا Tenant
                        // First try to match by HotelCode == Tenant.Code
                        var hotelSettings = await tenantContext.HotelSettings
                            .AsNoTracking()
                            .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == userTenant.Code.ToLower())
                            .Select(h => h.HotelId)
                            .ToListAsync();

                        // ✅ FALLBACK: If no match found, get ALL HotelIds from this tenant database
                        // This handles cases where hotel_code was changed or doesn't match Tenant.Code
                        if (!hotelSettings.Any())
                        {
                            _logger.LogWarning("⚠️ [GetSupervisorExpenses] No HotelSettings found matching Tenant Code '{Code}'. Getting ALL HotelIds from tenant database as fallback.", userTenant.Code);
                            
                            // Get all HotelIds from this tenant database
                            var allHotelIds = await tenantContext.HotelSettings
                                .AsNoTracking()
                                .Select(h => h.HotelId)
                                .ToListAsync();
                            
                            if (allHotelIds.Any())
                            {
                                hotelSettings = allHotelIds;
                                _logger.LogInformation("✅ [GetSupervisorExpenses] Using {Count} HotelIds from tenant database (fallback mode)", hotelSettings.Count);
                                
                                // Log all hotel_codes for debugging
                                var allHotelCodes = await tenantContext.HotelSettings
                                    .AsNoTracking()
                                    .Select(h => new { h.HotelId, h.HotelCode })
                                    .ToListAsync();
                                _logger.LogInformation("📋 [GetSupervisorExpenses] Available HotelSettings in tenant DB: {HotelSettings}", 
                                    string.Join(", ", allHotelCodes.Select(h => $"HotelId={h.HotelId}, HotelCode='{h.HotelCode}'")));
                            }
                            else
                            {
                                _logger.LogError("❌ [GetSupervisorExpenses] No HotelSettings found at all in tenant database: {DatabaseName}", userTenant.DatabaseName);
                            return new List<ExpenseResponseDto>();
                        }
                        }
                        else
                        {
                            _logger.LogInformation("✅ [GetSupervisorExpenses] Found {Count} HotelSettings matching Tenant Code '{Code}': HotelIds = {HotelIds}", 
                                hotelSettings.Count, userTenant.Code, string.Join(", ", hotelSettings));
                        }

                        // ✅ DIAGNOSTIC: Check what hotel_ids are actually in expenses table
                        var expenseHotelIds = await tenantContext.Expenses
                            .AsNoTracking()
                            .Select(e => e.HotelId)
                            .Distinct()
                            .ToListAsync();
                        _logger.LogInformation("🔍 [GetSupervisorExpenses] Tenant '{Code}' - Expenses table contains HotelIds: {ExpenseHotelIds}, Expected HotelIds: {ExpectedHotelIds}", 
                            userTenant.Code, string.Join(", ", expenseHotelIds), string.Join(", ", hotelSettings));

                        // ✅ الحصول على المصروفات من هذا Tenant (optimized query)
                        // ✅ CRITICAL FIX: Get ALL expenses from tenant database, regardless of hotel_id
                        // Each tenant database should only contain expenses for that tenant anyway
                        // Filtering by hotel_id can cause issues if hotel_code was changed or expenses have wrong hotel_id
                        // ✅ FIX: Load expenses WITHOUT Include HotelSettings because FK relationship is broken
                        var expenses = await tenantContext.Expenses
                            .AsNoTracking()
                            .Include(e => e.ExpenseRooms)
                                .ThenInclude(er => er.Apartment)
                            // ✅ Removed hotel_id filter - get ALL expenses from this tenant database
                            .OrderByDescending(e => e.DateTime)
                            .ToListAsync();
                        
                        // ✅ Load HotelSettings separately (by HotelCode, not by FK)
                        var hotelSettingsForTenant = await tenantContext.HotelSettings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == userTenant.Code.ToLower());
                        var hotelName = hotelSettingsForTenant?.HotelName ?? userTenant.Name;
                        
                        // ✅ Project in memory (after loading from DB)
                        var tenantExpenses = expenses.Select(e => new
                        {
                            Expense = e,
                            HotelName = hotelName,
                            HotelCode = userTenant.Code,
                            ExpenseRooms = e.ExpenseRooms.Select(er => new
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                ZaaerId = er.ZaaerId,
                                Purpose = er.Purpose,
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt,
                                Apartment = er.Apartment != null ? new
                                {
                                    ApartmentId = er.Apartment.ApartmentId,
                                    ApartmentCode = er.Apartment.ApartmentCode,
                                    ApartmentName = er.Apartment.ApartmentName
                                } : null
                            }).ToList()
                        }).ToList();

                        _logger.LogInformation("📊 [GetSupervisorExpenses] Tenant '{Code}' - Found {Count} expenses (all expenses from database, not filtered by hotel_id)", 
                            userTenant.Code, tenantExpenses.Count);

                        // ✅ Get all unique category IDs from expenses
                        var categoryIds = tenantExpenses
                            .Where(e => e.Expense.ExpenseCategoryId.HasValue)
                            .Select(e => e.Expense.ExpenseCategoryId!.Value)
                            .Distinct()
                            .ToList();

                        // ✅ Load category names from Master DB using a NEW scope for this task
                        // CRITICAL: Each parallel task needs its own DbContext instance to avoid concurrency issues
                        Dictionary<int, string> masterCategories;
                        if (categoryIds.Any())
                        {
                            // Create a new scope for this task to get a fresh DbContext instance
                            using var scope = HttpContext.RequestServices.CreateScope();
                            var masterDbForTask = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
                            masterCategories = await masterDbForTask.ExpenseCategories
                                .AsNoTracking()
                                .Where(ec => categoryIds.Contains(ec.Id))
                                .ToDictionaryAsync(ec => ec.Id, ec => ec.MainCategory);
                        }
                        else
                        {
                            masterCategories = new Dictionary<int, string>();
                        }

                        // ✅ Get all unique ApprovedBy user IDs from expenses
                        var approvedByUserIds = tenantExpenses
                            .Where(e => e.Expense.ApprovedBy.HasValue)
                            .Select(e => e.Expense.ApprovedBy!.Value)
                            .Distinct()
                            .ToList();

                        // ✅ Load all approved by user info (full name, role, tenant) from Master DB using a NEW scope
                        Dictionary<int, (string fullName, string? role, string? tenantName)> approvedByUsersDict;
                        if (approvedByUserIds.Any())
                        {
                            using var scope = HttpContext.RequestServices.CreateScope();
                            var masterDbForTask = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
                            var users = await masterDbForTask.MasterUsers
                                .AsNoTracking()
                                .Include(u => u.UserRoles)
                                    .ThenInclude(ur => ur.Role)
                                .Include(u => u.Tenant)
                                .Where(u => approvedByUserIds.Contains(u.Id))
                                .ToListAsync();
                            
                            approvedByUsersDict = users.ToDictionary(
                                u => u.Id,
                                u =>
                                {
                                    var fullName = u.FullName ?? u.Username;
                                    var primaryRole = u.UserRoles?.FirstOrDefault()?.Role;
                                    var roleName = GetRoleDisplayName(primaryRole?.Code);
                                    var tenantName = u.Tenant?.Name;
                                    return (fullName, roleName, tenantName);
                                }
                            );
                        }
                        else
                        {
                            approvedByUsersDict = new Dictionary<int, (string, string?, string?)>();
                        }

                        // ✅ تحويل إلى DTOs
                        var tenantExpenseDtos = tenantExpenses.Select(item =>
                        {
                            var expense = item.Expense;
                            
                            // ✅ Get category name from Master DB
                            string? categoryName = null;
                            if (expense.ExpenseCategoryId.HasValue && masterCategories.TryGetValue(expense.ExpenseCategoryId.Value, out var catName))
                            {
                                categoryName = catName;
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
                            
                            var expenseRooms = item.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                            {
                                ExpenseRoomId = er.ExpenseRoomId,
                                ExpenseId = er.ExpenseId,
                                ZaaerId = er.ZaaerId,
                                Purpose = er.Purpose,
                                Amount = er.Amount,
                                CreatedAt = er.CreatedAt,
                                ApartmentId = er.Apartment?.ApartmentId,
                                ApartmentCode = er.Apartment?.ApartmentCode,
                                ApartmentName = er.Apartment?.ApartmentName
                            }).ToList();

                            return new ExpenseResponseDto
                            {
                                ExpenseId = expense.ExpenseId,
                                HotelId = expense.HotelId,
                                HotelName = item.HotelName ?? userTenant.Name,
                                HotelCode = userTenant.Code,
                                DateTime = expense.DateTime,
                                DueDate = expense.DueDate,
                                Comment = expense.Comment,
                                ExpenseCategoryId = expense.ExpenseCategoryId,
                                ExpenseCategoryName = categoryName, // ✅ From Master DB
                                TaxRate = expense.TaxRate,
                                TaxAmount = expense.TaxAmount,
                                TotalAmount = expense.TotalAmount,
                                CreatedAt = expense.CreatedAt,
                                UpdatedAt = expense.UpdatedAt,
                                ApprovalStatus = expense.ApprovalStatus,
                                ApprovedBy = expense.ApprovedBy,
                                ApprovedByFullName = approvedByFullName,
                                ApprovedAt = expense.ApprovedAt,
                                RejectionReason = expense.RejectionReason,
                                PaymentSource = expense.PaymentSource, // ✅ Add payment source
                                ExpenseRooms = expenseRooms
                            };
                        }).ToList();

                        _logger.LogInformation("✅ [GetSupervisorExpenses] Retrieved {Count} expenses from Tenant: {Code}", 
                            tenantExpenseDtos.Count, userTenant.Code);
                        
                        return tenantExpenseDtos;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [GetSupervisorExpenses] Error fetching expenses from Tenant: {Code}, Error: {Message}", 
                            userTenant.Code, ex.Message);
                        return new List<ExpenseResponseDto>(); // Return empty list on error
                    }
                });

                // ✅ Wait for all tenants to complete in parallel (Performance Optimization)
                var allTenantResults = await Task.WhenAll(tenantExpensesTasks);
                
                // ✅ Flatten results into single list
                allExpenses = allTenantResults.SelectMany(x => x).ToList();

                _logger.LogInformation("✅ [GetSupervisorExpenses] Successfully retrieved {Count} total expenses for supervisor", allExpenses.Count);

                return Ok(allExpenses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorExpenses] Error fetching supervisor expenses: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch supervisor expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على المصروفات المعلقة للموافقة للمشرف
        /// Get pending expenses for supervisor approval
        /// </summary>
        /// <returns>قائمة المصروفات المعلقة</returns>
        [HttpGet("supervisor/pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetSupervisorPendingExpenses()
        {
            try
            {
                var allExpenses = await GetSupervisorExpenses();
                if (allExpenses.Result is OkObjectResult okResult && okResult.Value is IEnumerable<ExpenseResponseDto> expenses)
                {
                    // Filter for pending expenses only (including awaiting-manager)
                    var pendingExpenses = expenses.Where(e => 
                        e.ApprovalStatus?.ToLower() == "pending" || 
                        e.ApprovalStatus?.ToLower() == "awaiting-manager"
                    ).ToList();
                    _logger.LogInformation("✅ [GetSupervisorPendingExpenses] Found {Count} pending expenses", pendingExpenses.Count);
                    return Ok(pendingExpenses);
                }
                return Ok(new List<ExpenseResponseDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetSupervisorPendingExpenses] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch pending expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على المصروفات المنتظرة للموافقة من المسؤول المالي (Officer)
        /// Get expenses pending approval from Officer (awaiting-officer status)
        /// </summary>
        /// <returns>قائمة المصروفات المنتظرة للموافقة</returns>
        [HttpGet("officer/pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetOfficerPendingExpenses()
        {
            try
            {
                var allExpenses = await GetSupervisorExpenses();
                if (allExpenses.Result is OkObjectResult okResult && okResult.Value is IEnumerable<ExpenseResponseDto> expenses)
                {
                    // Filter for awaiting-officer expenses only
                    var pendingExpenses = expenses.Where(e => 
                        e.ApprovalStatus?.ToLower() == "awaiting-officer"
                    ).ToList();
                    _logger.LogInformation("✅ [GetOfficerPendingExpenses] Found {Count} awaiting-officer expenses", pendingExpenses.Count);
                    return Ok(pendingExpenses);
                }
                return Ok(new List<ExpenseResponseDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetOfficerPendingExpenses] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch awaiting-officer expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جميع المصروفات لمسؤول الحساب الجاري (Verifier)
        /// Get all expenses for verifier
        /// </summary>
        /// <returns>قائمة جميع المصروفات</returns>
        [HttpGet("verifier/all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetVerifierExpenses()
        {
            try
            {
                // ✅ Use the same logic as GetSupervisorExpenses since Verifier has access to all hotels
                return await GetSupervisorExpenses();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetVerifierExpenses] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch verifier expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على المصروفات المعلقة للموافقة لمسؤول الحساب الجاري (Verifier)
        /// Get pending expenses for verifier approval
        /// </summary>
        /// <returns>قائمة المصروفات المعلقة</returns>
        [HttpGet("verifier/pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetVerifierPendingExpenses()
        {
            try
            {
                var allExpenses = await GetSupervisorExpenses();
                if (allExpenses.Result is OkObjectResult okResult && okResult.Value is IEnumerable<ExpenseResponseDto> expenses)
                {
                    // ✅ Filter for awaiting-verifier expenses ONLY with Categories 169, 170, or 172
                    // Verifier can ONLY approve expenses with ExpenseCategoryId = 169, 170, or 172
                    // Category 169: جاري العييري - مصروفات البيت الكبير
                    // Category 170: جاري العييري - مصروفات البيت الصغير
                    // Category 172: جاري العييري - سحب نقدي
                    var pendingExpenses = expenses.Where(e => 
                        e.ApprovalStatus?.ToLower() == "awaiting-verifier" &&
                        e.ExpenseCategoryId.HasValue &&
                        (e.ExpenseCategoryId.Value == 169 || e.ExpenseCategoryId.Value == 170 || e.ExpenseCategoryId.Value == 172)
                    ).ToList();
                    _logger.LogInformation("✅ [GetVerifierPendingExpenses] Found {Count} pending expenses (Categories 169/170/172 only)", pendingExpenses.Count);
                    return Ok(pendingExpenses);
                }
                return Ok(new List<ExpenseResponseDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetVerifierPendingExpenses] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch pending expenses", details = ex.Message });
            }
        }

        /// <summary>
        /// تحويل Role Code إلى اسم عربي للعرض
        /// Convert Role Code to Arabic display name
        /// </summary>
        /// <summary>
        /// الحصول على قائمة المستخدمين لإرسال التوصية إليهم
        /// Get list of users for sending recommendations (Supervisor, Manager, Accountant, Admin, Staff)
        /// Excludes the current user and includes supervisor for current branch
        /// </summary>
        /// <param name="hotelCode">Hotel code for the expense (optional, used to filter staff from correct tenant)</param>
        /// <returns>قائمة المستخدمين</returns>
        [HttpGet("recommendation-users")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRecommendationUsers([FromQuery] string? hotelCode = null)
        {
            try
            {
                _logger.LogInformation("👥 Getting recommendation users list (hotelCode: {HotelCode})", hotelCode);

                // Get current user ID
                int? currentUserId = null;
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj != null)
                {
                    if (int.TryParse(userIdObj.ToString(), out int parsedUserId))
                    {
                        currentUserId = parsedUserId;
                    }
                }

                // ✅ Use hotelCode from query parameter if provided, otherwise use X-Hotel-Code header or tenant service
                string? currentHotelCode = hotelCode;
                if (string.IsNullOrWhiteSpace(currentHotelCode))
                {
                    if (HttpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues) && 
                        !string.IsNullOrWhiteSpace(hotelCodeValues))
                    {
                        currentHotelCode = hotelCodeValues.ToString().Trim();
                    }
                    else
                    {
                        // Try to get from tenant service
                        try
                        {
                            var tenantService = HttpContext.RequestServices.GetRequiredService<ITenantService>();
                            var tenant = tenantService.GetTenant();
                            currentHotelCode = tenant?.Code;
                        }
                        catch
                        {
                            // Ignore if tenant service not available
                        }
                    }
                }

                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();

                // Get current tenant ID for filtering staff
                int? currentTenantId = null;
                if (!string.IsNullOrWhiteSpace(currentHotelCode))
                {
                    var currentTenant = await masterDb.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Code.ToLower() == currentHotelCode.ToLower());
                    currentTenantId = currentTenant?.Id;
                }

                // Get all users with roles: supervisor, manager, accountant, admin, staff
                var allowedRoles = new[] { "supervisor", "manager", "accountant", "admin", "staff" };

                var users = await masterDb.MasterUsers
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Include(u => u.Tenant)
                    .Where(u => u.UserRoles.Any(ur => allowedRoles.Contains(ur.Role!.Code.ToLower())))
                    .ToListAsync();

                // Format users after loading from database (to allow calling GetRoleDisplayName)
                var result = users
                    .Where(u => 
                        !currentUserId.HasValue || u.Id != currentUserId.Value) // Exclude current user
                    .Where(u =>
                    {
                        // For staff/reception staff: only show from current hotel
                        var roleCode = u.UserRoles?.FirstOrDefault()?.Role?.Code?.ToLower() ?? "";
                        if (roleCode == "staff" || roleCode == "reception staff")
                        {
                            // Only include staff from current tenant/hotel
                            return currentTenantId.HasValue && u.TenantId == currentTenantId.Value;
                        }
                        // For other roles (supervisor, manager, accountant, admin): show all
                        return true;
                    })
                    .Select(u =>
                    {
                        var primaryRole = u.UserRoles?.FirstOrDefault()?.Role;
                        var roleCode = primaryRole?.Code?.ToLower() ?? "";
                        var roleDisplayName = GetRoleDisplayName(primaryRole?.Code);
                        var fullName = u.FullName ?? u.Username;
                        var tenantName = u.Tenant?.Name;
                        
                        // Build display text based on role
                        string displayText;
                        if (roleCode == "staff" || roleCode == "reception staff")
                        {
                            // Reception Staff: show full tenant name
                            displayText = tenantName != null 
                                ? $"{fullName} ({roleDisplayName} {tenantName})" 
                                : $"{fullName} ({roleDisplayName})";
                        }
                        else if (roleCode == "supervisor")
                        {
                            // Supervisor: show first word of tenant name (without numbers)
                            if (tenantName != null)
                            {
                                var firstWord = tenantName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                    .FirstOrDefault() ?? "";
                                firstWord = System.Text.RegularExpressions.Regex.Replace(firstWord, @"\d+", "").Trim();
                                displayText = firstWord != "" 
                                    ? $"{fullName} ({roleDisplayName} {firstWord})" 
                                    : $"{fullName} ({roleDisplayName})";
                            }
                            else
                            {
                                displayText = $"{fullName} ({roleDisplayName})";
                            }
                        }
                        else
                        {
                            // Manager, Admin, Accountant: show only role, no tenant name
                            displayText = $"{fullName} ({roleDisplayName})";
                        }
                        
                        return new
                        {
                            userId = u.Id,
                            fullName = fullName,
                            username = u.Username,
                            role = roleCode,
                            roleDisplayName = roleDisplayName,
                            tenantName = tenantName,
                            displayText = displayText
                        };
                    })
                    .OrderBy(u => u.role)
                    .ThenBy(u => u.fullName)
                    .ToList();

                // Add supervisor for current branch if not already in list
                if (!string.IsNullOrWhiteSpace(currentHotelCode))
                {
                    var currentTenant = await masterDb.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Code.ToLower() == currentHotelCode.ToLower());
                    
                    if (currentTenant != null)
                    {
                        // Get supervisor for current tenant
                        var supervisor = await masterDb.MasterUsers
                            .AsNoTracking()
                            .Include(u => u.UserRoles)
                                .ThenInclude(ur => ur.Role)
                            .Include(u => u.Tenant)
                            .Where(u => u.TenantId == currentTenant.Id && 
                                       u.UserRoles.Any(ur => ur.Role!.Code.ToLower() == "supervisor") &&
                                       (!currentUserId.HasValue || u.Id != currentUserId.Value))
                            .FirstOrDefaultAsync();
                        
                        if (supervisor != null && !result.Any(r => r.userId == supervisor.Id))
                        {
                            var primaryRole = supervisor.UserRoles?.FirstOrDefault()?.Role;
                            var roleDisplayName = GetRoleDisplayName(primaryRole?.Code);
                            var fullName = supervisor.FullName ?? supervisor.Username;
                            var tenantName = supervisor.Tenant?.Name;
                            
                            // Extract first word of tenant name (without numbers) for supervisor
                            string displayText;
                            if (tenantName != null)
                            {
                                var firstWord = tenantName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                    .FirstOrDefault() ?? "";
                                firstWord = System.Text.RegularExpressions.Regex.Replace(firstWord, @"\d+", "").Trim();
                                displayText = firstWord != "" 
                                    ? $"{fullName} ({roleDisplayName} {firstWord})" 
                                    : $"{fullName} ({roleDisplayName})";
                            }
                            else
                            {
                                displayText = $"{fullName} ({roleDisplayName})";
                            }
                            
                            result.Add(new
                            {
                                userId = supervisor.Id,
                                fullName = fullName,
                                username = supervisor.Username,
                                role = "supervisor",
                                roleDisplayName = roleDisplayName,
                                tenantName = tenantName,
                                displayText = displayText
                            });
                            
                            // Re-sort after adding supervisor
                            result = result
                                .OrderBy(u => u.role)
                                .ThenBy(u => u.fullName)
                                .ToList();
                        }
                    }
                }

                _logger.LogInformation("✅ Found {Count} users for recommendations (excluding current user ID: {CurrentUserId})", 
                    result.Count, currentUserId);

                return Ok(new { users = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting recommendation users: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to get recommendation users", details = ex.Message });
            }
        }

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
                "officer" => "مسؤول المشتريات",
                "owner" => "المالك",
                "verifier" => "مسؤول الحساب الجاري",
                _ => roleCode
            };
        }

        // ==================== Analytics Endpoints ====================

        /// <summary>
        /// Helper method to get all expenses from accessible tenants (reusable for analytics)
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <param name="hotelIds">قائمة معرفات الفنادق للتصفية (اختياري، null أو فارغ يعني جميع الفنادق)</param>
        private async Task<List<ExpenseResponseDto>> GetAllExpensesForAnalytics(DateTime? startDate = null, DateTime? endDate = null, int[]? hotelIds = null)
        {
            // ✅ Extract user information from JWT Token
            var userIdClaim = HttpContext.Items["UserId"]?.ToString();
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("⚠️ [GetAllExpensesForAnalytics] UserId not found in JWT token");
                return new List<ExpenseResponseDto>();
            }

            // ✅ Normalize dates to Saudi timezone for proper filtering
            // Assume incoming dates are in Saudi timezone (from frontend)
            DateTime? normalizedStartDate = null;
            DateTime? normalizedEndDate = null;
            
            if (startDate.HasValue)
            {
                // Normalize to start of day in Saudi timezone
                normalizedStartDate = startDate.Value.Date;
                _logger.LogInformation("📅 [GetAllExpensesForAnalytics] Normalized StartDate: {OriginalDate} -> {NormalizedDate}", 
                    startDate.Value, normalizedStartDate.Value);
            }
            
            if (endDate.HasValue)
            {
                // Normalize to end of day in Saudi timezone (23:59:59.999)
                normalizedEndDate = endDate.Value.Date.AddDays(1).AddTicks(-1);
                _logger.LogInformation("📅 [GetAllExpensesForAnalytics] Normalized EndDate: {OriginalDate} -> {NormalizedDate}", 
                    endDate.Value, normalizedEndDate.Value);
            }

            _logger.LogInformation("📋 [GetAllExpensesForAnalytics] Fetching expenses for analytics, UserId: {UserId}, StartDate: {StartDate}, EndDate: {EndDate}, NormalizedStart: {NormalizedStart}, NormalizedEnd: {NormalizedEnd}", 
                userId, startDate, endDate, normalizedStartDate, normalizedEndDate);

            // ✅ Get accessible tenants (same logic as GetSupervisorExpenses)
            var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
            
            var roleCsv = HttpContext.Items["Roles"]?.ToString() ?? string.Empty;
            var rolesList = roleCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .Select(r => r.Trim().ToLower())
                                   .Where(r => !string.IsNullOrWhiteSpace(r))
                                   .ToList();
            
            if (!rolesList.Any())
            {
                var dbRoles = await masterDb.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role!.Code)
                    .ToListAsync();
                
                rolesList = dbRoles.Where(r => !string.IsNullOrWhiteSpace(r))
                                  .Select(r => r.Trim().ToLower())
                                  .ToList();
            }
            
            var isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier = rolesList.Contains("manager") || 
                                                               rolesList.Contains("admin") || 
                                                               rolesList.Contains("accountant") || 
                                                               rolesList.Contains("officer") || 
                                                               rolesList.Contains("owner") ||
                                                               rolesList.Contains("verifier");

            var userTenants = await masterDb.UserTenants
                .AsNoTracking()
                .Include(ut => ut.Tenant)
                .Where(ut => ut.UserId == userId)
                .Select(ut => new { ut.TenantId, ut.Tenant!.Code, ut.Tenant.DatabaseName, ut.Tenant.Name })
                .ToListAsync();

            if (isManagerOrAdminOrAccountantOrOfficerOrOwnerOrVerifier)
            {
                userTenants = await masterDb.Tenants
                    .AsNoTracking()
                    .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                    .ToListAsync();
            }
            else if (!userTenants.Any())
            {
                userTenants = await masterDb.Tenants
                    .AsNoTracking()
                    .Select(t => new { TenantId = t.Id, Code = t.Code, DatabaseName = t.DatabaseName, Name = t.Name })
                    .ToListAsync();
            }

            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var server = configuration["TenantDatabase:Server"]?.Trim();
            var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
            var password = configuration["TenantDatabase:Password"]?.Trim();
            
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("❌ [GetAllExpensesForAnalytics] TenantDatabase settings not found");
                return new List<ExpenseResponseDto>();
            }

            // ✅ Fetch expenses from all tenants in parallel
            // Capture normalized dates in local variables for use in parallel tasks
            var startDateFilter = normalizedStartDate;
            var endDateFilter = normalizedEndDate;
            
            var tenantExpensesTasks = userTenants.Select(async userTenant =>
            {
                try
                {
                    var connectionString = $"Server={server}; Database={userTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlServer(connectionString);
                    await using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                    // ✅ FIX: Load expenses WITHOUT Include HotelSettings because FK relationship is broken
                    var query = tenantContext.Expenses
                        .AsNoTracking()
                        .Include(e => e.ExpenseRooms)
                            .ThenInclude(er => er.Apartment)
                        .AsQueryable();

                    // ✅ Apply date filter if provided (use normalized dates for proper comparison)
                    if (startDateFilter.HasValue)
                    {
                        var filterStart = startDateFilter.Value;
                        query = query.Where(e => e.DateTime >= filterStart);
                        _logger.LogInformation("🔍 [GetAllExpensesForAnalytics] Applied start date filter: >= {StartDate} for tenant {TenantCode}", 
                            filterStart, userTenant.Code);
                    }
                    if (endDateFilter.HasValue)
                    {
                        var filterEnd = endDateFilter.Value;
                        query = query.Where(e => e.DateTime <= filterEnd);
                        _logger.LogInformation("🔍 [GetAllExpensesForAnalytics] Applied end date filter: <= {EndDate} for tenant {TenantCode}", 
                            filterEnd, userTenant.Code);
                    }
                    
                    // Note: Hotel filter is applied after collecting all expenses from all tenants
                    // to allow filtering across different tenant databases
                    
                    // ✅ Load expenses first
                    var expenses = await query
                        .OrderByDescending(e => e.DateTime)
                        .ToListAsync();
                    
                    // ✅ Load HotelSettings separately (by HotelCode, not by FK)
                    var hotelSettingsForTenant = await tenantContext.HotelSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(h => h.HotelCode != null && h.HotelCode.ToLower() == userTenant.Code.ToLower());
                    var hotelName = hotelSettingsForTenant?.HotelName ?? userTenant.Name;
                    
                    // ✅ Project in memory (after loading from DB)
                    var tenantExpenses = expenses.Select(e => new
                    {
                        Expense = e,
                        HotelName = hotelName,
                        HotelCode = userTenant.Code,
                        ExpenseRooms = e.ExpenseRooms.Select(er => new
                        {
                            ExpenseRoomId = er.ExpenseRoomId,
                            ExpenseId = er.ExpenseId,
                            ZaaerId = er.ZaaerId,
                            Purpose = er.Purpose,
                            Amount = er.Amount,
                            CreatedAt = er.CreatedAt,
                            Apartment = er.Apartment != null ? new
                            {
                                ApartmentId = er.Apartment.ApartmentId,
                                ApartmentCode = er.Apartment.ApartmentCode,
                                ApartmentName = er.Apartment.ApartmentName
                            } : null
                        }).ToList()
                    }).ToList();
                    
                    // ✅ Log expenses found for debugging
                    _logger.LogInformation("📊 [GetAllExpensesForAnalytics] Found {Count} expenses from tenant {TenantCode} ({TenantName})", 
                        tenantExpenses.Count, userTenant.Code, userTenant.Name);
                    if (tenantExpenses.Any())
                    {
                        var sampleExpenses = tenantExpenses.Take(5).Select(e => new { 
                            Id = e.Expense.ExpenseId, 
                            DateTime = e.Expense.DateTime, 
                            Hotel = e.HotelName 
                        }).ToList();
                        _logger.LogInformation("📊 [GetAllExpensesForAnalytics] Sample expenses from {TenantCode}: {Expenses}", 
                            userTenant.Code, string.Join(", ", sampleExpenses.Select(e => $"ID={e.Id}, Date={e.DateTime:yyyy-MM-dd HH:mm:ss}, Hotel={e.Hotel}")));
                    }

                    // ✅ Get category names from Master DB
                    var categoryIds = tenantExpenses
                        .Where(e => e.Expense.ExpenseCategoryId.HasValue)
                        .Select(e => e.Expense.ExpenseCategoryId!.Value)
                        .Distinct()
                        .ToList();

                    Dictionary<int, string> masterCategories = new();
                    if (categoryIds.Any())
                    {
                        using var scope = HttpContext.RequestServices.CreateScope();
                        var masterDbForTask = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
                        masterCategories = await masterDbForTask.ExpenseCategories
                            .AsNoTracking()
                            .Where(ec => categoryIds.Contains(ec.Id))
                            .ToDictionaryAsync(ec => ec.Id, ec => ec.MainCategory);
                    }

                    // ✅ Convert to DTOs
                    return tenantExpenses.Select(item =>
                    {
                        var expense = item.Expense;
                        string? categoryName = null;
                        if (expense.ExpenseCategoryId.HasValue && masterCategories.TryGetValue(expense.ExpenseCategoryId.Value, out var catName))
                        {
                            categoryName = catName;
                        }

                        var expenseRooms = item.ExpenseRooms.Select(er => new ExpenseRoomResponseDto
                        {
                            ExpenseRoomId = er.ExpenseRoomId,
                            ExpenseId = er.ExpenseId,
                            ZaaerId = er.ZaaerId,
                            Purpose = er.Purpose,
                            Amount = er.Amount,
                            CreatedAt = er.CreatedAt,
                            ApartmentId = er.Apartment?.ApartmentId,
                            ApartmentCode = er.Apartment?.ApartmentCode,
                            ApartmentName = er.Apartment?.ApartmentName
                        }).ToList();

                        return new ExpenseResponseDto
                        {
                            ExpenseId = expense.ExpenseId,
                            HotelId = expense.HotelId,
                            HotelName = item.HotelName ?? userTenant.Name,
                            HotelCode = userTenant.Code,
                            DateTime = expense.DateTime,
                            DueDate = expense.DueDate,
                            Comment = expense.Comment,
                            ExpenseCategoryId = expense.ExpenseCategoryId,
                            ExpenseCategoryName = categoryName,
                            TaxRate = expense.TaxRate,
                            TaxAmount = expense.TaxAmount,
                            TotalAmount = expense.TotalAmount,
                            CreatedAt = expense.CreatedAt,
                            UpdatedAt = expense.UpdatedAt,
                            ApprovalStatus = expense.ApprovalStatus,
                            ApprovedBy = expense.ApprovedBy,
                            ApprovedAt = expense.ApprovedAt,
                            RejectionReason = expense.RejectionReason,
                            PaymentSource = expense.PaymentSource,
                            ExpenseRooms = expenseRooms
                        };
                    }).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [GetAllExpensesForAnalytics] Error fetching expenses from Tenant: {Code}", userTenant.Code);
                    return new List<ExpenseResponseDto>();
                }
            });

            var allTenantResults = await Task.WhenAll(tenantExpensesTasks);
            var allExpenses = allTenantResults.SelectMany(x => x).ToList();
            
            // ✅ Apply hotel filter after collecting all expenses from all tenants
            // Note: hotelIds parameter contains Tenant.Id from Master DB
            // ExpenseResponseDto already includes HotelCode (Tenant.Code) which we set during DTO creation
            if (hotelIds != null && hotelIds.Length > 0 && hotelIds.Any(id => id > 0))
            {
                // Filter out -1 (all hotels indicator)
                var validTenantIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                if (validTenantIds.Length > 0)
                {
                    // ✅ Get Tenant Codes from Master DB
                    var selectedTenants = await masterDb.Tenants
                        .AsNoTracking()
                        .Where(t => validTenantIds.Contains(t.Id))
                        .Select(t => new { t.Id, t.Code })
                        .ToListAsync();
                    
                    var selectedTenantCodes = selectedTenants
                        .Where(t => !string.IsNullOrWhiteSpace(t.Code))
                        .Select(t => t.Code!.ToLower())
                        .Distinct()
                        .ToList();
                    
                    if (selectedTenantCodes.Any())
                    {
                        // ✅ Filter expenses by HotelCode (Tenant.Code) which is already in ExpenseResponseDto
                        var originalCount = allExpenses.Count;
                        allExpenses = allExpenses.Where(e => 
                            !string.IsNullOrWhiteSpace(e.HotelCode) && 
                            selectedTenantCodes.Contains(e.HotelCode.ToLower())
                        ).ToList();
                        
                        _logger.LogInformation("✅ [GetAllExpensesForAnalytics] Filtered by TenantIds: [{TenantIds}] (Codes: [{Codes}]), Expenses: {OriginalCount} -> {FilteredCount}", 
                            string.Join(", ", validTenantIds), string.Join(", ", selectedTenantCodes), originalCount, allExpenses.Count);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [GetAllExpensesForAnalytics] No tenant codes found for TenantIds: [{TenantIds}], showing all expenses", 
                            string.Join(", ", validTenantIds));
                    }
                }
            }

            _logger.LogInformation("✅ [GetAllExpensesForAnalytics] Retrieved {Count} total expenses for analytics", allExpenses.Count);
            return allExpenses;
        }

        /// <summary>
        /// الحصول على مؤشرات الأداء الرئيسية (KPIs) للتحليلات
        /// Get Key Performance Indicators (KPIs) for analytics
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <returns>مؤشرات الأداء الرئيسية</returns>
        [HttpGet("analytics/kpis")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExpenseAnalyticsKpiDto>> GetAnalyticsKPIs([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetAnalyticsKPIs] Fetching KPIs, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                var totalAmount = allExpenses.Sum(e => e.TotalAmount);
                var expenseCount = allExpenses.Count;
                
                // ✅ حساب المتوسط: إجمالي المصروفات ÷ عدد الأيام في الفترة المحددة
                // Average calculation: Total Expenses ÷ Number of Days in Selected Period
                decimal averageAmount = 0;
                int daysCount = 0;
                
                if (startDate.HasValue && endDate.HasValue)
                {
                    // ✅ إذا كانت الفترة محددة، استخدم عدد الأيام الفعلية في تلك الفترة
                    // ✅ استخدام Date فقط (بدون وقت) لضمان الحساب الصحيح
                    var startDateOnly = startDate.Value.Date;
                    var endDateOnly = endDate.Value.Date;
                    
                    // ✅ حساب عدد الأيام: الفرق بين التواريخ + 1 ليشمل اليوم الأول واليوم الأخير
                    // مثال: من 1 نوفمبر إلى 30 نوفمبر = (30 - 1) + 1 = 30 يوم
                    daysCount = Math.Max(1, (endDateOnly - startDateOnly).Days + 1);
                    averageAmount = daysCount > 0 ? totalAmount / daysCount : 0;
                    
                    _logger.LogInformation("📊 [GetAnalyticsKPIs] Average calculation (based on period): Total={Total}, DaysInPeriod={Days}, StartDate={StartDate}, EndDate={EndDate}, Average={Avg}, Calculation={Total}/{Days}={Avg}", 
                        totalAmount, daysCount, startDateOnly, endDateOnly, averageAmount, totalAmount, daysCount, averageAmount);
                }
                else if (startDate.HasValue && !endDate.HasValue)
                {
                    // ✅ إذا كان startDate فقط، استخدم من startDate حتى اليوم الحالي (Saudi time)
                    var today = KsaTime.Now.Date;
                    daysCount = (today - startDate.Value.Date).Days + 1;
                    averageAmount = daysCount > 0 ? totalAmount / daysCount : 0;
                    _logger.LogInformation("📊 [GetAnalyticsKPIs] Average calculation (from startDate to today): Total={Total}, DaysInPeriod={Days}, StartDate={StartDate}, EndDate={EndDate}, Average={Avg}", 
                        totalAmount, daysCount, startDate.Value.Date, today, averageAmount);
                }
                else if (!startDate.HasValue && endDate.HasValue)
                {
                    // ✅ إذا كان endDate فقط، استخدم من أول الشهر حتى endDate
                    var monthStart = new DateTime(endDate.Value.Year, endDate.Value.Month, 1);
                    daysCount = (endDate.Value.Date - monthStart.Date).Days + 1;
                    averageAmount = daysCount > 0 ? totalAmount / daysCount : 0;
                    _logger.LogInformation("📊 [GetAnalyticsKPIs] Average calculation (from month start to endDate): Total={Total}, DaysInPeriod={Days}, StartDate={StartDate}, EndDate={EndDate}, Average={Avg}", 
                        totalAmount, daysCount, monthStart, endDate.Value.Date, averageAmount);
                }
                else
                {
                    // ✅ إذا لم تكن الفترة محددة، استخدم عدد أيام الشهر الحالي الكامل (Saudi time)
                    var now = KsaTime.Now;
                    var currentMonthStart = new DateTime(now.Year, now.Month, 1);
                    var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
                    daysCount = currentMonthEnd.Day; // عدد أيام الشهر الحالي الكامل (30 لشهر نوفمبر)
                    averageAmount = daysCount > 0 ? totalAmount / daysCount : 0;
                    _logger.LogInformation("📊 [GetAnalyticsKPIs] Average calculation (current month full): Total={Total}, DaysInCurrentMonth={Days}, Month={Month}, Average={Avg}", 
                        totalAmount, daysCount, now.Month, averageAmount);
                }
                
                var uniqueHotels = allExpenses
                    .Where(e => !string.IsNullOrWhiteSpace(e.HotelName))
                    .Select(e => e.HotelName)
                    .Distinct()
                    .Count();

                var kpis = new ExpenseAnalyticsKpiDto
                {
                    TotalAmount = totalAmount,
                    ExpenseCount = expenseCount,
                    AverageAmount = averageAmount,
                    HotelCount = uniqueHotels
                };

                _logger.LogInformation("✅ [GetAnalyticsKPIs] KPIs calculated: Total={Total}, Count={Count}, Avg={Avg}, Hotels={Hotels}", 
                    totalAmount, expenseCount, averageAmount, uniqueHotels);

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetAnalyticsKPIs] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch analytics KPIs", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على أعلى الفنادق صرفت مصروفات
        /// Get top hotels by expenses amount
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <param name="top">عدد الفنادق المطلوبة (افتراضي: 10)</param>
        /// <returns>قائمة أعلى الفنادق</returns>
        [HttpGet("analytics/top-hotels")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsTopHotelDto>>> GetTopHotels(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int top = 10,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetTopHotels] Fetching top {Top} hotels, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    top, startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                // Group by hotel
                var hotelStats = allExpenses
                    .Where(e => !string.IsNullOrWhiteSpace(e.HotelName))
                    .GroupBy(e => new { e.HotelName, e.HotelCode })
                    .Select(g => new
                    {
                        HotelName = g.Key.HotelName ?? "غير محدد",
                        HotelCode = g.Key.HotelCode,
                        Amount = g.Sum(e => e.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .Take(top)
                    .ToList();

                var totalAmount = hotelStats.Sum(h => h.Amount);

                var result = hotelStats.Select((item, index) => new ExpenseAnalyticsTopHotelDto
                {
                    HotelName = item.HotelName,
                    HotelCode = item.HotelCode,
                    Amount = item.Amount,
                    Count = item.Count,
                    Percentage = totalAmount > 0 ? (item.Amount / totalAmount) * 100 : 0
                }).ToList();

                _logger.LogInformation("✅ [GetTopHotels] Found {Count} top hotels", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetTopHotels] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch top hotels", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على أعلى أنواع المصروفات
        /// Get top expense categories
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <param name="top">عدد الأنواع المطلوبة (افتراضي: 10)</param>
        /// <returns>قائمة أعلى أنواع المصروفات</returns>
        [HttpGet("analytics/top-categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsTopCategoryDto>>> GetTopCategories(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int top = 10,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetTopCategories] Fetching top {Top} categories, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    top, startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                // Group by category
                var categoryStats = allExpenses
                    .Where(e => !string.IsNullOrWhiteSpace(e.ExpenseCategoryName))
                    .GroupBy(e => e.ExpenseCategoryName)
                    .Select(g => new
                    {
                        CategoryName = g.Key ?? "غير محدد",
                        Amount = g.Sum(e => e.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .Take(top)
                    .ToList();

                var result = categoryStats.Select(item => new ExpenseAnalyticsTopCategoryDto
                {
                    CategoryName = item.CategoryName,
                    Amount = item.Amount,
                    Count = item.Count
                }).ToList();

                _logger.LogInformation("✅ [GetTopCategories] Found {Count} top categories", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetTopCategories] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch top categories", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على اتجاهات المصروفات (grouped by date)
        /// Get expense trends grouped by date
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <returns>قائمة اتجاهات المصروفات</returns>
        [HttpGet("analytics/trends")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsTrendDto>>> GetExpenseTrends(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetExpenseTrends] Fetching trends, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                // Group by date (date only, no time)
                var trendData = allExpenses
                    .GroupBy(e => e.DateTime.Date)
                    .Select(g => new ExpenseAnalyticsTrendDto
                    {
                        Date = g.Key,
                        Amount = g.Sum(e => e.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                _logger.LogInformation("✅ [GetExpenseTrends] Found {Count} trend data points", trendData.Count);
                return Ok(trendData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetExpenseTrends] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch expense trends", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على توزيع حالات المصروفات
        /// Get expense status distribution
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <returns>قائمة توزيع الحالات</returns>
        [HttpGet("analytics/status-distribution")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsStatusDistributionDto>>> GetStatusDistribution(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetStatusDistribution] Fetching status distribution, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                // Status labels mapping
                var statusLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "pending", "في انتظار المشرف" },
                    { "accepted", "مقبول" },
                    { "rejected", "مرفوض" },
                    { "awaiting-manager", "في انتظار مدير العمليات" },
                    { "awaiting-accountant", "في انتظار المحاسب" },
                    { "awaiting-admin", "في انتظار المدير العام" },
                    { "awaiting-officer", "في انتظار مسؤول المشتريات" },
                    { "auto-approved", "مقبول تلقائياً" }
                };

                // Group by status
                var statusData = allExpenses
                    .GroupBy(e => e.ApprovalStatus ?? "غير محدد")
                    .Select(g => new ExpenseAnalyticsStatusDistributionDto
                    {
                        Status = g.Key,
                        StatusName = statusLabels.TryGetValue(g.Key, out var label) ? label : g.Key,
                        Count = g.Count(),
                        Amount = g.Sum(e => e.TotalAmount)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                _logger.LogInformation("✅ [GetStatusDistribution] Found {Count} status groups", statusData.Count);
                return Ok(statusData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetStatusDistribution] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch status distribution", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على جدول تفاصيل مصروفات الفنادق
        /// Get hotels expenses details table
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <returns>جدول تفاصيل مصروفات الفنادق</returns>
        [HttpGet("analytics/hotels-table")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsHotelTableDto>>> GetHotelsTable(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetHotelsTable] Fetching hotels table, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);

                // Group by hotel
                var hotelStats = allExpenses
                    .Where(e => !string.IsNullOrWhiteSpace(e.HotelName))
                    .GroupBy(e => new { e.HotelName, e.HotelCode })
                    .Select(g => new
                    {
                        HotelName = g.Key.HotelName ?? "غير محدد",
                        HotelCode = g.Key.HotelCode,
                        Amount = g.Sum(e => e.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                var result = hotelStats.Select((item, index) => new ExpenseAnalyticsHotelTableDto
                {
                    Rank = index + 1,
                    HotelName = item.HotelName,
                    HotelCode = item.HotelCode,
                    Count = item.Count,
                    Amount = item.Amount,
                    Average = item.Count > 0 ? item.Amount / item.Count : 0
                }).ToList();

                _logger.LogInformation("✅ [GetHotelsTable] Found {Count} hotels", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetHotelsTable] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch hotels table", details = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على إحصائيات الأدوار (عدد الطلبات المقبولة وفي الانتظار لكل دور)
        /// Get role statistics (accepted and pending requests count per role)
        /// </summary>
        /// <param name="startDate">تاريخ البداية (اختياري)</param>
        /// <param name="endDate">تاريخ النهاية (اختياري)</param>
        /// <param name="hotelIds">مصفوفة معرفات الفنادق (اختياري)</param>
        /// <returns>إحصائيات الأدوار</returns>
        [HttpGet("analytics/role-statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ExpenseAnalyticsRoleStatisticsDto>>> GetRoleStatistics(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int[]? hotelIds = null)
        {
            try
            {
                // ✅ Filter out -1 (all hotels indicator) and null/empty arrays
                int[]? validHotelIds = null;
                if (hotelIds != null && hotelIds.Length > 0)
                {
                    validHotelIds = hotelIds.Where(id => id > 0).Distinct().ToArray();
                    if (validHotelIds.Length == 0)
                    {
                        validHotelIds = null; // Treat as "all hotels"
                    }
                }
                
                _logger.LogInformation("📊 [GetRoleStatistics] Fetching role statistics, StartDate: {StartDate}, EndDate: {EndDate}, HotelIds: [{HotelIds}]", 
                    startDate, endDate, validHotelIds != null ? string.Join(", ", validHotelIds) : "All");

                var allExpenses = await GetAllExpensesForAnalytics(startDate, endDate, validHotelIds);
                
                _logger.LogInformation("📊 [GetRoleStatistics] Total expenses retrieved: {Count}", allExpenses.Count);
                if (allExpenses.Any())
                {
                    var statusGroups = allExpenses
                        .Where(e => !string.IsNullOrWhiteSpace(e.ApprovalStatus))
                        .GroupBy(e => e.ApprovalStatus)
                        .Select(g => $"{g.Key}: {g.Count()}")
                        .ToList();
                    _logger.LogInformation("📊 [GetRoleStatistics] Expense statuses breakdown: {Statuses}", 
                        string.Join(", ", statusGroups));
                    
                    // Log hotel distribution
                    var hotelGroups = allExpenses
                        .Where(e => !string.IsNullOrWhiteSpace(e.HotelCode))
                        .GroupBy(e => e.HotelCode)
                        .Select(g => $"{g.Key}: {g.Count()}")
                        .ToList();
                    _logger.LogInformation("📊 [GetRoleStatistics] Hotels distribution: {Hotels}", 
                        string.Join(", ", hotelGroups));
                }

                // ✅ Get Master DB context to fetch user and tenant information
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();

                // Role mapping based on approval status (for pending expenses)
                var roleStatusMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "pending", "supervisor" },
                    { "awaiting-manager", "manager" },
                    { "awaiting-accountant", "accountant" },
                    { "awaiting-admin", "admin" },
                    { "awaiting-officer", "officer" },
                    { "awaiting-verifier", "verifier" }
                };

                // Role display names (without tenant name)
                var roleDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "supervisor", "مشرف فرع" },
                    { "manager", "مدير العمليات" },
                    { "accountant", "المحاسب" },
                    { "admin", "المدير العام" },
                    { "officer", "المشتريات" },
                    { "verifier", "مسؤول الجاري" }
                };

                // ✅ Role order priority (for sorting): admin first, then accountant, then others
                var roleOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "admin", 1 },
                    { "accountant", 2 },
                    { "manager", 3 },
                    { "supervisor", 4 },
                    { "officer", 5 },
                    { "verifier", 6 }
                };

                // ✅ Dictionary to store statistics by Role ONLY (aggregated across all tenants/users)
                // Key format: just the role name (e.g., "supervisor", "manager", etc.)
                var resultDict = new Dictionary<string, ExpenseAnalyticsRoleStatisticsDto>(StringComparer.OrdinalIgnoreCase);

                // ✅ Get all unique approved_by user IDs to fetch their roles
                var approvedByUserIds = allExpenses
                    .Where(e => e.ApprovedBy.HasValue && e.ApprovedBy.Value > 0)
                    .Select(e => e.ApprovedBy!.Value)
                    .Distinct()
                    .ToList();

                // ✅ Fetch user role information from Master DB (simplified - only need role codes)
                var approvedByUserRoles = new Dictionary<int, string>();
                if (approvedByUserIds.Any())
                {
                    var userRoles = await masterDb.UserRoles
                        .AsNoTracking()
                        .Include(ur => ur.Role)
                        .Where(ur => approvedByUserIds.Contains(ur.UserId))
                        .GroupBy(ur => ur.UserId)
                        .Select(g => new { UserId = g.Key, RoleCode = g.First().Role!.Code })
                        .ToListAsync();

                    foreach (var ur in userRoles)
                    {
                        if (!string.IsNullOrWhiteSpace(ur.RoleCode))
                        {
                            approvedByUserRoles[ur.UserId] = ur.RoleCode.ToLower();
                        }
                    }
                }

                // ✅ Process all expenses - aggregate by role only
                foreach (var expense in allExpenses.Where(e => !string.IsNullOrWhiteSpace(e.ApprovalStatus)))
                {
                    var status = expense.ApprovalStatus ?? "";

                    // ✅ Handle pending expenses (waiting for approval)
                    if (roleStatusMapping.TryGetValue(status, out var pendingRole))
                    {
                        // Use role name as key (no tenant/user distinction)
                        var roleKey = pendingRole.ToLower();
                        
                        if (!resultDict.ContainsKey(roleKey))
                        {
                            var displayName = roleDisplayNames.TryGetValue(pendingRole, out var name) ? name : pendingRole;
                            resultDict[roleKey] = new ExpenseAnalyticsRoleStatisticsDto
                            {
                                RoleName = pendingRole,
                                RoleDisplayName = displayName,
                                TenantName = null, // No tenant name for aggregated view
                                AcceptedCount = 0,
                                PendingCount = 0,
                                TotalCount = 0,
                                AcceptedAmount = 0,
                                PendingAmount = 0
                            };
                        }
                        resultDict[roleKey].PendingCount++;
                        resultDict[roleKey].PendingAmount += expense.TotalAmount;
                        resultDict[roleKey].TotalCount++;
                    }
                    // ✅ Handle accepted/auto-approved expenses (count for the role that approved them)
                    else if (status.Equals("accepted", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("auto-approved", StringComparison.OrdinalIgnoreCase))
                    {
                        // Determine the role that approved this expense
                        string? approvedRoleCode = null;

                        if (!string.IsNullOrWhiteSpace(expense.ApprovedByRole))
                        {
                            // Use role from expense DTO
                            approvedRoleCode = expense.ApprovedByRole.ToLower();
                        }
                        // ✅ Fallback: Get role from approved_by user ID
                        else if (expense.ApprovedBy.HasValue && approvedByUserRoles.TryGetValue(expense.ApprovedBy.Value, out var roleCode))
                        {
                            approvedRoleCode = roleCode;
                        }

                        if (!string.IsNullOrWhiteSpace(approvedRoleCode))
                        {
                            // Map role code to our role keys
                            string roleKey = approvedRoleCode switch
                            {
                                "supervisor" or "مشرف فرع" => "supervisor",
                                "manager" or "مدير العمليات" => "manager",
                                "accountant" or "المحاسب" => "accountant",
                                "admin" or "administrator" or "المدير العام" => "admin",
                                "officer" or "المشتريات" => "officer",
                                "verifier" or "مسؤول الجاري" or "مسؤول الحساب الجاري" => "verifier",
                                _ => approvedRoleCode // Keep original if not in mapping
                            };

                            // Use role name as key (no tenant/user distinction)
                            var normalizedRoleKey = roleKey.ToLower();
                            
                            if (!resultDict.ContainsKey(normalizedRoleKey))
                            {
                                var displayName = roleDisplayNames.TryGetValue(roleKey, out var name) ? name : approvedRoleCode;
                                resultDict[normalizedRoleKey] = new ExpenseAnalyticsRoleStatisticsDto
                                {
                                    RoleName = roleKey,
                                    RoleDisplayName = displayName,
                                    TenantName = null, // No tenant name for aggregated view
                                    AcceptedCount = 0,
                                    PendingCount = 0,
                                    TotalCount = 0,
                                    AcceptedAmount = 0,
                                    PendingAmount = 0
                                };
                            }
                            resultDict[normalizedRoleKey].AcceptedCount++;
                            resultDict[normalizedRoleKey].AcceptedAmount += expense.TotalAmount;
                            resultDict[normalizedRoleKey].TotalCount++;
                        }
                    }
                }

                // ✅ Convert to list and sort by role order only (admin first, then accountant, then others)
                var resultList = resultDict.Values
                    .OrderBy(r => roleOrder.TryGetValue(r.RoleName, out var order) ? order : 999) // Sort by role order only
                    .ToList();

                _logger.LogInformation("✅ [GetRoleStatistics] Returning {Count} role entries (aggregated by role)", resultList.Count);
                foreach (var role in resultList)
                {
                    _logger.LogInformation("   - {RoleDisplayName}: {PendingCount} pending (SAR {PendingAmount}), {AcceptedCount} accepted (SAR {AcceptedAmount}), {TotalCount} total", 
                        role.RoleDisplayName, role.PendingCount, role.PendingAmount, role.AcceptedCount, role.AcceptedAmount, role.TotalCount);
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetRoleStatistics] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch role statistics", details = ex.Message });
            }
        }

        /// <summary>
        /// إرسال توصية/رسالة للمستخدمين حسب الدور
        /// Send recommendation/message to users based on role
        /// </summary>
        /// <param name="request">بيانات التوصية (role, message, userId)</param>
        /// <returns>نتيجة العملية</returns>
        [HttpPost("recommendations")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendRecommendation([FromBody] SendRecommendationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Role and message are required" });
                }

                // ✅ Get current user ID
                var userIdClaim = HttpContext.Items["UserId"]?.ToString();
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                // ✅ Get current user info from Master DB
                var masterDb = HttpContext.RequestServices.GetRequiredService<MasterDbContext>();
                var currentUser = await masterDb.MasterUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == currentUserId);

                if (currentUser == null)
                {
                    return Unauthorized(new { error = "User not found" });
                }

                var currentUserFullName = currentUser.FullName ?? currentUser.Username ?? "Unknown";

                _logger.LogInformation("📨 [SendRecommendation] Sending recommendation - Role: {Role}, UserId: {UserId}, CurrentUser: {CurrentUserFullName}", 
                    request.Role, request.UserId, currentUserFullName);

                // ✅ Map role to approval status
                var roleStatusMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "supervisor", "pending" },
                    { "manager", "awaiting-manager" },
                    { "accountant", "awaiting-accountant" },
                    { "admin", "awaiting-admin" },
                    { "officer", "awaiting-officer" },
                    { "verifier", "awaiting-verifier" }
                };

                if (!roleStatusMapping.TryGetValue(request.Role.ToLower(), out var targetStatus))
                {
                    return BadRequest(new { error = $"Invalid role: {request.Role}" });
                }

                // ✅ Get all tenants (same logic as GetAllExpensesForAnalytics)
                var userTenants = await masterDb.Tenants
                    .AsNoTracking()
                    .Select(t => new { t.Id, t.Code, t.DatabaseName, t.Name })
                    .ToListAsync();

                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var server = configuration["TenantDatabase:Server"]?.Trim();
                var dbUserId = configuration["TenantDatabase:UserId"]?.Trim();
                var password = configuration["TenantDatabase:Password"]?.Trim();

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(dbUserId) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("❌ [SendRecommendation] TenantDatabase settings not found");
                    return StatusCode(500, new { error = "Database configuration error" });
                }

                // ✅ Find target users (if userId is provided, send to that user only; otherwise, send to all users with this role)
                List<int> targetUserIds = new List<int>();
                List<int>? targetUserTenantIds = null; // ✅ Track target user's tenant IDs if specific user is targeted
                
                if (request.UserId.HasValue && request.UserId.Value > 0)
                {
                    // ✅ Send to specific user only - get their tenant IDs
                    targetUserIds.Add(request.UserId.Value);
                    
                    // ✅ Get all tenants this user has access to
                    targetUserTenantIds = await masterDb.UserTenants
                        .AsNoTracking()
                        .Where(ut => ut.UserId == request.UserId.Value)
                        .Select(ut => ut.TenantId)
                        .Distinct()
                        .ToListAsync();
                    
                    _logger.LogInformation("✅ [SendRecommendation] Targeting specific user: UserId={UserId}, TenantIds=[{TenantIds}]", 
                        request.UserId.Value, targetUserTenantIds != null && targetUserTenantIds.Any() ? string.Join(", ", targetUserTenantIds) : "None");
                }
                else
                {
                    // Send to all users with this role
                    var roleCode = request.Role.ToLower();
                    var usersWithRole = await masterDb.UserRoles
                        .AsNoTracking()
                        .Include(ur => ur.Role)
                        .Where(ur => ur.Role!.Code.ToLower() == roleCode)
                        .Select(ur => ur.UserId)
                        .Distinct()
                        .ToListAsync();
                    targetUserIds.AddRange(usersWithRole);
                    
                    _logger.LogInformation("✅ [SendRecommendation] Targeting all users with role: {Role}, Count={Count}", 
                        request.Role, targetUserIds.Count);
                }

                if (!targetUserIds.Any())
                {
                    _logger.LogWarning("⚠️ [SendRecommendation] No target users found for role: {Role}", request.Role);
                    return Ok(new { message = "No target users found for this role", sentCount = 0 });
                }

                // ✅ Find expenses to process
                List<ExpenseResponseDto> expensesToProcess;
                
                // ✅ If expenseId is provided, only process that specific expense
                if (request.ExpenseId.HasValue && request.ExpenseId.Value > 0)
                {
                    _logger.LogInformation("✅ [SendRecommendation] Targeting specific expense: ExpenseId={ExpenseId}", request.ExpenseId.Value);
                    
                    // Get all expenses to find the specific one
                    var allExpenses = await GetAllExpensesForAnalytics(null, null, null);
                    var specificExpense = allExpenses
                        .FirstOrDefault(e => e.ExpenseId == request.ExpenseId.Value);
                    
                    if (specificExpense == null)
                    {
                        _logger.LogWarning("⚠️ [SendRecommendation] Expense not found: ExpenseId={ExpenseId}", request.ExpenseId.Value);
                        return NotFound(new { error = $"Expense with id {request.ExpenseId.Value} not found" });
                    }
                    
                    // ✅ Verify expense has the target status
                    if (!specificExpense.ApprovalStatus?.Equals(targetStatus, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _logger.LogWarning("⚠️ [SendRecommendation] Expense {ExpenseId} does not have target status {TargetStatus}, current status: {CurrentStatus}", 
                            request.ExpenseId.Value, targetStatus, specificExpense.ApprovalStatus);
                        return BadRequest(new { error = $"Expense does not have the required status for this role. Current status: {specificExpense.ApprovalStatus}" });
                    }
                    
                    // ✅ If targeting specific user, verify expense belongs to their tenant(s)
                    if (targetUserTenantIds != null && targetUserTenantIds.Any())
                    {
                        var targetHotelCodes = userTenants
                            .Where(t => targetUserTenantIds.Contains(t.Id))
                            .Select(t => t.Code)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        
                        if (string.IsNullOrWhiteSpace(specificExpense.HotelCode) || !targetHotelCodes.Contains(specificExpense.HotelCode))
                        {
                            _logger.LogWarning("⚠️ [SendRecommendation] Expense {ExpenseId} does not belong to target user's tenant(s). Expense HotelCode: {ExpenseHotelCode}", 
                                request.ExpenseId.Value, specificExpense.HotelCode);
                            return BadRequest(new { error = "Expense does not belong to the target user's hotel(s)" });
                        }
                    }
                    
                    expensesToProcess = new List<ExpenseResponseDto> { specificExpense };
                    _logger.LogInformation("✅ [SendRecommendation] Processing specific expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                        specificExpense.ExpenseId, specificExpense.HotelCode);
                }
                else
                {
                    // ✅ No specific expense - process all expenses with target status
                    var allExpenses = await GetAllExpensesForAnalytics(null, null, null);
                    var targetExpenses = allExpenses
                        .Where(e => e.ApprovalStatus?.Equals(targetStatus, StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    if (!targetExpenses.Any())
                    {
                        _logger.LogInformation("ℹ️ [SendRecommendation] No expenses found with status: {Status}", targetStatus);
                        return Ok(new { message = "No pending expenses found for this role", sentCount = 0 });
                    }

                    // ✅ Filter expenses: If targeting specific user, only process expenses from their tenant(s)
                    if (targetUserTenantIds != null && targetUserTenantIds.Any())
                    {
                        // ✅ Only process expenses from the target user's tenant(s)
                        var targetHotelCodes = userTenants
                            .Where(t => targetUserTenantIds.Contains(t.Id))
                            .Select(t => t.Code)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        
                        expensesToProcess = targetExpenses
                            .Where(e => !string.IsNullOrWhiteSpace(e.HotelCode) && targetHotelCodes.Contains(e.HotelCode))
                            .ToList();
                        
                        _logger.LogInformation("✅ [SendRecommendation] Filtered expenses for specific user - Total: {Total}, For user tenants: {Filtered}", 
                            targetExpenses.Count, expensesToProcess.Count);
                    }
                    else
                    {
                        // ✅ Process all expenses with target status (sending to all users with this role)
                        expensesToProcess = targetExpenses;
                        _logger.LogInformation("✅ [SendRecommendation] Processing all expenses with status: {Status}, Count: {Count}", 
                            targetStatus, expensesToProcess.Count);
                    }
                }

                if (!expensesToProcess.Any())
                {
                    _logger.LogInformation("ℹ️ [SendRecommendation] No expenses found for target user's tenant(s) with status: {Status}", targetStatus);
                    return Ok(new { message = "No pending expenses found for the target user's tenant(s)", sentCount = 0 });
                }

                // ✅ Add recommendation to approval history for each expense
                int successCount = 0;
                var errors = new List<string>();

                foreach (var expenseDto in expensesToProcess)
                {
                    try
                    {
                        // Find the tenant database for this expense
                        var expenseTenant = userTenants.FirstOrDefault(t => t.Code == expenseDto.HotelCode);
                        if (expenseTenant == null)
                        {
                            _logger.LogWarning("⚠️ [SendRecommendation] Tenant not found for expense: ExpenseId={ExpenseId}, HotelCode={HotelCode}", 
                                expenseDto.ExpenseId, expenseDto.HotelCode);
                            continue;
                        }

                        var connectionString = $"Server={server}; Database={expenseTenant.DatabaseName}; User Id={dbUserId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        optionsBuilder.UseSqlServer(connectionString);
                        await using var tenantContext = new ApplicationDbContext(optionsBuilder.Options);

                        // Find the expense in tenant database
                        var expense = await tenantContext.Expenses
                            .FirstOrDefaultAsync(e => e.ExpenseId == expenseDto.ExpenseId);

                        if (expense == null)
                        {
                            _logger.LogWarning("⚠️ [SendRecommendation] Expense not found: ExpenseId={ExpenseId}", expenseDto.ExpenseId);
                            continue;
                        }

                        // Add recommendation to approval history
                        var history = new FinanceLedgerAPI.Models.ExpenseApprovalHistory
                        {
                            ExpenseId = expense.ExpenseId,
                            Action = "recommendation",
                            ActionBy = currentUserId,
                            ActionByFullName = currentUserFullName,
                            ActionAt = KsaTime.Now,
                            Status = expense.ApprovalStatus ?? "",
                            Comments = $"تم إرسال توصية من {currentUserFullName}",
                            Recommendation = request.Message.Trim(),
                            RecommendationToUserId = request.UserId, // NULL if sending to all users with this role
                            RecommendationReadBy = null
                        };

                        await tenantContext.ExpenseApprovalHistories.AddAsync(history);
                        await tenantContext.SaveChangesAsync();
                        successCount++;

                        _logger.LogInformation("✅ [SendRecommendation] Recommendation added: ExpenseId={ExpenseId}, TargetUserId={TargetUserId}", 
                            expense.ExpenseId, request.UserId);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Error processing expense {expenseDto.ExpenseId}: {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogError(ex, "❌ [SendRecommendation] {Error}", errorMsg);
                    }
                }

                if (errors.Any())
                {
                    _logger.LogWarning("⚠️ [SendRecommendation] Completed with {SuccessCount} successes and {ErrorCount} errors", 
                        successCount, errors.Count);
                    return Ok(new { 
                        message = $"Recommendation sent to {successCount} expense(s)", 
                        sentCount = successCount,
                        errors = errors
                    });
                }

                _logger.LogInformation("✅ [SendRecommendation] Successfully sent {Count} recommendation(s)", successCount);
                return Ok(new { 
                    message = $"تم إرسال التوصية بنجاح إلى {successCount} مصروف", 
                    sentCount = successCount 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SendRecommendation] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to send recommendation", details = ex.Message });
            }
        }
    }

    /// <summary>
    /// DTO for sending recommendations
    /// </summary>
    public class SendRecommendationRequest
    {
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? RecommendationType { get; set; }
        public int? ExpenseId { get; set; } // ✅ Optional: If provided, send recommendation only to this specific expense
    }
}

