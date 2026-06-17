namespace zaaerIntegration.Middleware
{
    /// <summary>
    /// Adds standard browser security headers (CSP, frame protection, HSTS in production).
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public SecurityHeadersMiddleware(
            RequestDelegate next,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _next = next;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (!_environment.IsDevelopment())
            {
                headers["Content-Security-Policy"] = BuildContentSecurityPolicy();
            }

            if (_environment.IsProduction() &&
                _configuration.GetValue("Security:EnableHsts", true) &&
                context.Request.IsHttps)
            {
                var maxAge = Math.Max(0, _configuration.GetValue("Security:HstsMaxAgeSeconds", 31536000));
                headers["Strict-Transport-Security"] = $"max-age={maxAge}; includeSubDomains";
            }

            await _next(context);
        }

        private string BuildContentSecurityPolicy()
        {
            var configured = _configuration["Security:ContentSecurityPolicy"]?.Trim();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            // DevExtreme + PMS static assets require inline script/style in current architecture.
            return string.Join("; ",
                "default-src 'self'",
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
                "style-src 'self' 'unsafe-inline'",
                "img-src 'self' data: blob: https:",
                "font-src 'self' data:",
                "connect-src 'self'",
                "frame-ancestors 'self'",
                "base-uri 'self'",
                "form-action 'self'");
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
