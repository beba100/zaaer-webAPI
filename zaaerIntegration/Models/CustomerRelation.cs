using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Maps to <c>dbo.customer_relations</c> — relationship labels for companions (e.g. Son, Wife).
    /// </summary>
    [Table("customer_relations")]
    public class CustomerRelation
    {
        /// <summary>Column <c>cr_id</c> (PK).</summary>
        [Key]
        [Column("cr_id")]
        public int CrId { get; set; }

        /// <summary>Column <c>cr_name</c> (English label).</summary>
        [Required]
        [StringLength(100)]
        [Column("cr_name")]
        public string CrName { get; set; } = string.Empty;

        /// <summary>Column <c>cr_name_ar</c> (Arabic label).</summary>
        [Required]
        [StringLength(100)]
        [Column("cr_name_ar")]
        public string CrNameAr { get; set; } = string.Empty;
    }
}
