using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsHallEventLookupsDto
    {
        public int HotelId { get; set; }
        public bool IsHall { get; set; }
        public string PropertyType { get; set; } = "hotel";
        public List<string> EventTypes { get; set; } = new();
        public List<PmsLookupOptionDto> EventStatuses { get; set; } = new();
        public List<string> GenderTypes { get; set; } = new();
        public List<string> VenueKinds { get; set; } = new();
        public List<string> PreparationStatuses { get; set; } = new();
        public List<string> PackagePriceTypes { get; set; } = new();
        public List<string> PackageCategories { get; set; } = new();
        public List<PmsHallListItemDto> Halls { get; set; } = new();
    }

    public sealed class PmsLookupOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string LabelEn { get; set; } = string.Empty;
        public string LabelAr { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public sealed class PmsHallListItemDto
    {
        public int HallId { get; set; }
        public int? ZaaerId { get; set; }
        public string HallCode { get; set; } = string.Empty;
        public string? HallName { get; set; }
        public int? RoomTypeId { get; set; }
        public string? RoomTypeName { get; set; }
        public string? HallGenderType { get; set; }
        public int? HallCapacity { get; set; }
        public string? PreparationStatus { get; set; }
        public bool IsActive { get; set; }
    }

    public class PmsHallEventListItemDto
    {
        public int ReservationId { get; set; }
        public int? ZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? HallId { get; set; }
        public string? HallName { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string? EventDateHijri { get; set; }
        public string? EventDateHijriDisplay { get; set; }
        public string EventStartTime { get; set; } = string.Empty;
        public string EventEndTime { get; set; } = string.Empty;
        public int ExpectedGuests { get; set; }
        public int? ActualGuests { get; set; }
        public string? OccasionName { get; set; }
        public string? OccasionOwner { get; set; }
        public string EventStatus { get; set; } = string.Empty;
        public string EventStatusLabelEn { get; set; } = string.Empty;
        public string EventStatusLabelAr { get; set; } = string.Empty;
        public string EventStatusColor { get; set; } = string.Empty;
        public decimal DepositAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal TotalAmount { get; set; }
        public bool ContractSigned { get; set; }
        public string ReservationStatus { get; set; } = string.Empty;
        public List<string> AllowedTransitions { get; set; } = new();
    }

    public sealed class PmsHallEventDetailDto : PmsHallEventListItemDto
    {
        public string? OccasionOwner { get; set; }
        public string? CompletionNotes { get; set; }
        public DateTime? DepositDueAt { get; set; }
        public DateTime? ContractSignedAt { get; set; }
        public PmsFunctionSheetDto? FunctionSheet { get; set; }
    }

    public sealed class PmsCreateHallEventDto
    {
        public int? CustomerId { get; set; }

        [Required]
        public int HallId { get; set; }

        [Required]
        public string EventType { get; set; } = "wedding";

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        public string EventStartTime { get; set; } = "18:00";

        [Required]
        public string EventEndTime { get; set; } = "23:00";

        public int ExpectedGuests { get; set; }
        public string? OccasionName { get; set; }
        public string? OccasionOwner { get; set; }
        public decimal HallRentAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public DateTime? DepositDueAt { get; set; }
        public string? Notes { get; set; }
        public List<PmsHallEventPackageLineDto> Packages { get; set; } = new();
    }

    public sealed class PmsHallEventPackageLineDto
    {
        public int? PackageId { get; set; }
        public string? Name { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string PriceType { get; set; } = "fixed";
    }

    public sealed class PmsUpdateHallEventScheduleDto
    {
        [Required]
        public DateTime EventDate { get; set; }

        public string? EventDateHijri { get; set; }

        public string? EventStartTime { get; set; }

        public string? EventEndTime { get; set; }
    }

    public sealed class PmsUpdateHallEventDto
    {
        public string? EventType { get; set; }
        public DateTime? EventDate { get; set; }
        public string? EventDateHijri { get; set; }
        public string? EventStartTime { get; set; }
        public string? EventEndTime { get; set; }
        public int? ExpectedGuests { get; set; }
        public string? OccasionName { get; set; }
        public int? HallId { get; set; }
        public decimal? HallRentAmount { get; set; }
        public decimal? DepositAmount { get; set; }
    }

    public sealed class PmsTransitionHallEventStatusDto
    {
        [Required]
        public string EventStatus { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? ActualGuests { get; set; }
    }

    public sealed class PmsRecordHallDepositDto
    {
        [Required]
        public decimal Amount { get; set; }

        [Required]
        public int PaymentMethodId { get; set; }

        public int? BankId { get; set; }
        public string? TransactionNo { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class PmsHallSchedulerItemDto
    {
        public int ReservationId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int HallId { get; set; }
        public string HallName { get; set; } = string.Empty;
        public string EventStatus { get; set; } = string.Empty;
        public string EventStatusColor { get; set; } = string.Empty;
    }

    public sealed class PmsHallDashboardDto
    {
        public int TodayCount { get; set; }
        public int TomorrowCount { get; set; }
        public int WeekCount { get; set; }
        public int LatePaymentCount { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal DepositsTotal { get; set; }
        public decimal RemainingBalanceTotal { get; set; }
        public List<PmsHallEventListItemDto> TodayEvents { get; set; } = new();
        public List<PmsHallEventListItemDto> TomorrowEvents { get; set; } = new();
        public List<PmsHallEventListItemDto> UpcomingEvents { get; set; } = new();
        public List<PmsHallEventListItemDto> LatePayments { get; set; } = new();
        public List<PmsHallEventAlertDto> Alerts { get; set; } = new();
    }

    public sealed class PmsHallEventAlertDto
    {
        public int AlertId { get; set; }
        public int ReservationId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public bool IsRead { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class PmsHallOccupancyCardDto
    {
        public int HallId { get; set; }
        public string HallCode { get; set; } = string.Empty;
        public string? HallName { get; set; }
        public string? PreparationStatus { get; set; }
        public PmsHallEventListItemDto? CurrentEvent { get; set; }
        public PmsHallEventListItemDto? NextEvent { get; set; }
        public string? OccupancyState { get; set; }
        public string? TimeRemainingLabel { get; set; }
        public string? NextEventLabel { get; set; }
    }

    public sealed class PmsCompleteHallEventDto
    {
        public bool EventCompleted { get; set; }
        public bool HallDelivered { get; set; }
        public bool NoIssues { get; set; }
        public int? ActualGuests { get; set; }
        public string? CompletionNotes { get; set; }
    }

    public sealed class PmsUpdateHallPreparationDto
    {
        [Required]
        public string PreparationStatus { get; set; } = string.Empty;
    }

    public sealed class PmsFunctionSheetDto
    {
        public int? FunctionSheetId { get; set; }
        public int ReservationId { get; set; }
        public int? HallId { get; set; }
        public DateTime EventDate { get; set; }
        public string? HallOpenTime { get; set; }
        public string? GuestArrivalTime { get; set; }
        public string? ServiceStartTime { get; set; }
        public string? CoffeeType { get; set; }
        public string? MenuNotes { get; set; }
        public string? DecorationNotes { get; set; }
        public string? SoundAvNotes { get; set; }
        public int? CoordinatorUserId { get; set; }
        public string? ClientSpecialRequests { get; set; }
        public string ExecutionStatus { get; set; } = "draft";
        public DateTime? PrintedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public List<PmsFunctionSheetItemDto> Items { get; set; } = new();
    }

    public sealed class PmsFunctionSheetItemDto
    {
        public int? ItemId { get; set; }
        public string? Category { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PmsHallDailyEventsReportDto
    {
        public DateTime ReportDate { get; set; }
        public List<PmsHallEventListItemDto> Events { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalBalanceDue { get; set; }
    }

    public sealed class PmsHallUtilizationReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<PmsHallUtilizationLineDto> Lines { get; set; } = new();
    }

    public sealed class PmsHallUtilizationLineDto
    {
        public int HallId { get; set; }
        public string HallName { get; set; } = string.Empty;
        public decimal BookedHours { get; set; }
        public decimal AvailableHours { get; set; }
        public decimal UtilizationPercent { get; set; }
        public int EventCount { get; set; }
    }

    public sealed class PmsHallEventSettlementDto
    {
        public int ReservationId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal DisbursedAmount { get; set; }
        public decimal BalanceDue { get; set; }
        public decimal ReservationAmountPaid { get; set; }
        public decimal ReservationBalanceAmount { get; set; }
        public bool CanClose { get; set; }
    }

    public sealed class PmsHallUnpaidBalanceItemDto
    {
        public int ReservationId { get; set; }
        public int? ZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? HallName { get; set; }
        public DateTime EventDate { get; set; }
        public string EventStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal DisbursedAmount { get; set; }
        public decimal BalanceDue { get; set; }
    }

    public sealed class PmsHallUnpaidBalancesPageDto
    {
        public int TotalCount { get; set; }
        public List<PmsHallUnpaidBalanceItemDto> Items { get; set; } = new();
    }
}
