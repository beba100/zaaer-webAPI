#pragma warning disable CS1591

using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public class PmsDepositRowDto
    {
        public int ReceiptId { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal DisplayAmount { get; set; }
        public int? BankId { get; set; }
        public string? BankName { get; set; }
        public string? BankNameAr { get; set; }
        public string? BankNameEn { get; set; }
        public int? PaymentMethodId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public string ReceiptStatus { get; set; } = "paid";
        public string? FirstImageUrl { get; set; }
        public int ImageCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class PmsDepositDetailDto : PmsDepositRowDto
    {
        public IReadOnlyList<PmsDepositImageDto> Images { get; set; } = Array.Empty<PmsDepositImageDto>();
    }

    public sealed class PmsDepositListResultDto
    {
        public IReadOnlyList<PmsDepositRowDto> Items { get; set; } = Array.Empty<PmsDepositRowDto>();
        public PmsDepositSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsDepositSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class PmsDepositBankDto
    {
        public int Id { get; set; }
        public int BankId { get; set; }
        public int? ZaaerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? BankSlug { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class PmsDepositPaymentMethodDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
    }

    public sealed class PmsDepositImageDto
    {
        public int DepositImageId { get; set; }
        /// <summary>Internal route id (<c>payment_receipts.receipt_id</c>) for API.</summary>
        public int ReceiptId { get; set; }
        /// <summary>Stored link id (<c>payment_receipts.zaaer_id</c>).</summary>
        public int ReceiptZaaerId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string? OriginalFilename { get; set; }
        public long? FileSize { get; set; }
        public string? ContentType { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class PmsCreateDepositDto
    {
        [Required]
        public DateTime ReceiptDate { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal AmountPaid { get; set; }

        [Required]
        public int BankZaaerId { get; set; }

        [Required]
        public int PaymentMethodId { get; set; }

        public string? Notes { get; set; }
    }

    public sealed class PmsUpdateDepositDto
    {
        public DateTime? ReceiptDate { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? AmountPaid { get; set; }

        public int? BankZaaerId { get; set; }

        public int? PaymentMethodId { get; set; }

        public string? Notes { get; set; }
    }
}
