namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض سجل موافقات المصروف
    /// </summary>
    public class ExpenseApprovalHistoryDto
    {
        public int Id { get; set; }
        public long ExpenseId { get; set; }
        public string Action { get; set; } = string.Empty; // created, approved, rejected, awaiting-manager, etc.
        public int? ActionBy { get; set; }
        public string? ActionByFullName { get; set; }
        public string? ActionByRole { get; set; }
        public string? ActionByTenantName { get; set; }
        public DateTime ActionAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string? Comments { get; set; }
        
        /// <summary>
        /// التوصية (اختياري)
        /// Recommendation (optional)
        /// </summary>
        public string? Recommendation { get; set; }
        
        /// <summary>
        /// معرف المستخدم المستهدف للتوصية (NULL = للجميع)
        /// Recommendation target user ID (NULL = for everyone)
        /// </summary>
        public int? RecommendationToUserId { get; set; }
        
        /// <summary>
        /// اسم المستخدم المستهدف للتوصية
        /// Recommendation target user name
        /// </summary>
        public string? RecommendationToUserName { get; set; }
        
        /// <summary>
        /// قائمة معرفات المستخدمين الذين قرأوا التوصية
        /// List of user IDs who read the recommendation
        /// </summary>
        public List<int>? RecommendationReadBy { get; set; }
        
        /// <summary>
        /// قائمة أسماء المستخدمين الذين قرأوا التوصية
        /// List of full names of users who read the recommendation
        /// </summary>
        public List<string>? RecommendationReadByFullNames { get; set; }
        
        /// <summary>
        /// هل قرأ المستخدم الحالي التوصية؟
        /// Has the current user read the recommendation?
        /// </summary>
        public bool IsRecommendationReadByCurrentUser { get; set; }
    }
}

