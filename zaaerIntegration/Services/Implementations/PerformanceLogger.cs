using Microsoft.Extensions.Logging;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
	/// <summary>
	/// Performance Monitoring Logger Service
	/// خدمة تسجيل مراقبة الأداء
	/// </summary>
	public class PerformanceLogger : IPerformanceLogger
	{
		private readonly ILogger<PerformanceLogger> _logger;

		public PerformanceLogger(ILogger<PerformanceLogger> logger)
		{
			_logger = logger;
		}

		/// <summary>
		/// Log auto-linking operation performance
		/// تسجيل أداء عملية الربط التلقائي
		/// </summary>
		public void LogAutoLinkPerformance(string operation, string entityType, int entityId, long elapsedMs, bool success, string? details = null)
		{
			var logMessage = $"[PERFORMANCE] [AUTO-LINK] Operation: {operation} | EntityType: {entityType} | EntityId: {entityId} | " +
							$"Elapsed: {elapsedMs}ms | Success: {success} | " +
							$"Timestamp: {KsaTime.Now:yyyy-MM-dd HH:mm:ss.fff}";

			if (!string.IsNullOrEmpty(details))
			{
				logMessage += $" | Details: {details}";
			}

			if (success)
			{
				_logger.LogInformation(logMessage);
			}
			else
			{
				_logger.LogWarning(logMessage);
			}
		}

		/// <summary>
		/// Log database query performance
		/// تسجيل أداء استعلام قاعدة البيانات
		/// </summary>
		public void LogQueryPerformance(string queryName, long elapsedMs, int? recordCount = null, string? details = null)
		{
			var logMessage = $"[PERFORMANCE] [QUERY] QueryName: {queryName} | Elapsed: {elapsedMs}ms | " +
							$"Timestamp: {KsaTime.Now:yyyy-MM-dd HH:mm:ss.fff}";

			if (recordCount.HasValue)
			{
				logMessage += $" | Records: {recordCount.Value}";
			}

			if (!string.IsNullOrEmpty(details))
			{
				logMessage += $" | Details: {details}";
			}

			// Log slow queries as warning
			if (elapsedMs > 1000)
			{
				_logger.LogWarning(logMessage + " | ⚠️ SLOW QUERY");
			}
			else if (elapsedMs > 500)
			{
				_logger.LogWarning(logMessage + " | ⚠️ MODERATE QUERY");
			}
			else
			{
				_logger.LogInformation(logMessage);
			}
		}

		/// <summary>
		/// Log allocation operation performance
		/// تسجيل أداء عملية التخصيص
		/// </summary>
		public void LogAllocationPerformance(string operation, int invoiceId, int receiptId, decimal amount, long elapsedMs, bool success, string? details = null)
		{
			var logMessage = $"[PERFORMANCE] [ALLOCATION] Operation: {operation} | InvoiceId: {invoiceId} | ReceiptId: {receiptId} | " +
							$"Amount: {amount:N2} SAR | Elapsed: {elapsedMs}ms | Success: {success} | " +
							$"Timestamp: {KsaTime.Now:yyyy-MM-dd HH:mm:ss.fff}";

			if (!string.IsNullOrEmpty(details))
			{
				logMessage += $" | Details: {details}";
			}

			if (success)
			{
				_logger.LogInformation(logMessage);
			}
			else
			{
				_logger.LogError(logMessage);
			}
		}

		/// <summary>
		/// Log endpoint performance
		/// تسجيل أداء الـ endpoint
		/// </summary>
		public void LogEndpointPerformance(string endpoint, string method, long elapsedMs, int statusCode, string? details = null)
		{
			var logMessage = $"[PERFORMANCE] [ENDPOINT] Method: {method} | Endpoint: {endpoint} | " +
							$"Elapsed: {elapsedMs}ms | StatusCode: {statusCode} | " +
							$"Timestamp: {KsaTime.Now:yyyy-MM-dd HH:mm:ss.fff}";

			if (!string.IsNullOrEmpty(details))
			{
				logMessage += $" | Details: {details}";
			}

			// Log slow endpoints as warning
			if (elapsedMs > 2000)
			{
				_logger.LogWarning(logMessage + " | ⚠️ SLOW ENDPOINT");
			}
			else if (elapsedMs > 1000)
			{
				_logger.LogWarning(logMessage + " | ⚠️ MODERATE ENDPOINT");
			}
			else
			{
				_logger.LogInformation(logMessage);
			}
		}

		/// <summary>
		/// Log transaction performance
		/// تسجيل أداء المعاملة
		/// </summary>
		public void LogTransactionPerformance(string operation, long elapsedMs, bool success, int? affectedRecords = null, string? details = null)
		{
			var logMessage = $"[PERFORMANCE] [TRANSACTION] Operation: {operation} | Elapsed: {elapsedMs}ms | Success: {success} | " +
							$"Timestamp: {KsaTime.Now:yyyy-MM-dd HH:mm:ss.fff}";

			if (affectedRecords.HasValue)
			{
				logMessage += $" | AffectedRecords: {affectedRecords.Value}";
			}

			if (!string.IsNullOrEmpty(details))
			{
				logMessage += $" | Details: {details}";
			}

			if (success)
			{
				_logger.LogInformation(logMessage);
			}
			else
			{
				_logger.LogError(logMessage);
			}
		}
	}
}
