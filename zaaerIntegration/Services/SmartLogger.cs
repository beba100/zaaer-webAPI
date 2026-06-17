using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace zaaerIntegration.Services
{
    /// <summary>
    /// Smart Logger - Prevents duplicate error logs and adds context
    /// Only logs Error, Critical, and real Warnings (not routine Info)
    /// </summary>
    public class SmartLogger
    {
        private readonly ILogger _logger;
        
        // Track duplicate errors: Key = Error Signature, Value = (Count, LastOccurrence)
        private static readonly ConcurrentDictionary<string, (int Count, DateTime LastOccurrence)> _errorOccurrences = new();
        
        // Cleanup old entries every hour
        private static DateTime _lastCleanup = DateTime.UtcNow;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

        public SmartLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Log error with duplicate detection
        /// </summary>
        public void LogError(string category, string message, int? hotelId = null, int? userId = null, 
            string? action = null, int? entityId = null, Exception? exception = null)
        {
            // Create error signature for duplicate detection
            var errorSignature = CreateErrorSignature(message, hotelId, entityId);
            
            // Check if this error occurred before
            var (count, lastOccurrence) = _errorOccurrences.AddOrUpdate(
                errorSignature,
                (1, DateTime.UtcNow),
                (key, existing) => (existing.Count + 1, DateTime.UtcNow));

            // Log only first occurrence, or update every 100 occurrences
            if (count == 1 || count % 100 == 0)
            {
                var logMessage = BuildLogMessage(category, message, hotelId, userId, action, entityId, exception, count);
                
                if (exception != null)
                {
                    _logger.LogError(exception, logMessage);
                }
                else
                {
                    _logger.LogError(logMessage);
                }
            }

            // Cleanup old entries periodically
            CleanupOldEntries();
        }

        /// <summary>
        /// Log critical error (always logged, no duplicate detection)
        /// </summary>
        public void LogCritical(string category, string message, int? hotelId = null, int? userId = null,
            string? action = null, int? entityId = null, Exception? exception = null)
        {
            var logMessage = BuildLogMessage(category, message, hotelId, userId, action, entityId, exception);
            
            if (exception != null)
            {
                _logger.LogCritical(exception, logMessage);
            }
            else
            {
                _logger.LogCritical(logMessage);
            }
        }

        /// <summary>
        /// Log warning (only real issues, not routine operations)
        /// </summary>
        public void LogWarning(string category, string message, int? hotelId = null, int? userId = null,
            string? action = null, int? entityId = null, Exception? exception = null)
        {
            var logMessage = BuildLogMessage(category, message, hotelId, userId, action, entityId, exception);
            
            if (exception != null)
            {
                _logger.LogWarning(exception, logMessage);
            }
            else
            {
                _logger.LogWarning(logMessage);
            }
        }

        /// <summary>
        /// Build structured log message with context
        /// Format: [CATEGORY] Message | HotelId: X, UserId: Y, Action: Z, EntityId: W | Error: ...
        /// </summary>
        private string BuildLogMessage(string category, string message, int? hotelId, int? userId,
            string? action, int? entityId, Exception? exception, int? occurrenceCount = null)
        {
            var sb = new StringBuilder();
            sb.Append($"[{category}] {message}");
            
            // Add context
            var contextParts = new List<string>();
            if (hotelId.HasValue) contextParts.Add($"HotelId: {hotelId}");
            if (userId.HasValue) contextParts.Add($"UserId: {userId}");
            if (!string.IsNullOrWhiteSpace(action)) contextParts.Add($"Action: {action}");
            if (entityId.HasValue) contextParts.Add($"EntityId: {entityId}");
            
            if (contextParts.Any())
            {
                sb.Append(" | ");
                sb.Append(string.Join(", ", contextParts));
            }
            
            // Add exception message (without stack trace for Warning level)
            if (exception != null)
            {
                sb.Append($" | Error: {exception.Message}");
                if (exception.InnerException != null)
                {
                    sb.Append($" | Inner: {exception.InnerException.Message}");
                }
            }
            
            // Add occurrence count if this is a duplicate
            if (occurrenceCount.HasValue && occurrenceCount > 1)
            {
                sb.Append($" | Occurrences: {occurrenceCount}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Create error signature for duplicate detection
        /// Uses message + HotelId + EntityId (if available)
        /// </summary>
        private string CreateErrorSignature(string message, int? hotelId, int? entityId)
        {
            var parts = new List<string> { message };
            if (hotelId.HasValue) parts.Add($"H{hotelId}");
            if (entityId.HasValue) parts.Add($"E{entityId}");
            return string.Join("|", parts);
        }

        /// <summary>
        /// Cleanup old error occurrences (older than 24 hours)
        /// </summary>
        private void CleanupOldEntries()
        {
            if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
                return;

            _lastCleanup = DateTime.UtcNow;
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            
            var keysToRemove = _errorOccurrences
                .Where(kvp => kvp.Value.LastOccurrence < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _errorOccurrences.TryRemove(key, out _);
            }
        }
    }
}

