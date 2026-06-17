namespace zaaerIntegration.Configuration
{
    /// <summary>
    /// VAT-only exception lists for payment daily net ex-tax (÷ 1.15 vs ÷ 1.17875).
    /// </summary>
    public sealed class PaymentDailyNetExTaxOptions
    {
        public const string SectionName = "Reports:PaymentDailyNetExTax";

        public List<string> VatOnlyHotelCodes { get; set; } = new();

        public List<int> VatOnlyTenantIds { get; set; } = new();

        public List<int> VatOnlyZaaerIds { get; set; } = new();
    }
}
