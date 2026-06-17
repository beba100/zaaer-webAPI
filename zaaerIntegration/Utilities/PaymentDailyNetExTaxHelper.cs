using zaaerIntegration.Configuration;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Shared net ex-tax rules for payment daily report and target achievement.
    /// </summary>
    public static class PaymentDailyNetExTaxHelper
    {
        public const decimal TaxVatFactor = 1.15m;
        public const decimal TaxGrossWithVatAndLodging = 1.17875m;

        public static bool RowUsesVatOnlyNetExTax(
            string? tenantCode,
            int? tenantId,
            int? zaaerId,
            PaymentDailyNetExTaxOptions options,
            bool isManual = false)
        {
            if (isManual)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(tenantCode)
                && options.VatOnlyHotelCodes.Any(code =>
                    string.Equals(code, tenantCode, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (tenantId is > 0 && options.VatOnlyTenantIds.Contains(tenantId.Value))
            {
                return true;
            }

            if (zaaerId is > 0 && options.VatOnlyZaaerIds.Contains(zaaerId.Value))
            {
                return true;
            }

            return false;
        }

        public static decimal CalcNetExTax(decimal totalNet, bool usesVatOnly)
        {
            if (totalNet == 0m)
            {
                return 0m;
            }

            var divisor = usesVatOnly ? TaxVatFactor : TaxGrossWithVatAndLodging;
            return Math.Round(totalNet / divisor, 2, MidpointRounding.AwayFromZero);
        }
    }
}
