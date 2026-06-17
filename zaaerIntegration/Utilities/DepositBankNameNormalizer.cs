using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Maps tenant <c>banks</c> rows to legacy <c>payment_receipts.bank_name</c> slugs.
    /// </summary>
    public static class DepositBankNameNormalizer
    {
        public const string Bilad = "bilad";
        public const string Riyad = "riyad";

        public static string? FromBank(Bank bank)
        {
            if (bank == null)
            {
                return null;
            }

            return FromNames(bank.BankNameAr, bank.BankNameEn, bank.BankCode);
        }

        public static string? FromNames(string? bankNameAr, string? bankNameEn, string? bankCode = null)
        {
            foreach (var candidate in new[] { bankNameAr, bankNameEn, bankCode })
            {
                var slug = FromSingleName(candidate);
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return slug;
                }
            }

            return null;
        }

        public static string? FromSingleName(string? bankName)
        {
            if (string.IsNullOrWhiteSpace(bankName))
            {
                return null;
            }

            var trimmed = bankName.Trim();

            if (trimmed.Equals("بنك البلاد", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("بنك البلاد", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("البلاد", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(Bilad, StringComparison.OrdinalIgnoreCase))
            {
                return Bilad;
            }

            if (trimmed.Equals("بنك الرياض", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("بنك الرياض", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("الرياض", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(Riyad, StringComparison.OrdinalIgnoreCase))
            {
                return Riyad;
            }

            return null;
        }
    }
}
