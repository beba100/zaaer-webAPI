#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsHotelBookingsReportDto
    {
        public IReadOnlyList<PmsHotelBookingsReportRowDto> Items { get; set; } = Array.Empty<PmsHotelBookingsReportRowDto>();
        public PmsHotelBookingsReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelBookingsReportSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal TotalRefunded { get; set; }
        public decimal TotalSecurityDeposit { get; set; }
    }

    public sealed class PmsHotelBookingsReportRowDto
    {
        public int ReservationId { get; set; }
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public string? Classifier { get; set; }
        public string? Source { get; set; }
        public string? CustomerName { get; set; }
        public string? CompanyName { get; set; }
        public string? UnitLabel { get; set; }
        public string? Status { get; set; }
        public string? RentalType { get; set; }
        public DateTime? CheckInDate { get; set; }
        public DateTime? CheckOutDate { get; set; }
        public DateTime ReservationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalExtra { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal SecurityDeposit { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Refunded { get; set; }
        public decimal BalanceAmount { get; set; }
    }

    public sealed class PmsHotelDeparturesReportDto
    {
        public IReadOnlyList<PmsHotelDeparturesReportRowDto> Items { get; set; } = Array.Empty<PmsHotelDeparturesReportRowDto>();
        public PmsHotelDeparturesReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelDeparturesReportSummaryDto
    {
        public int Count { get; set; }
    }

    public sealed class PmsHotelDeparturesReportRowDto
    {
        public int ReservationId { get; set; }
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public DateTime DepartureDate { get; set; }
        public string? UnitLabel { get; set; }
        public decimal UnitRentAmount { get; set; }
        public string? RentalType { get; set; }
        public string? CustomerName { get; set; }
        public string? MobileNo { get; set; }
    }

    public sealed class PmsHotelUnitTransfersReportDto
    {
        public IReadOnlyList<PmsHotelUnitTransfersReportRowDto> Items { get; set; } =
            Array.Empty<PmsHotelUnitTransfersReportRowDto>();

        public PmsHotelUnitTransfersReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelUnitTransfersReportSummaryDto
    {
        public int Count { get; set; }
    }

    public sealed class PmsHotelUnitTransfersReportRowDto
    {
        public int SwitchId { get; set; }
        public int ReservationId { get; set; }
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int UnitId { get; set; }
        public int FromApartmentId { get; set; }
        public int ToApartmentId { get; set; }
        public string? FromUnitLabel { get; set; }
        public string? FromRoomTypeName { get; set; }
        public string? ToUnitLabel { get; set; }
        public string? ToRoomTypeName { get; set; }
        public string? ApplyMode { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CustomerName { get; set; }
        public string? CreatedByUserName { get; set; }
    }

    public sealed class PmsHotelOnlineBookingsReportDto
    {
        public IReadOnlyList<PmsHotelOnlineBookingsReportRowDto> Items { get; set; } = Array.Empty<PmsHotelOnlineBookingsReportRowDto>();
        public PmsHotelOnlineBookingsReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelOnlineBookingsReportSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PmsHotelOnlineBookingsSourceSummaryDto> SourceBreakdown { get; set; } =
            Array.Empty<PmsHotelOnlineBookingsSourceSummaryDto>();
    }

    public sealed class PmsHotelOnlineBookingsSourceSummaryDto
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class PmsHotelOnlineBookingsReportRowDto
    {
        public int ReservationId { get; set; }
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public string? Source { get; set; }
        public string? CustomerName { get; set; }
        public int? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class PmsHotelFinanceReportRowDto
    {
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public string? UnitLabel { get; set; }
        public string? CustomerName { get; set; }

        public int DocumentId { get; set; }
        public int? DocumentZaaerId { get; set; }
        public string DocumentNo { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? VoucherCode { get; set; }
        public string? VoucherLabel { get; set; }
        public int? OrderId { get; set; }
        public string? ReceiptType { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? BankName { get; set; }
        public string? LinkedInvoiceNo { get; set; }
        public int? LinkedInvoiceId { get; set; }
        public int? LinkedInvoiceZaaerId { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? AmountRemaining { get; set; }
        public string? CreditType { get; set; }
        public string? ExpenseCategoryName { get; set; }
        public decimal? BeforeTaxAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public string? ApprovalStatus { get; set; }
    }

    public sealed class PmsHotelFinanceReportDto
    {
        public IReadOnlyList<PmsHotelFinanceReportRowDto> Items { get; set; } = Array.Empty<PmsHotelFinanceReportRowDto>();
        public PmsHallFinanceReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelMonthEndClosingReportDto
    {
        public IReadOnlyList<PmsHotelMonthEndClosingReportRowDto> Items { get; set; } =
            Array.Empty<PmsHotelMonthEndClosingReportRowDto>();

        public PmsHotelMonthEndClosingReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHotelMonthEndClosingReportSummaryDto
    {
        public int DayCount { get; set; }
        public decimal RentInsuranceNet { get; set; }
        public decimal CashAmount { get; set; }
        public decimal MadaAmount { get; set; }
        public decimal OtherPaidAmount { get; set; }
        public decimal BankTransferAmount { get; set; }
        public decimal NetExTax { get; set; }
        public decimal DepositsAmount { get; set; }
        public decimal ExpensesAmount { get; set; }
    }

    public sealed class PmsHotelMonthEndClosingReportRowDto
    {
        public DateTime Date { get; set; }
        public decimal RentInsuranceNet { get; set; }
        public decimal CashAmount { get; set; }
        public decimal MadaAmount { get; set; }
        public decimal OtherPaidAmount { get; set; }
        public decimal BankTransferAmount { get; set; }
        public decimal NetExTax { get; set; }
        public decimal DepositsAmount { get; set; }
        public decimal ExpensesAmount { get; set; }
    }
}
