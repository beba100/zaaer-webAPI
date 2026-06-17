namespace zaaerIntegration.Services.Interfaces
{
	/// <summary>
	/// Interface for Performance Monitoring Logger
	/// واجهة لتسجيل مراقبة الأداء
	/// </summary>
	public interface IPerformanceLogger
	{
		/// <summary>
		/// Log auto-linking operation performance
		/// تسجيل أداء عملية الربط التلقائي
		/// </summary>
		void LogAutoLinkPerformance(string operation, string entityType, int entityId, long elapsedMs, bool success, string? details = null);

		/// <summary>
		/// Log database query performance
		/// تسجيل أداء استعلام قاعدة البيانات
		/// </summary>
		void LogQueryPerformance(string queryName, long elapsedMs, int? recordCount = null, string? details = null);

		/// <summary>
		/// Log allocation operation performance
		/// تسجيل أداء عملية التخصيص
		/// </summary>
		void LogAllocationPerformance(string operation, int invoiceId, int receiptId, decimal amount, long elapsedMs, bool success, string? details = null);

		/// <summary>
		/// Log endpoint performance
		/// تسجيل أداء الـ endpoint
		/// </summary>
		void LogEndpointPerformance(string endpoint, string method, long elapsedMs, int statusCode, string? details = null);

		/// <summary>
		/// Log transaction performance
		/// تسجيل أداء المعاملة
		/// </summary>
		void LogTransactionPerformance(string operation, long elapsedMs, bool success, int? affectedRecords = null, string? details = null);
	}
}
