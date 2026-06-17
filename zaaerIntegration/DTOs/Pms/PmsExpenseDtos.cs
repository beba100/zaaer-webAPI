#pragma warning disable CS1591

using System.ComponentModel.DataAnnotations;
using zaaerIntegration.DTOs.Expense;

namespace zaaerIntegration.DTOs.Pms
{
    /// <summary>
    /// Expense row for PMS grids (hotel scope via <c>X-Hotel-Code</c>).
    /// </summary>
    public class PmsExpenseRowDto
    {
        public long ExpenseId { get; set; }
        public string ExpenseNo { get; set; } = string.Empty;
        public int ExpenseSeq { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Comment { get; set; }
        public int? ExpenseCategoryId { get; set; }
        public string? ExpenseCategoryName { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? BeforeTaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool HasTax => TaxAmount.HasValue && TaxAmount.Value > 0;
        public string ApprovalStatus { get; set; } = "pending";
        public string? PaymentSource { get; set; }
        public string? HotelName { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>First attachment URL for grid link column.</summary>
        public string? FirstImageUrl { get; set; }

        public int ImageCount { get; set; }

        public string Number => ExpenseNo;
        public DateTime Date => DateTime;
        public decimal Amount => TotalAmount;
        public string Status => ApprovalStatus;
    }

    /// <summary>
    /// Full expense detail for PMS forms (includes rooms, company, images).
    /// </summary>
    public sealed class PmsExpenseDetailDto : PmsExpenseRowDto
    {
        public int? ApprovedBy { get; set; }
        public string? ApprovedByFullName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
        public List<ExpenseRoomResponseDto> ExpenseRooms { get; set; } = new();
        public PmsExpenseCompanyDto? Company { get; set; }
        public List<PmsExpenseImageDto> Images { get; set; } = new();
    }

    public sealed class PmsExpenseCategoryDto
    {
        public int ExpenseCategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public sealed class PmsExpenseCompanyDto
    {
        public int? Id { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [MaxLength(300)]
        public string? CompanyName { get; set; }

        public int? CompanyId { get; set; }
    }

    public sealed class PmsExpenseImageDto
    {
        public int ExpenseImageId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string? OriginalFilename { get; set; }
        public long? FileSize { get; set; }
        public string? ContentType { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class PmsExpenseTaxConfigDto
    {
        public decimal VatRate { get; set; }
        public bool VatTaxIncluded { get; set; } = true;
    }

    public sealed class PmsExpenseSummaryDto
    {
        public decimal TotalAmount { get; set; }
        public decimal BeforeTaxAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public int Count { get; set; }
    }

    public sealed class PmsExpenseListResultDto
    {
        public List<PmsExpenseRowDto> Items { get; set; } = new();
        public PmsExpenseSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsMasterCompanyRowDto
    {
        public int Id { get; set; }
        public string? TaxNumber { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }

    public sealed class PmsUpsertMasterCompanyDto
    {
        [Required]
        [MaxLength(300)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TaxNumber { get; set; } = string.Empty;
    }

    public sealed class PmsCreateExpenseDto
    {
        [Required]
        public DateTime DateTime { get; set; }

        public DateTime? DueDate { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        public int? ExpenseCategoryId { get; set; }

        public bool HasTax { get; set; }

        [Range(0, 100)]
        public decimal? TaxRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? TaxAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? BeforeTaxAmount { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public List<CreateExpenseRoomDto>? ExpenseRooms { get; set; }

        [MaxLength(20)]
        public string? PaymentSource { get; set; }

        public PmsExpenseCompanyDto? Company { get; set; }
    }

    public sealed class PmsUpdateExpenseDto
    {
        public DateTime? DateTime { get; set; }
        public DateTime? DueDate { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        public int? ExpenseCategoryId { get; set; }

        public bool? HasTax { get; set; }

        [Range(0, 100)]
        public decimal? TaxRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? TaxAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? BeforeTaxAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? TotalAmount { get; set; }

        public List<CreateExpenseRoomDto>? ExpenseRooms { get; set; }

        [MaxLength(30)]
        public string? ApprovalStatus { get; set; }

        [MaxLength(20)]
        public string? PaymentSource { get; set; }

        public PmsExpenseCompanyDto? Company { get; set; }
    }

    public sealed class PmsApproveExpenseRequestDto
    {
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = string.Empty;

        public string? RejectionReason { get; set; }
        public string? Recommendation { get; set; }
        public int? RecommendationToUserId { get; set; }
    }
}
