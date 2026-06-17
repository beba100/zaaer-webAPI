using FinanceLedgerAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using zaaerIntegration.Data;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// خدمة للحصول على معلومات الفندق (Tenant) الحالي
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MasterDbContext _masterDbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantService> _logger;
        private readonly SmartLogger? _smartLogger;
        private Tenant? _currentTenant;

        /// <summary>
        /// Constructor for TenantService
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <param name="masterDbContext">Master database context</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="logger">Logger</param>
        /// <param name="smartLogger">Smart logger for optimized logging</param>
        public TenantService(
            IHttpContextAccessor httpContextAccessor, 
            MasterDbContext masterDbContext,
            IConfiguration configuration,
            ILogger<TenantService> logger,
            SmartLogger? smartLogger = null)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _smartLogger = smartLogger;
        }

        /// <summary>
        /// الحصول على الفندق الحالي بناءً على X-Hotel-Code Header
        /// </summary>
        public Tenant? GetTenant()
        {
            // استخدام الـ Tenant المخزن مؤقتاً إذا كان موجوداً (Caching)
            // This allows background workers to set tenant before calling GetTenant()
            if (_currentTenant != null)
                return _currentTenant;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _smartLogger?.LogWarning(
                    category: "SECURITY",
                    message: "HttpContext is null - cannot resolve tenant",
                    action: "GetTenant");
                throw new InvalidOperationException("HttpContext is not available. Cannot resolve tenant.");
            }

            // قراءة قيمة X-Hotel-Code من Header، مع fallback للـ query حتى تعمل روابط API المفتوحة من DevTools.
            var hotelCode = httpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues)
                ? hotelCodeValues.ToString().Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(hotelCode) &&
                httpContext.Request.Query.TryGetValue("hotelCode", out var hotelCodeQuery))
            {
                hotelCode = hotelCodeQuery.ToString().Trim();
            }

            if (string.IsNullOrWhiteSpace(hotelCode))
            {
                _smartLogger?.LogWarning(
                    category: "SECURITY",
                    message: "Missing or empty X-Hotel-Code header / hotelCode query",
                    action: "GetTenant");
                throw new UnauthorizedAccessException("Missing X-Hotel-Code header. Please provide a valid hotel code.");
            }

            try
            {
                // البحث عن الفندق في قاعدة البيانات المركزية (Case-insensitive)
                _currentTenant = _masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefault(t => t.Code.ToLower() == hotelCode.ToLower());

                if (_currentTenant == null)
                {
                    _smartLogger?.LogError(
                        category: "SECURITY",
                        message: $"Tenant not found for code: {hotelCode} in Master DB",
                        action: "GetTenant");
                    throw new KeyNotFoundException($"Tenant not found for code: {hotelCode}. Please verify the hotel code exists in Master Database.");
                }

                // التحقق من وجود DatabaseName
                if (string.IsNullOrWhiteSpace(_currentTenant.DatabaseName))
                {
                    _smartLogger?.LogError(
                        category: "DB",
                        message: $"DatabaseName is not set for tenant: {_currentTenant.Code} (Id: {_currentTenant.Id})",
                        action: "GetTenant");
                    throw new InvalidOperationException(
                        $"DatabaseName is not configured for tenant: {_currentTenant.Code}. " +
                        "Please add DatabaseName to the tenant record in Master Database.");
                }

                // Removed routine LogInformation - only log errors/warnings

                return _currentTenant;
            }
            catch (KeyNotFoundException)
            {
                // Re-throw KeyNotFoundException as-is
                throw;
            }
            catch (InvalidOperationException)
            {
                // Re-throw InvalidOperationException as-is
                throw;
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: $"Database error while resolving tenant for code: {hotelCode}: {ex.Message}",
                    action: "GetTenant",
                    exception: ex);
                
                // Check if it's a database connection error
                if (ex is Microsoft.Data.SqlClient.SqlException || 
                    ex is Microsoft.EntityFrameworkCore.DbUpdateException ||
                    ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Failed to connect to Master Database (db29328) while resolving tenant. " +
                        $"Please check Master Database connection. Error: {ex.Message}", ex);
                }
                
                throw new InvalidOperationException(
                    $"An error occurred while resolving tenant for code: {hotelCode}. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// الحصول على كود الفندق من HTTP Request
        /// </summary>
        public string? GetTenantCode()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null)
                    return null;

                if (httpContext.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeValues))
                {
                    var code = hotelCodeValues.ToString().Trim();
                    return string.IsNullOrWhiteSpace(code) ? null : code;
                }

                if (httpContext.Request.Query.TryGetValue("hotelCode", out var hotelCodeQuery))
                {
                    var code = hotelCodeQuery.ToString().Trim();
                    return string.IsNullOrWhiteSpace(code) ? null : code;
                }

                return null;
            }
            catch (Exception ex)
            {
                _smartLogger?.LogWarning(
                    category: "SECURITY",
                    message: $"Error getting tenant code from header: {ex.Message}",
                    action: "GetTenantCode",
                    exception: ex);
                return null;
            }
        }

        /// <summary>
        /// الحصول على Connection String للفندق الحالي بناءً على DatabaseName
        /// </summary>
        /// <returns>Connection String للفندق الحالي</returns>
        public string GetTenantConnectionString()
        {
            try
            {
                var tenant = GetTenant();
                
                if (tenant == null)
                {
                    _smartLogger?.LogError(
                        category: "DB",
                        message: "Cannot get connection string - Tenant is null",
                        action: "GetTenantConnectionString");
                    throw new InvalidOperationException("Tenant not resolved. Cannot get connection string.");
                }

                return BuildConnectionStringForTenant(tenant);
            }
            catch (KeyNotFoundException)
            {
                // Re-throw KeyNotFoundException as-is
                throw;
            }
            catch (InvalidOperationException)
            {
                // Re-throw InvalidOperationException as-is
                throw;
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: $"Error getting connection string for tenant: {_currentTenant?.Code ?? "Unknown"}: {ex.Message}",
                    action: "GetTenantConnectionString",
                    exception: ex);
                throw new InvalidOperationException(
                    $"Failed to get connection string for tenant. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// بناء Connection String لـ Tenant محدد
        /// </summary>
        /// <param name="tenant">الـ Tenant المطلوب</param>
        /// <returns>Connection String</returns>
        public string BuildConnectionStringForTenant(Tenant tenant)
        {
            if (tenant == null)
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: "BuildConnectionStringForTenant: Tenant is null",
                    action: "BuildConnectionStringForTenant");
                throw new ArgumentNullException(nameof(tenant));
            }

            // التحقق من وجود DatabaseName
            if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: $"DatabaseName is null or empty for tenant: {tenant.Code} (Id: {tenant.Id})",
                    action: "BuildConnectionStringForTenant");
                throw new InvalidOperationException(
                    $"DatabaseName is not set for tenant: {tenant.Code}. " +
                    "Please add DatabaseName to the tenant record in Master Database.");
            }

            // قراءة إعدادات قاعدة البيانات من appsettings.json
            var server = _configuration["TenantDatabase:Server"]?.Trim();
            var userId = _configuration["TenantDatabase:UserId"]?.Trim();
            var password = _configuration["TenantDatabase:Password"]?.Trim();

            // التحقق من وجود جميع الإعدادات المطلوبة
            if (string.IsNullOrWhiteSpace(server))
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: "TenantDatabase:Server is missing in appsettings.json",
                    action: "BuildConnectionStringForTenant");
                throw new InvalidOperationException(
                    "TenantDatabase:Server is not configured in appsettings.json. " +
                    "Please add TenantDatabase:Server setting.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: "TenantDatabase:UserId is missing in appsettings.json",
                    action: "BuildConnectionStringForTenant");
                throw new InvalidOperationException(
                    "TenantDatabase:UserId is not configured in appsettings.json. " +
                    "Please add TenantDatabase:UserId setting.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _smartLogger?.LogError(
                    category: "DB",
                    message: "TenantDatabase:Password is missing in appsettings.json",
                    action: "BuildConnectionStringForTenant");
                throw new InvalidOperationException(
                    "TenantDatabase:Password is not configured in appsettings.json. " +
                    "Please add TenantDatabase:Password setting.");
            }

            // بناء Connection String ديناميكياً
            var connectionString = $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

            // Removed routine LogDebug - only log errors/warnings

            return connectionString;
        }

        /// <summary>
        /// التحقق من الاتصال بقاعدة بيانات الفندق الحالي
        /// </summary>
        /// <returns>true إذا كان الاتصال ناجحاً</returns>
        public async Task<bool> ValidateTenantConnectionAsync()
        {
            Tenant? tenant = null;
            try
            {
                tenant = GetTenant();
                
                if (tenant == null)
                {
                    _smartLogger?.LogError(
                        category: "DB",
                        message: "Cannot validate connection - Tenant is null",
                        action: "ValidateTenantConnectionAsync");
                    return false;
                }

                var connectionString = GetTenantConnectionString();
                
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // التحقق من اسم قاعدة البيانات الصحيحة
                var command = new SqlCommand("SELECT DB_NAME() AS CurrentDatabase", connection);
                var result = await command.ExecuteScalarAsync();
                var currentDatabase = result?.ToString();
                
                if (currentDatabase != tenant.DatabaseName)
                {
                    _smartLogger?.LogError(
                        category: "DB",
                        message: $"Database mismatch! Expected: {tenant.DatabaseName}, Actual: {currentDatabase} for tenant: {tenant.Code}",
                        action: "ValidateTenantConnectionAsync");
                    return false;
                }
                
                // Removed routine LogInformation - only log errors/warnings
                
                return true;
            }
            catch (Exception ex)
            {
                var tenantCode = tenant?.Code ?? _currentTenant?.Code ?? "Unknown";
                _smartLogger?.LogError(
                    category: "DB",
                    message: $"Failed to validate connection for tenant: {tenantCode}: {ex.Message}",
                    action: "ValidateTenantConnectionAsync",
                    exception: ex);
                return false;
            }
        }

        /// <summary>
        /// تعيين الـ Tenant الحالي مباشرة (للاستخدام في background workers حيث لا يوجد HttpContext)
        /// </summary>
        public void SetCurrentTenant(Tenant tenant)
        {
            if (tenant == null)
                throw new ArgumentNullException(nameof(tenant));

            _currentTenant = tenant;
            // Removed routine LogDebug - only log errors/warnings
        }

        /// <summary>
        /// الحصول على الـ Tenant من HotelId (للاستخدام في background workers)
        /// Note: HotelId here refers to Zaaer ID from hotel_settings, not tenant.Id
        /// We need to find tenant by matching hotel_settings.zaaer_id
        /// </summary>
        public async Task<Tenant?> GetTenantByHotelIdAsync(int hotelId)
        {
            try
            {
                // HotelId in PartnerQueue refers to hotel_settings.zaaer_id, not tenant.Id
                // We need to find the tenant by checking all tenant databases for matching hotel_settings.zaaer_id
                // This is expensive, so we'll use a simpler approach: get tenant from connection string if available
                // For now, return null and let the handler use SetCurrentTenant with tenant from db connection
                _smartLogger?.LogWarning(
                    category: "SYNC",
                    message: $"GetTenantByHotelIdAsync called with HotelId={hotelId}, but this requires checking all tenant databases. Use SetCurrentTenant instead.",
                    action: "GetTenantByHotelIdAsync");
                return null;
            }
            catch (Exception ex)
            {
                _smartLogger?.LogError(
                    category: "SYNC",
                    message: $"Error getting tenant by HotelId {hotelId}: {ex.Message}",
                    action: "GetTenantByHotelIdAsync",
                    exception: ex);
                return null;
            }
        }
    }
}
