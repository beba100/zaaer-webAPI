using System.Diagnostics;

namespace zaaerIntegration.Middleware
{
    /// <summary>
    /// Logs slow report API requests with a [PERF] prefix for performance monitoring.
    /// </summary>
    public sealed class SlowRequestLoggingMiddleware
    {
        private const int DefaultThresholdMs = 3000;

        private readonly RequestDelegate _next;
        private readonly ILogger<SlowRequestLoggingMiddleware> _logger;
        private readonly int _thresholdMs;

        public SlowRequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<SlowRequestLoggingMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _thresholdMs = Math.Max(
                100,
                configuration.GetValue<int?>("Performance:SlowRequestThresholdMs") ?? DefaultThresholdMs);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!ShouldMonitor(context.Request))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds >= _thresholdMs)
                {
                    var request = context.Request;
                    _logger.LogWarning(
                        "[PERF] Slow request | Method: {Method} | Path: {Path} | DurationMs: {DurationMs} | StatusCode: {StatusCode}",
                        request.Method,
                        request.Path.Value ?? string.Empty,
                        stopwatch.ElapsedMilliseconds,
                        context.Response.StatusCode);
                }
            }
        }

        private static bool ShouldMonitor(HttpRequest request)
        {
            var path = request.Path.Value ?? string.Empty;
            return path.StartsWith("/api/reports", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v1/pms/hotel-reports", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class SlowRequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseSlowRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SlowRequestLoggingMiddleware>();
        }
    }
}
