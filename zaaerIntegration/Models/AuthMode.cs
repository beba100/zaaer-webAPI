namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Defines where a tenant resolves users, roles, and permissions from.
    /// </summary>
    public static class AuthModes
    {
        public const string CentralManaged = "CentralManaged";
        public const string LocalManaged = "LocalManaged";
        public const string Hybrid = "Hybrid";

        public static bool IsValid(string? value)
        {
            return value == CentralManaged || value == LocalManaged || value == Hybrid;
        }
    }
}
