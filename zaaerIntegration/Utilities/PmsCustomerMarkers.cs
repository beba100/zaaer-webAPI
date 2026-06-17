using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// System-owned customer rows used only to satisfy FK while a reservation draft has no guest yet.
    /// </summary>
    public static class PmsCustomerMarkers
    {
        public const string DraftPlaceholderComments = "pms:draft-placeholder";

        public const string DraftPlaceholderCustomerNo = "PMS-DRAFT";

        public static bool IsDraftPlaceholder(Customer? customer) =>
            customer != null && IsDraftPlaceholderComments(customer.Comments);

        public static bool IsDraftPlaceholderComments(string? comments) =>
            string.Equals(comments?.Trim(), DraftPlaceholderComments, StringComparison.Ordinal);

        public static IQueryable<Customer> ExcludeDraftPlaceholders(IQueryable<Customer> query) =>
            query.Where(c => c.Comments != DraftPlaceholderComments);
    }
}
