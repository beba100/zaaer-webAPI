using System.Text.Json.Serialization;

namespace zaaerIntegration.Services.Integrations
{
    public sealed class NtmpCreateOrUpdateBookingRequest
    {
        [JsonPropertyName("bookingNo")] public string BookingNo { get; set; } = string.Empty;
        [JsonPropertyName("nationalityCode")] public string NationalityCode { get; set; } = "630";
        [JsonPropertyName("checkInDate")] public string CheckInDate { get; set; } = string.Empty;
        [JsonPropertyName("checkOutDate")] public string CheckOutDate { get; set; } = string.Empty;
        [JsonPropertyName("totalDurationDays")] public string TotalDurationDays { get; set; } = "1";
        [JsonPropertyName("allotedRoomNo")] public string AllotedRoomNo { get; set; } = "1";
        [JsonPropertyName("roomRentType")] public string RoomRentType { get; set; } = "1";
        [JsonPropertyName("dailyRoomRate")] public string DailyRoomRate { get; set; } = "0";
        [JsonPropertyName("totalRoomRate")] public string TotalRoomRate { get; set; } = "0";
        [JsonPropertyName("vat")] public string Vat { get; set; } = "0";
        [JsonPropertyName("municipalityTax")] public string MunicipalityTax { get; set; } = "0";
        [JsonPropertyName("discount")] public string Discount { get; set; } = "0";
        [JsonPropertyName("grandTotal")] public string GrandTotal { get; set; } = "0";
        [JsonPropertyName("transactionTypeId")] public string TransactionTypeId { get; set; } = "1";
        [JsonPropertyName("gender")] public string Gender { get; set; } = "0";
        [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
        [JsonPropertyName("checkInTime")] public string CheckInTime { get; set; } = "0";
        [JsonPropertyName("checkOutTime")] public string CheckOutTime { get; set; } = "0";
        [JsonPropertyName("customerType")] public string CustomerType { get; set; } = "3";
        [JsonPropertyName("noOfGuest")] public string NoOfGuest { get; set; } = "1";
        [JsonPropertyName("roomType")] public string RoomType { get; set; } = "2";
        [JsonPropertyName("purposeOfVisit")] public string PurposeOfVisit { get; set; } = "7";
        [JsonPropertyName("dateOfBirth")] public string DateOfBirth { get; set; } = "0";
        [JsonPropertyName("paymentType")] public string PaymentType { get; set; } = "1";
        [JsonPropertyName("noOfRooms")] public string NoOfRooms { get; set; } = "1";
        [JsonPropertyName("channel")] public string Channel { get; set; } = NtmpApiConstants.ChannelName;
        [JsonPropertyName("cuFlag")] public string CuFlag { get; set; } = "1";
    }

    public sealed class NtmpCancelBookingRequest
    {
        [JsonPropertyName("transactionId")] public string TransactionId { get; set; } = string.Empty;
        [JsonPropertyName("cancelReason")] public string CancelReason { get; set; } = "1";
        [JsonPropertyName("cancelWithCharges")] public string CancelWithCharges { get; set; } = "0";
        [JsonPropertyName("chargeableDays")] public string ChargeableDays { get; set; } = "0";
        [JsonPropertyName("roomRentType")] public string RoomRentType { get; set; } = "1";
        [JsonPropertyName("dailyRoomRate")] public string DailyRoomRate { get; set; } = "0";
        [JsonPropertyName("totalRoomRate")] public string TotalRoomRate { get; set; } = "0";
        [JsonPropertyName("vat")] public string Vat { get; set; } = "0";
        [JsonPropertyName("municipalityTax")] public string MunicipalityTax { get; set; } = "0";
        [JsonPropertyName("discount")] public string Discount { get; set; } = "0";
        [JsonPropertyName("grandTotal")] public string GrandTotal { get; set; } = "0";
        [JsonPropertyName("paymentType")] public string PaymentType { get; set; } = "1";
        [JsonPropertyName("cuFlag")] public string CuFlag { get; set; } = "1";
        [JsonPropertyName("channel")] public string Channel { get; set; } = NtmpApiConstants.ChannelName;
    }

    public sealed class NtmpBookingExpenseRequest
    {
        [JsonPropertyName("transactionId")] public string TransactionId { get; set; } = string.Empty;
        [JsonPropertyName("channel")] public string Channel { get; set; } = NtmpApiConstants.ChannelName;
        [JsonPropertyName("expenseItems")] public List<NtmpExpenseItem> ExpenseItems { get; set; } = new();
    }

    public sealed class NtmpExpenseItem
    {
        [JsonPropertyName("expenseDate")] public string ExpenseDate { get; set; } = string.Empty;
        [JsonPropertyName("itemNumber")] public string ItemNumber { get; set; } = string.Empty;
        [JsonPropertyName("expenseTypeId")] public string ExpenseTypeId { get; set; } = "1";
        [JsonPropertyName("unitPrice")] public string UnitPrice { get; set; } = "0";
        [JsonPropertyName("discount")] public string Discount { get; set; } = "0";
        [JsonPropertyName("vat")] public string Vat { get; set; } = "0";
        [JsonPropertyName("municipalityTax")] public string MunicipalityTax { get; set; } = "0";
        [JsonPropertyName("grandTotal")] public string GrandTotal { get; set; } = "0";
        [JsonPropertyName("paymentType")] public string PaymentType { get; set; } = "1";
        [JsonPropertyName("cuFlag")] public string CuFlag { get; set; } = "1";
    }

    public sealed class NtmpOccupancyUpdateRequest
    {
        [JsonPropertyName("updateDate")] public string UpdateDate { get; set; } = string.Empty;
        [JsonPropertyName("roomsOccupied")] public string RoomsOccupied { get; set; } = "0";
        [JsonPropertyName("roomsAvailable")] public string RoomsAvailable { get; set; } = "0";
        [JsonPropertyName("roomsBooked")] public string RoomsBooked { get; set; } = "0";
        [JsonPropertyName("roomsOnMaintenance")] public string RoomsOnMaintenance { get; set; } = "0";
        [JsonPropertyName("totalRooms")] public string TotalRooms { get; set; } = "1";
        /// <summary>Required by OccupancyUpdate v2.0 (NTMP error 18 if missing/invalid).</summary>
        [JsonPropertyName("channel")] public string Channel { get; set; } = NtmpApiConstants.ChannelName;
        [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
        [JsonPropertyName("dayClosing")] public string? DayClosing { get; set; }
        [JsonPropertyName("totalAdults")] public string? TotalAdults { get; set; }
        [JsonPropertyName("totalChildren")] public string? TotalChildren { get; set; }
        [JsonPropertyName("totalGuests")] public string? TotalGuests { get; set; }
    }

    public sealed class NtmpGetTransactionIdRequest
    {
        [JsonPropertyName("bookingNo")] public List<string> BookingNo { get; set; } = new();
    }

    public sealed class NtmpGatewayResponse
    {
        public bool Success { get; set; }
        public int HttpStatusCode { get; set; }
        public string? CorrelationId { get; set; }
        public string? TransactionId { get; set; }
        public string? RawResponse { get; set; }
        public string? RawRequest { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> ErrorCodes { get; set; } = new();
    }
}
