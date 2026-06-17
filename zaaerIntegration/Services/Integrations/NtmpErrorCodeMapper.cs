namespace zaaerIntegration.Services.Integrations
{
    /// <summary>Human-readable NTMP gateway error codes (MT Integration Guide v2.13).</summary>
    public static class NtmpErrorCodeMapper
    {
        public const string ApiCreateOrUpdate = "CreateOrUpdateBooking";
        public const string ApiCancelBooking = "CancelBooking";
        public const string ApiBookingExpense = "BookingExpense";
        public const string ApiOccupancyUpdate = "OccupancyUpdate";

        public static string Describe(string? code, string api = ApiOccupancyUpdate) =>
            code switch
            {
                null or "" => "Unknown NTMP error.",
                "0" => "Success",
                _ when string.Equals(api, ApiOccupancyUpdate, StringComparison.OrdinalIgnoreCase)
                    => DescribeOccupancy(code),
                _ when string.Equals(api, ApiBookingExpense, StringComparison.OrdinalIgnoreCase)
                    => DescribeBookingExpense(code),
                _ when string.Equals(api, ApiCreateOrUpdate, StringComparison.OrdinalIgnoreCase)
                    => DescribeCreateOrUpdate(code),
                _ when string.Equals(api, ApiCancelBooking, StringComparison.OrdinalIgnoreCase)
                    => DescribeCancelBooking(code),
                _ => $"NTMP error {code}."
            };

        public static string DescribeMany(IEnumerable<string> codes, string api = ApiOccupancyUpdate)
        {
            var list = codes.Where(c => !string.IsNullOrWhiteSpace(c) && c != "0").ToList();
            if (list.Count == 0)
            {
                return "Success";
            }

            return string.Join("; ", list.Select(c => $"{c}: {Describe(c, api)}"));
        }

        private static string DescribeOccupancy(string code) => code switch
        {
            "1" => "Invalid updateDate (YYYYMMDD, Gregorian).",
            "2" => "Invalid roomsOccupied.",
            "3" => "Invalid roomsAvailable.",
            "4" => "Invalid roomsBooked.",
            "5" => "Invalid roomsOnMaintenance.",
            "6" => "Invalid userId.",
            "7" => "Invalid or unknown transactionId.",
            "8" => "Invalid dayClosing (must be true or false).",
            "9" => "Invalid totalRooms (cannot be 0).",
            "10" => "totalRooms must equal occupied + available + booked + maintenance.",
            "11" => "Invalid totalAdults.",
            "12" => "Invalid totalChildren.",
            "13" => "Invalid totalGuests.",
            "14" => "totalGuests must equal totalAdults + totalChildren.",
            "15" => "totalGuests required when dayClosing=true.",
            "16" => "Invalid totalRevenue.",
            "17" => "totalRevenue required when dayClosing=true.",
            "18" => "Channel name missing or invalid. Register channel with NTMP (e.g. Aleairy PMS).",
            "100" => "Invalid credentials (API key / username / password).",
            "101" => "NTMP internal server error.",
            _ => $"NTMP OccupancyUpdate error {code}."
        };

        private static string DescribeBookingExpense(string code) => code switch
        {
            "1" => "Invalid transactionId (GUID not found in MT).",
            "2" => "Invalid expenseDate (YYYYMMDD, Gregorian). Must be between check-in and check-out.",
            "3" => "Invalid itemNumber (numeric, unique per transactionId and userId).",
            "4" => "itemNumber not found in MT (valid when cuFlag=2 update).",
            "5" => "Invalid expenseTypeId.",
            "6" => "Invalid unitPrice.",
            "7" => "Invalid discount.",
            "8" => "Invalid VAT (amount only).",
            "9" => "Invalid municipalityTax.",
            "10" => "Invalid grandTotal or formula mismatch.",
            "12" => "Invalid paymentType.",
            "13" => "No checkout data for transactionId. Call BookingExpense only after checkout is posted to NTMP.",
            "14" => "Invalid cuFlag (1=Add, 2=Update).",
            "15" => "Duplicate itemNumber for transactionId. Resend with cuFlag=2 to update.",
            "16" => "Channel name missing or invalid.",
            "100" => "Invalid credentials.",
            "101" => "NTMP internal server error.",
            _ => $"NTMP BookingExpense error {code}."
        };

        private static string DescribeCreateOrUpdate(string code) => code switch
        {
            "1" => "Invalid bookingNo.",
            "2" => "Invalid nationalityCode.",
            "3" => "Invalid checkInDate.",
            "4" => "Invalid checkOutDate.",
            "5" => "Invalid totalDurationDays.",
            "6" => "Invalid allotedRoomNo.",
            "7" => "Invalid roomRentType.",
            "8" => "Invalid dailyRoomRate.",
            "9" => "Invalid totalRoomRate.",
            "10" => "Invalid VAT.",
            "11" => "Invalid municipalityTax.",
            "12" => "Invalid discount.",
            "13" => "Invalid grandTotal.",
            "14" => "Grand total formula mismatch.",
            "15" => "Invalid transactionTypeId (1=Booking, 2=CheckIn, 3=CheckOut).",
            "16" => "Invalid gender.",
            "17" => "Invalid userId.",
            "18" => "Invalid transactionId (GUID not found in MT).",
            "19" => "Invalid checkInTime (HHMMSS or 0 for booking).",
            "20" => "Invalid checkOutTime (HHMMSS or 0 until checkout).",
            "21" => "Invalid customerType.",
            "22" => "Invalid noOfGuest.",
            "23" => "Invalid roomType.",
            "24" => "Invalid purposeOfVisit.",
            "25" => "Invalid paymentType.",
            "26" => "Invalid noOfRooms.",
            "27" => "Invalid cuFlag (1=Create, 2=Update).",
            "28" => "Invalid dateOfBirth (0 or YYYYMMDD).",
            "29" => "TransactionId and transactionTypeId already exist. Resend with cuFlag=2 to update.",
            "30" => "Updates not allowed because checkout already exists for this transactionId.",
            "31" => "Booking updates not allowed because check-in already exists.",
            "32" => "Updates not allowed because booking is already cancelled.",
            "33" => "Update requested but record does not exist in MT. Create first (cuFlag=1) before updating (cuFlag=2).",
            "34" => "bookingNo and userId combination must be unique for new entries.",
            "35" => "Channel name missing or invalid.",
            "100" => "Invalid credentials.",
            "101" => "NTMP internal server error.",
            _ => $"NTMP CreateOrUpdateBooking error {code}."
        };

        private static string DescribeCancelBooking(string code) => code switch
        {
            "1" => "Invalid transactionId.",
            "2" => "Invalid cancelReason.",
            "3" => "Invalid cancelWithCharges.",
            "4" => "Invalid chargeableDays.",
            "5" => "Invalid roomRentType.",
            "6" => "Invalid dailyRoomRate.",
            "7" => "Invalid totalRoomRate.",
            "8" => "Invalid VAT.",
            "9" => "Invalid municipalityTax.",
            "10" => "Invalid discount.",
            "11" => "Invalid grandTotal.",
            "12" => "Invalid paymentType.",
            "13" => "Invalid cuFlag.",
            "14" => "Booking already cancelled.",
            "15" => "Cannot cancel after checkout.",
            "16" => "Channel name missing or invalid.",
            "100" => "Invalid credentials.",
            "101" => "NTMP internal server error.",
            _ => $"NTMP CancelBooking error {code}."
        };
    }
}
