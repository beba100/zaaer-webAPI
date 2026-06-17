using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Middleware
{
    /// <summary>
    /// Middleware  X-Hotel-Code Header 
    /// </summary>
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;

        /// <summary>
        /// Creates the tenant resolution middleware.
        /// </summary>
        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Resolves the request tenant and enforces hotel access for PMS tenant data.
        /// </summary>
        public async Task InvokeAsync(
            HttpContext context,
            ITenantService tenantService,
            ICurrentUserContext currentUser,
            IHotelAccessService hotelAccessService)
        {
            //  Tenant  endpoints:
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var pathUpper = context.Request.Path.Value ?? "";
            var method = context.Request.Method;
            var isPmsApiPath = path.StartsWith("/api/v1/pms/", StringComparison.OrdinalIgnoreCase);
            
            // Check if path is in whitelist (doesn't require X-Hotel-Code)
            var isDevExpressReportingPath =
                path.StartsWith("/dxxrdv") ||
                path.StartsWith("/dxxrd") ||
                path.StartsWith("/dxxqb");

            var isWhitelisted = path.Contains("/swagger") ||
                path.Contains("/health") ||
                path.Contains("/_framework") ||
                path.Contains("/css") ||
                path.Contains("/js") ||
                path.Contains("/api/tenant") ||
                path.Contains("/api/jobs/") ||
                path.Contains("/api/config/devextreme-license") ||  // ? Allow DevExtreme license endpoint
                path.Contains("/api/reports/devextreme-license") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports/daily-report") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports/pending-failed-receipts") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports/zaaer-integrate") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports/payment-method-summary") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports/payment-daily-net-ex-tax-rules") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.StartsWith("/api/reports/find-tenant-by-zaaer-id") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.StartsWith("/api/reports/find-tenant-by-code") ||  // Report endpoint: bypasses X-Hotel-Code only; requires JWT + HotelScopeService tenant filter
                path.Contains("/api/reports-test/") ||  // ✅ Allow reports testing endpoints without hotel code (DevExpress testing)
                path.Contains("/api/auth/") || // ? ✅ Allow auth endpoints (login, forgot-password) without hotel code
                path.Contains("/api/v1/public/booking", StringComparison.OrdinalIgnoreCase) || // Public booking engine (hotel via query)
                path.Contains("/api/rbac") || // ✅ Master RBAC admin APIs (JWT + optional hotel scope)
                path.Contains("/room-board/hotel-codes", StringComparison.OrdinalIgnoreCase) || // PMS room board: populate hotel dropdown from Master Tenants before X-Hotel-Code is chosen
                path.Contains("/api/masteruser", StringComparison.OrdinalIgnoreCase) ||  // ✅ Allow MasterUser endpoints without hotel code (uses Master DB)
                path == "/" ||
                path == "/index.html" ||
                path == "/devextreme.html" ||
                path == "/test-reports.html" ||  // ✅ Allow test-reports.html page for DevExpress testing
                path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||  // ?    HTML
                path == "/favicon.ico" ||  // Favicon
                path == "/robots.txt" ||  // Robots.txt
                // ? Allow access to static files (file uploads/downloads) without X-Hotel-Code header
                IsStaticFile(path) ||
                IsFileUploadPath(pathUpper);
            
            if (isWhitelisted)
            {
                await _next(context);
                return;
            }

            if (isDevExpressReportingPath)
            {
                var hasDevExpressHotelCode =
                    (context.Request.Headers.TryGetValue("X-Hotel-Code", out var dxHotelHeader) &&
                     !string.IsNullOrWhiteSpace(dxHotelHeader)) ||
                    (context.Request.Query.TryGetValue("hotelCode", out var dxHotelQuery) &&
                     !string.IsNullOrWhiteSpace(dxHotelQuery));

                if (!hasDevExpressHotelCode)
                {
                    _logger.LogWarning(
                        "[SECURITY] DevExpress reporting request missing hotel scope | Path: {Path} | Method: {Method}",
                        pathUpper,
                        method);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Unauthorized",
                        message = "DevExpress reporting requires X-Hotel-Code header or hotelCode query parameter.",
                        hint = "Pass hotelCode with every /DXXRDV request for tenant isolation."
                    });
                    return;
                }
            }

            // Check if X-Hotel-Code header is present
            var hasHotelCode =
                (context.Request.Headers.TryGetValue("X-Hotel-Code", out var hotelCodeHeader) &&
                 !string.IsNullOrWhiteSpace(hotelCodeHeader)) ||
                (context.Request.Query.TryGetValue("hotelCode", out var hotelCodeQuery) &&
                 !string.IsNullOrWhiteSpace(hotelCodeQuery));
            
            if (!hasHotelCode)
            {
                // Log the specific path that's missing the header for debugging
                _logger.LogWarning(
                    "[SECURITY] Missing X-Hotel-Code header | Path: {Path} | Method: {Method} | UserAgent: {UserAgent} | RemoteIP: {RemoteIP}",
                    pathUpper,
                    method,
                    context.Request.Headers["User-Agent"].ToString(),
                    context.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
            }

            try
            {
                //     Tenant
                var tenant = tenantService.GetTenant();
                
                if (tenant != null)
                {
                    if (isPmsApiPath && (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue))
                    {
                        _logger.LogWarning(
                            "[SECURITY] Unauthenticated PMS tenant request rejected | Path: {Path} | TenantId: {TenantId} ({TenantCode})",
                            pathUpper,
                            tenant.Id,
                            tenant.Code);

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Unauthorized",
                            message = "Authentication is required to access PMS hotel data."
                        });
                        return;
                    }

                    if (currentUser.IsAuthenticated && currentUser.UserId.HasValue)
                    {
                        var canAccessTenant = await hotelAccessService.CanAccessTenantAsync(
                            currentUser.UserId.Value,
                            tenant.Id,
                            context.RequestAborted);

                        if (!canAccessTenant)
                        {
                            _logger.LogWarning(
                                "[SECURITY] User {UserId} attempted to access unauthorized tenant {TenantId} ({TenantCode})",
                                currentUser.UserId.Value,
                                tenant.Id,
                                tenant.Code);

                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Forbidden",
                                message = "You are not allowed to access this hotel."
                            });
                            return;
                        }
                    }
                }
                
                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Log the specific path and method that caused the issue for debugging
                _logger.LogWarning(
                    "[SECURITY] Missing X-Hotel-Code header | Path: {Path} | Method: {Method} | Message: {Message}",
                    pathUpper,
                    context.Request.Method,
                    ex.Message);
                
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = ex.Message,
                    hint = "Please provide 'X-Hotel-Code' header with a valid hotel code (e.g., Dammam1)",
                    path = pathUpper,
                    method = context.Request.Method
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Tenant not found: {Message}", ex.Message);
                
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Not Found",
                    message = ex.Message,
                    hint = "The hotel code you provided does not exist in the Master Database. Please verify the hotel code."
                });
            }
            catch (InvalidOperationException ex)
            {
                var isResortOnly = ex.Message.Contains("Resort tickets are available only", StringComparison.OrdinalIgnoreCase);
                if (isResortOnly)
                {
                    _logger.LogWarning(
                        "[SECURITY] Resort tickets rejected for non-resort tenant | Path: {Path} | Message: {Message}",
                        pathUpper,
                        ex.Message);

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Bad Request",
                        message = ex.Message,
                        hint = "Set hotel_settings.property_type to 'resort' for this property, or switch to a resort hotel in the header picker."
                    });
                }
                else if (ex.Message.Contains("Master Database", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("DatabaseName", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(ex, "Invalid operation in TenantMiddleware: {Message}", ex.Message);

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Database Configuration Error",
                        message = ex.Message,
                        hint = "Please check Master Database connection or tenant configuration in Master DB"
                    });
                }
                else
                {
                    _logger.LogWarning(ex, "Invalid operation in TenantMiddleware: {Message}", ex.Message);

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Invalid Operation",
                        message = ex.Message,
                        hint = "An error occurred while processing your request"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TenantMiddleware: {Message}", ex.Message);
                
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal Server Error",
                    message = "An unexpected error occurred while processing your request",
                    hint = "Please contact support if this issue persists"
                });
            }
        }

        /// <summary>
        /// Check if the request is for a static file (based on file extension)
        /// </summary>
        private static bool IsStaticFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Common static file extensions
            var staticExtensions = new[]
            {
                ".zip", ".rar", ".7z", ".tar", ".gz",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp",
                ".mp4", ".avi", ".mov", ".wmv", ".flv",
                ".mp3", ".wav", ".ogg", ".aac",
                ".txt", ".csv", ".json", ".xml",
                ".exe", ".msi", ".dmg", ".deb", ".rpm",
                ".ico"  // Favicon
            };

            return staticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if the request is for file upload/download paths (e.g., wwwroot subdirectories)
        /// </summary>
        private static bool IsFileUploadPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Allow access to wwwroot subdirectories
            // This includes paths like: /Old Exe(RHotel_n)/Debug.zip
            var fileUploadPaths = new[]
            {
                "/Exe(RHotel_n)/",
                "/uploads",
                "/files",
                "/downloads",
                "/temp",
                "/images",
                "/documents",
                "/assets"
            };

            // Check if path contains any of the upload paths
            return fileUploadPaths.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Extension method  Middleware
    /// </summary>
    public static class TenantMiddlewareExtensions
    {
        /// <summary>
        /// Adds tenant resolution and PMS hotel-access enforcement to the request pipeline.
        /// </summary>
        public static IApplicationBuilder UseTenantMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantMiddleware>();
        }
    }
}
