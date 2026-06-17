using DevExpress.XtraReports.Web.Extensions;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Hosting;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Reports.Invoice;

namespace zaaerIntegration.Services
{
    /// <summary>
    /// Custom Report Storage for DevExpress Reporting.
    /// Layout-only loading; runtime data binding happens in IReportProvider.
    /// Must not inject tenant-scoped services — DevExpress resolves this during app startup.
    /// </summary>
    public class CustomReportStorageWebExtension : ReportStorageWebExtension
    {
        private readonly string _reportsDirectory;
        private readonly bool _allowLayoutWrites;

        /// <summary>
        /// Creates report storage and enables layout writes only while running in Development.
        /// </summary>
        public CustomReportStorageWebExtension(IWebHostEnvironment environment)
        {
            _reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports");
            _allowLayoutWrites = environment.IsDevelopment();
        }

        /// <inheritdoc />
        public override Dictionary<string, string> GetUrls()
        {
            var urls = new Dictionary<string, string>
            {
                [ReportKeys.Invoice] = ReportKeys.Invoice,
                [ReportVersions.Invoice_v1] = ReportKeys.Invoice
            };

            if (Directory.Exists(_reportsDirectory))
            {
                foreach (var reportFile in Directory.GetFiles(_reportsDirectory, "*.repx"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(reportFile);
                    var relativePath = Path.Combine("Reports", Path.GetFileName(reportFile));
                    urls.TryAdd(fileName, relativePath);
                }
            }

            return urls;
        }

        /// <inheritdoc />
        public override bool IsValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return url.EndsWith(".repx", StringComparison.OrdinalIgnoreCase)
                || url.Contains(ReportKeys.Invoice, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override byte[] GetData(string url)
        {
            try
            {
                if (url.Contains(ReportKeys.Invoice, StringComparison.OrdinalIgnoreCase))
                {
                    return SaveLayoutBytes(new InvoiceReport_v1());
                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), url);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Report file not found: {filePath}");
                }

                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
                    $"Could not load report '{url}': {ex.Message}");
            }
        }

        /// <inheritdoc />
        public override bool CanSetData(string url) => _allowLayoutWrites && IsValidUrl(url);

        /// <inheritdoc />
        public override void SetData(XtraReport report, string url)
        {
            if (!CanSetData(url))
            {
                throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
                    "Report layout updates are disabled in this environment.");
            }

            try
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), url);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                report.SaveLayoutToXml(filePath);
            }
            catch (Exception ex)
            {
                throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
                    $"Could not save report '{url}': {ex.Message}");
            }
        }

        private static byte[] SaveLayoutBytes(XtraReport report)
        {
            using var stream = new MemoryStream();
            report.SaveLayoutToXml(stream);
            return stream.ToArray();
        }
    }
}
