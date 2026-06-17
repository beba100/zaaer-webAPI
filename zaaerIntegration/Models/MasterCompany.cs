using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Master DB supplier / corporate company (Companies table).
    /// </summary>
    public class MasterCompany
    {
        public int Id { get; set; }

        public string? TaxNumber { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public string? CountryCode { get; set; }

        public string? Mobile { get; set; }

        public string? Email { get; set; }

        public string? Street { get; set; }

        public string? City { get; set; }

        public string? Country { get; set; }

        public string? PostalCode { get; set; }
    }
}
