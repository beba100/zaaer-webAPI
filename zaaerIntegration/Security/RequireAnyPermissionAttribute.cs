using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Allows the action when the user has at least one of the listed permissions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireAnyPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _permissionCodes;

        public RequireAnyPermissionAttribute(params string[] permissionCodes)
        {
            _permissionCodes = permissionCodes ?? Array.Empty<string>();
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (_permissionCodes.Length == 0)
            {
                return;
            }

            var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
            if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
                return;
            }

            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            foreach (var code in _permissionCodes)
            {
                if (await permissionService.HasPermissionAsync(
                        currentUser.UserId.Value,
                        currentUser.TenantId.Value,
                        code,
                        currentUser.AuthMode,
                        context.HttpContext.RequestAborted))
                {
                    return;
                }
            }

            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<RequireAnyPermissionAttribute>();
            logger.LogWarning(
                "[SECURITY] RBAC deny: User {UserId} missing all of [{Permissions}] for {Path}",
                currentUser.UserId.Value,
                string.Join(", ", _permissionCodes),
                context.HttpContext.Request.Path.Value);
            context.Result = new ForbidResult();
        }
    }
}
