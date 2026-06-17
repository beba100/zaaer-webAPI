using System.Security.Claims;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public class CurrentUserContext : ICurrentUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<IReadOnlyCollection<string>> _roles;
        private readonly Lazy<IReadOnlyCollection<string>> _permissions;
        private readonly Lazy<IReadOnlyCollection<int>> _allowedHotelIds;
        private readonly Lazy<IReadOnlyCollection<int>> _allowedGroupIds;

        public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _roles = new Lazy<IReadOnlyCollection<string>>(() => ReadCsvAndClaims("roles", ClaimTypes.Role));
            _permissions = new Lazy<IReadOnlyCollection<string>>(() => ReadCsvAndClaims("permissions", "permission"));
            _allowedHotelIds = new Lazy<IReadOnlyCollection<int>>(() => ReadIntCsv("allowedHotelIds"));
            _allowedGroupIds = new Lazy<IReadOnlyCollection<int>>(() => ReadIntCsv("allowedGroupIds"));
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
        public int? UserId => ReadIntClaim("userId");
        public int? TenantId => ReadIntClaim("tenantId");
        public string? TenantCode => Principal?.FindFirst("tenantCode")?.Value;
        public string? Username => Principal?.FindFirst("username")?.Value ?? Principal?.Identity?.Name;
        public string AuthMode => Principal?.FindFirst("authMode")?.Value ?? AuthModes.CentralManaged;
        public IReadOnlyCollection<string> Roles => _roles.Value;
        public IReadOnlyCollection<string> Permissions => _permissions.Value;
        public IReadOnlyCollection<int> AllowedHotelIds => _allowedHotelIds.Value;
        public IReadOnlyCollection<int> AllowedGroupIds => _allowedGroupIds.Value;

        public bool HasPermission(string permissionCode)
        {
            return Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
        }

        public bool CanAccessHotel(int tenantId)
        {
            return AllowedHotelIds.Contains(tenantId);
        }

        private int? ReadIntClaim(string claimType)
        {
            var value = Principal?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private IReadOnlyCollection<int> ReadIntCsv(string claimType)
        {
            var value = Principal?.FindFirst(claimType)?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<int>();
            }

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var parsed) ? parsed : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToArray();
        }

        private IReadOnlyCollection<string> ReadCsvAndClaims(string csvClaimType, string repeatedClaimType)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var csv = Principal?.FindFirst(csvClaimType)?.Value;
            if (!string.IsNullOrWhiteSpace(csv))
            {
                foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    values.Add(item);
                }
            }

            if (Principal != null)
            {
                foreach (var claim in Principal.FindAll(repeatedClaimType))
                {
                    if (!string.IsNullOrWhiteSpace(claim.Value))
                    {
                        values.Add(claim.Value);
                    }
                }
            }

            return values.ToArray();
        }
    }
}
