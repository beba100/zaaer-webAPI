using System.Diagnostics;
using System.Security.Claims;

namespace zaaerIntegration.Middleware
{
    /// <summary>
    /// Logs slow PMS API requests to the performance log sink without affecting non-PMS traffic.
    /// </summary>
    public sealed class PmsSlowRequestLoggingMiddleware
    {
        private const int DefaultThresholdMs = 1000;
        private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "access_token",
            "apiKey",
            "apikey",
            "authorization",
            "bearer",
            "password",
            "refreshToken",
            "secret",
            "token"
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<PmsSlowRequestLoggingMiddleware> _logger;
        private readonly int _thresholdMs;

        public PmsSlowRequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<PmsSlowRequestLoggingMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _thresholdMs = Math.Max(
                100,
                configuration.GetValue<int?>("Performance:PmsSlowRequestThresholdMs") ?? DefaultThresholdMs);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!IsPmsApiRequest(context.Request))
            {
                await _next(context);
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds >= _thresholdMs)
                {
                    LogSlowRequest(context, sw.ElapsedMilliseconds);
                }
            }
        }

        private void LogSlowRequest(HttpContext context, long elapsedMs)
        {
            var request = context.Request;
            var hotelCode = request.Headers.TryGetValue("X-Hotel-Code", out var hotelHeader)
                ? hotelHeader.ToString()
                : string.Empty;

            _logger.LogWarning(
                "[PERFORMANCE] PMS slow request | Method: {Method} | Path: {Path} | Query: {QueryString} | StatusCode: {StatusCode} | ElapsedMs: {ElapsedMs} | ThresholdMs: {ThresholdMs} | HotelCode: {HotelCode} | UserId: {UserId} | TraceId: {TraceId}",
                request.Method,
                request.Path.Value ?? string.Empty,
                BuildSafeQueryString(request),
                context.Response.StatusCode,
                elapsedMs,
                _thresholdMs,
                hotelCode,
                ResolveUserId(context.User),
                context.TraceIdentifier);
        }

        private static bool IsPmsApiRequest(HttpRequest request) =>
            request.Path.StartsWithSegments("/api/v1/pms", StringComparison.OrdinalIgnoreCase);

        private static string BuildSafeQueryString(HttpRequest request)
        {
            if (!request.QueryString.HasValue || request.Query.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var pair in request.Query.OrderBy(q => q.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = SensitiveQueryKeys.Contains(pair.Key)
                    ? "***"
                    : pair.Value.ToString();
                parts.Add($"{pair.Key}={value}");
            }

            return string.Join("&", parts);
        }

        private static string ResolveUserId(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return string.Empty;
            }

            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("userId")
                ?? user.FindFirstValue("UserId")
                ?? user.Identity?.Name
                ?? string.Empty;
        }
    }

    public static class PmsSlowRequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UsePmsSlowRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PmsSlowRequestLoggingMiddleware>();
        }
    }
}
