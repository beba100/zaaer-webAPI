namespace FinanceLedgerAPI.Enums
{
    /// <summary>
    /// Rental Type for reservation pricing
    /// </summary>
    public enum RentalType
    {
        Daily = 0,
        Monthly = 1,
        Yearly = 2,
        InHour = 3
    }

    /// <summary>
    /// Persisted <c>rental_type</c> values (lowercase snake/word, matches PMS storage).
    /// </summary>
    public static class RentalTypeHelper
    {
        public static string ToStorageValue(RentalType rentalType) => rentalType switch
        {
            RentalType.Daily => "daily",
            RentalType.Monthly => "monthly",
            RentalType.Yearly => "yearly",
            RentalType.InHour => "hourly",
            _ => rentalType.ToString().ToLowerInvariant()
        };

        public static bool TryParseStorage(string? value, out RentalType rentalType)
        {
            rentalType = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var norm = value.Trim().ToLowerInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);

            switch (norm)
            {
                case "daily":
                    rentalType = RentalType.Daily;
                    return true;
                case "monthly":
                    rentalType = RentalType.Monthly;
                    return true;
                case "yearly":
                    rentalType = RentalType.Yearly;
                    return true;
                case "hourly":
                case "inhour":
                    rentalType = RentalType.InHour;
                    return true;
                default:
                    return Enum.TryParse(value, true, out rentalType);
            }
        }
    }
}

