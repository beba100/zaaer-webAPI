namespace zaaerIntegration.Services.Integrations
{
    public static class NtmpApiConstants
    {
        public const string ServiceName = "NTMP";

        public const string ChannelName = "Aleairy PMS";

        public static string ResolveBaseHost(string environment) =>
            (environment ?? "production").Trim().ToLowerInvariant() switch
            {
                "dev" or "development" => "https://dev-api.tourism.sa",
                "staging" or "stage" or "stg" => "https://stg-api.tourism.sa",
                _ => "https://api.tourism.sa"
            };

        public static string CreateOrUpdateBookingUrl(string environment) =>
            $"{ResolveBaseHost(environment)}/gateway/CreateOrUpdateBooking/1.0/createOrUpdateBooking";

        public static string CancelBookingUrl(string environment) =>
            $"{ResolveBaseHost(environment)}/gateway/CancelBooking/1.0/cancelBooking";

        public static string BookingExpenseUrl(string environment) =>
            $"{ResolveBaseHost(environment)}/gateway/BookingExpense/1.0/bookingExpense";

        public static string OccupancyUpdateUrl(string environment) =>
            $"{ResolveBaseHost(environment)}/gateway/OccupancyUpdate/2.0/occupancyUpdate";

        public static string GetTransactionIdUrl(string environment) =>
            $"{ResolveBaseHost(environment)}/gateway/GetTransactionIDByBookingNo/1.0/getTransactionIDByBookingNo";

        public const int TransactionTypeBooking = 1;
        public const int TransactionTypeCheckIn = 2;
        public const int TransactionTypeCheckOut = 3;
    }
}
