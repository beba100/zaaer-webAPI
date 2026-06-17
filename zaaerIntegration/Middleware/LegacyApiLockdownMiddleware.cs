using System.Text.Json;

namespace zaaerIntegration.Middleware
{
    /// <summary>
    /// In Production, returns 410 Gone for retired legacy controller routes under /api/{controller}.
    /// Does not affect /api/v1/pms/* or other non-legacy paths.
    /// </summary>
    public sealed class LegacyApiLockdownMiddleware
    {
        private static readonly HashSet<string> LegacyControllerSegments = new(StringComparer.OrdinalIgnoreCase)
        {
            "Expense",
            "Reservation",
            "Invoice",
            "PaymentReceipt",
            "Apartment",
            "Building",
            "Floor",
            "RoomType",
            "ReservationUnit",
            "ExpenseApprovalRules",
            "WeatherForecast"
        };

        private static readonly byte[] GoneJson = JsonSerializer.SerializeToUtf8Bytes(
            new { error = "Legacy API retired. Use /api/v1/pms/*." });

        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;

        public LegacyApiLockdownMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_environment.IsProduction() && IsLegacyApiPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status410Gone;
                context.Response.ContentType = "application/json";
                await context.Response.Body.WriteAsync(GoneJson, context.RequestAborted);
                return;
            }

            await _next(context);
        }

        private static bool IsLegacyApiPath(PathString path)
        {
            if (!path.HasValue)
            {
                return false;
            }

            var value = path.Value!;
            if (!value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remainder = value.AsSpan(5);
            var slashIndex = remainder.IndexOf('/');
            var segment = slashIndex >= 0
                ? remainder[..slashIndex].ToString()
                : remainder.ToString();

            return LegacyControllerSegments.Contains(segment);
        }
    }
}
