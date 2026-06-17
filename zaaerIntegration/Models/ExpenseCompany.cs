using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Models
{
    /// <summary>
    /// Supplier company linked to a taxable expense (expense_companies).
    /// </summary>
    [Table("expense_companies")]
    public class ExpenseCompany
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("tax_number")]
        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [Column("company_name")]
        [MaxLength(300)]
        public string? CompanyName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("company_id")]
        public int? CompanyId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("expense_id")]
        public long ExpenseId { get; set; }

        /// <summary>Legacy link — mirrors expenses.old_expense_id (NOT NULL on tenant DBs).</summary>
        [Column("old_expense_id")]
        public int OldExpenseId { get; set; }

        [ForeignKey(nameof(ExpenseId))]
        public Expense Expense { get; set; } = null!;
    }
}
