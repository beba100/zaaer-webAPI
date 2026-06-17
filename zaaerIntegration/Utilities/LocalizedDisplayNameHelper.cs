using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    public static class LocalizedDisplayNameHelper
    {
        public static string CombineName(string? first, string? last)
        {
            return $"{first} {last}".Trim();
        }

        public static string? UserFullNameEn(MasterRbacUser user)
        {
            var en = CombineName(user.FirstNameEn, user.LastNameEn);
            return string.IsNullOrWhiteSpace(en) ? null : en;
        }

        public static string? TenantNameEn(Tenant tenant)
        {
            return string.IsNullOrWhiteSpace(tenant.NameEn) ? null : tenant.NameEn.Trim();
        }
    }
}
