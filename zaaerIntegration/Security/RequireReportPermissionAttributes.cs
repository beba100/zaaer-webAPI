using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Requires a lodging report permission matching the current property type (hotel or resort).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireLodgingReportPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _reportKey;

        public RequireLodgingReportPermissionAttribute(string reportKey)
        {
            _reportKey = reportKey ?? string.Empty;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
            if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
                return;
            }

            var propertyType = await ResolvePropertyTypeAsync(context, cancellationToken: context.HttpContext.RequestAborted);
            if (PropertyTypes.IsHall(propertyType))
            {
                context.Result = new ForbidResult();
                return;
            }

            var codes = PmsReportPermissions.LodgingCodes(_reportKey, propertyType);
            if (codes.Length == 0)
            {
                context.Result = new ForbidResult();
                return;
            }

            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            foreach (var code in codes)
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

            context.Result = new ForbidResult();
        }

        internal static async Task<string?> ResolvePropertyTypeAsync(
            AuthorizationFilterContext context,
            CancellationToken cancellationToken)
        {
            var tenantService = context.HttpContext.RequestServices.GetRequiredService<ITenantService>();
            var tenant = tenantService.GetTenant();
            if (tenant == null || string.IsNullOrWhiteSpace(tenant.Code))
            {
                return null;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var code = tenant.Code.Trim();
            var propertyType = await db.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == code.ToLower())
                .Select(h => h.PropertyType)
                .FirstOrDefaultAsync(cancellationToken);

            return propertyType?.Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Requires a hall report permission (hall properties only).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireHallReportPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _reportKey;

        public RequireHallReportPermissionAttribute(string reportKey)
        {
            _reportKey = reportKey ?? string.Empty;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
            if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
                return;
            }

            var propertyType = await RequireLodgingReportPermissionAttribute.ResolvePropertyTypeAsync(
                context,
                context.HttpContext.RequestAborted);
            if (!PropertyTypes.IsHall(propertyType))
            {
                context.Result = new ForbidResult();
                return;
            }

            var codes = PmsReportPermissions.HallCodes(_reportKey);
            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            foreach (var code in codes)
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

            context.Result = new ForbidResult();
        }
    }
}
