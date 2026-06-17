using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Action filter for permission-based authorization.
    /// Use after JWT authentication is enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public RequirePermissionAttribute(string permissionCode)
        {
            _permissionCode = permissionCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
            if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
                return;
            }

            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            var hasPermission = await permissionService.HasPermissionAsync(
                currentUser.UserId.Value,
                currentUser.TenantId.Value,
                _permissionCode,
                currentUser.AuthMode,
                context.HttpContext.RequestAborted);

            if (!hasPermission)
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<RequirePermissionAttribute>();
                logger.LogWarning(
                    "[SECURITY] RBAC deny: User {UserId} is missing permission {PermissionCode} for {Path}",
                    currentUser.UserId.Value,
                    _permissionCode,
                    context.HttpContext.Request.Path.Value);
                context.Result = new ForbidResult();
            }
        }
    }
}
