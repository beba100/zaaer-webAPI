using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Invoice Journal Entry Tracking - تتبع القيود المحاسبية للفواتير
    /// Tracks journal entries sent to VoM system for invoices
    /// </summary>
    [Table("invoice_journal_entries")]
    public class InvoiceJournalEntry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Invoice ID - معرف الفاتورة (Primary Key)
        /// </summary>
        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        /// <summary>
        /// Invoice Zaaer ID - معرف الفاتورة في Zaaer (used for matching with credit_notes.invoice_id)
        /// This is the zaaer_id from invoices table, used to validate reverse journal entries for credit notes
        /// </summary>
        [Column("invoice_zaaer_id")]
        public int? InvoiceZaaerId { get; set; }

        /// <summary>
        /// VoM Journal Entry ID - معرف القيد المحاسبي في VoM
        /// </summary>
        [Column("vom_journal_entry_id")]
        public int? VomJournalEntryId { get; set; }

        /// <summary>
        /// Journal Entry Code - رقم القيد (same as InvoiceNo)
        /// </summary>
        [Required]
        [Column("journal_entry_code")]
        [MaxLength(50)]
        public string JournalEntryCode { get; set; } = string.Empty;

        /// <summary>
        /// Journal Date - تاريخ القيد
        /// </summary>
        [Required]
        [Column("journal_date")]
        public DateTime JournalDate { get; set; }

        /// <summary>
        /// Total Debit Amount - إجمالي المدين
        /// </summary>
        [Required]
        [Column("total_debit", TypeName = "decimal(12,2)")]
        public decimal TotalDebit { get; set; }

        /// <summary>
        /// Total Credit Amount - إجمالي الدائن
        /// </summary>
        [Required]
        [Column("total_credit", TypeName = "decimal(12,2)")]
        public decimal TotalCredit { get; set; }

        /// <summary>
        /// Status - حالة الإرسال
        /// Values: Pending, Sent, Failed, Cancelled
        /// </summary>
        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// VoM Response - استجابة VoM API
        /// </summary>
        [Column("vom_response", TypeName = "nvarchar(max)")]
        public string? VomResponse { get; set; }

        /// <summary>
        /// Error Message - رسالة الخطأ (if failed)
        /// </summary>
        [Column("error_message", TypeName = "nvarchar(1000)")]
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Retry Count - عدد محاولات الإعادة
        /// </summary>
        [Column("retry_count")]
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Last Retry At - تاريخ آخر محاولة
        /// </summary>
        [Column("last_retry_at")]
        public DateTime? LastRetryAt { get; set; }

        /// <summary>
        /// Created At - تاريخ الإنشاء
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Updated At - تاريخ آخر تحديث
        /// </summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("InvoiceId")]
        public Invoice Invoice { get; set; } = null!;
    }
}
