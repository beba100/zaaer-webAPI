using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System.IO;
using System.Linq;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Custom File Sink that uses KsaTime for daily log file rotation
    /// Ensures log files are created based on Saudi Arabia timezone, not server timezone
    /// </summary>
    public class KsaTimeFileSink : ILogEventSink, IDisposable
    {
        private readonly string _pathTemplate;
        private readonly ITextFormatter _formatter;
        private readonly long? _fileSizeLimitBytes;
        private readonly int? _retainedFileCountLimit;
        private string? _currentFilePath;
        private string? _currentDate;
        private StreamWriter? _currentWriter;
        private long _currentFileSize;
        private readonly object _lock = new object();

        public KsaTimeFileSink(
            string pathTemplate,
            ITextFormatter formatter,
            long? fileSizeLimitBytes = null,
            int? retainedFileCountLimit = null)
        {
            // Extract base path (without date) from template
            // Template format: "logs/log-{Date}.txt" or "logs/log-2026-01-01.txt"
            // We'll replace {Date} or the date part dynamically
            if (pathTemplate.Contains("{Date}"))
            {
                _pathTemplate = pathTemplate;
            }
            else
            {
                // If date is already in template, extract base path
                // Example: "logs/log-2026-01-01.txt" -> base: "logs/log-", extension: ".txt"
                var lastDash = pathTemplate.LastIndexOf('-');
                var lastDot = pathTemplate.LastIndexOf('.');
                if (lastDash > 0 && lastDot > lastDash)
                {
                    _pathTemplate = pathTemplate.Substring(0, lastDash + 1) + "{Date}" + pathTemplate.Substring(lastDot);
                }
                else
                {
                    _pathTemplate = pathTemplate.Replace(".txt", "-{Date}.txt");
                }
            }
            _formatter = formatter;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retainedFileCountLimit = retainedFileCountLimit;
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_lock)
            {
                // Get current date in Saudi Arabia timezone
                var ksaDate = KsaTime.Now.ToString("yyyyMMdd");
                
                // Check if we need to rotate to a new file
                if (_currentDate != ksaDate || _currentWriter == null)
                {
                    // Close current file if exists
                    if (_currentWriter != null)
                    {
                        _currentWriter.Dispose();
                        _currentWriter = null;
                    }

                    // Generate new file path using KsaTime
                    var ksaDateFormatted = KsaTime.Now.ToString("yyyy-MM-dd");
                    _currentFilePath = _pathTemplate.Replace("{Date}", ksaDateFormatted);
                    
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(_currentFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Open new file
                    _currentWriter = new StreamWriter(_currentFilePath, append: true);
                    _currentDate = ksaDate;
                    _currentFileSize = 0;

                    // Cleanup old files if retention limit is set
                    if (_retainedFileCountLimit.HasValue && !string.IsNullOrEmpty(directory))
                    {
                        CleanupOldFiles(directory, _retainedFileCountLimit.Value);
                    }
                }

                // Check file size limit
                if (_fileSizeLimitBytes.HasValue && _currentFileSize >= _fileSizeLimitBytes.Value)
                {
                    // Rotate file by appending timestamp
                    var timestamp = KsaTime.Now.ToString("yyyyMMddHHmmss");
                    var rotatedPath = _currentFilePath!.Replace(".txt", $"-{timestamp}.txt");
                    _currentWriter?.Dispose();
                    if (File.Exists(_currentFilePath))
                    {
                        File.Move(_currentFilePath, rotatedPath);
                    }
                    _currentWriter = new StreamWriter(_currentFilePath, append: false);
                    _currentFileSize = 0;
                }

                // Write log event
                if (_currentWriter != null)
                {
                    _formatter.Format(logEvent, _currentWriter);
                    _currentWriter.Flush();
                    _currentFileSize = _currentWriter.BaseStream.Length;
                }
            }
        }

        private void CleanupOldFiles(string directory, int retentionLimit)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (files.Count > retentionLimit)
                {
                    foreach (var file in files.Skip(retentionLimit))
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch
                        {
                            // Ignore deletion errors
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public void Dispose()
        {
            _currentWriter?.Dispose();
        }
    }
}

