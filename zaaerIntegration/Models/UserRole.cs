using System.ComponentModel.DataAnnotations;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// UserRole Model - ربط المستخدم بالدور
    /// </summary>
    public class UserRole
    {
        /// <summary>
        /// معرف السجل
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// معرف المستخدم (MasterUser)
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// معرف الدور (Role)
        /// </summary>
        public int RoleId { get; set; }

        /// <summary>
        /// Navigation Properties
        /// </summary>
        public MasterUser? User { get; set; }
        public Role? Role { get; set; }
    }
}

