using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Reports Testing Controller - Visible in Swagger for testing DevExpress reports
    /// </summary>
    [ApiController]
    [Route("api/reports-test")]
    public class ReportsTestController : ControllerBase
    {
        private readonly ILogger<ReportsTestController> _logger;
        private readonly string _reportsDirectory;

        public ReportsTestController(ILogger<ReportsTestController> logger)
        {
            _logger = logger;
            _reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Reports");
        }

        /// <summary>
        /// Get list of available reports for testing
        /// </summary>
        /// <returns>List of available reports with test URLs</returns>
        [HttpGet("available-reports")]
        public IActionResult GetAvailableReports()
        {
            try
            {
                var reports = new List<object>();

                if (Directory.Exists(_reportsDirectory))
                {
                    var reportFiles = Directory.GetFiles(_reportsDirectory, "*.repx", SearchOption.AllDirectories);
                    foreach (var reportFile in reportFiles)
                    {
                        // Get the relative path from the Reports directory
                        var relativePath = Path.GetRelativePath(_reportsDirectory, reportFile);
                        var reportName = Path.GetFileNameWithoutExtension(relativePath);
                        var displayName = Path.GetFileNameWithoutExtension(reportFile);

                        reports.Add(new
                        {
                            ReportName = reportName,
                            FileName = Path.GetFileName(reportFile),
                            RelativePath = relativePath.Replace("\\", "/"), // For display
                            TestUrl = $"/api/reports-test/view-report?reportName={reportName}",
                            SwaggerTestUrl = $"{Request.Scheme}://{Request.Host}/api/reports-test/view-report?reportName={reportName}",
                            DirectViewerUrl = $"/DXXRDV?reportName={reportName}"
                        });
                    }
                }

                var response = new
                {
                    TotalReports = reports.Count,
                    Reports = reports,
                    Instructions = new
                    {
                        SwaggerTest = "Use the 'Test it out' button for each report URL",
                        DirectViewer = "Use DirectViewerUrl for DevExpress Web Report Viewer",
                        HtmlPage = "Use TestUrl for HTML preview with mock data"
                    }
                };

                _logger.LogInformation("Retrieved {Count} available reports for testing", reports.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available reports");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test a specific report with HTML preview and mock data
        /// </summary>
        /// <param name="reportName">Name of the report to test (without .repx extension)</param>
        /// <returns>HTML page with report preview</returns>
        [HttpGet("view-report")]
        public IActionResult ViewReport(string reportName)
        {
            try
            {
                if (string.IsNullOrEmpty(reportName))
                {
                    return BadRequest(new { error = "Report name is required" });
                }

                // Check if report exists (search recursively)
                var reportFiles = Directory.GetFiles(_reportsDirectory, "*.repx", SearchOption.AllDirectories);
                var reportPath = reportFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(reportName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(reportPath) || !System.IO.File.Exists(reportPath))
                {
                    return NotFound(new { error = $"Report '{reportName}' not found" });
                }

                // Generate HTML preview with mock data
                var htmlContent = GenerateTestReportHtml(reportName);

                _logger.LogInformation("Generated test preview for report: {ReportName}", reportName);
                return Content(htmlContent, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing report {ReportName}", reportName);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get DevExpress Web Document Viewer URL for a report
        /// </summary>
        /// <param name="reportName">Name of the report</param>
        /// <returns>DevExpress viewer URL</returns>
        [HttpGet("viewer-url")]
        public IActionResult GetViewerUrl(string reportName)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var viewerUrl = $"{baseUrl}/DXXRDV";

                var response = new
                {
                    ReportName = reportName,
                    ViewerUrl = viewerUrl,
                    FullUrl = $"{viewerUrl}?reportName={reportName}",
                    Instructions = "Use this URL to access DevExpress Web Document Viewer directly"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating viewer URL for {ReportName}", reportName);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get system status for DevExpress reporting
        /// </summary>
        /// <returns>System status information</returns>
        [HttpGet("status")]
        public IActionResult GetSystemStatus()
        {
            try
            {
                var reportsDirectoryExists = Directory.Exists(_reportsDirectory);
                var reportFiles = reportsDirectoryExists ?
                    Directory.GetFiles(_reportsDirectory, "*.repx", SearchOption.AllDirectories) : new string[0];

                var response = new
                {
                    DevExpressReporting = new
                    {
                        Status = "Enabled",
                        Version = "24.1.9",
                        ReportsDirectory = _reportsDirectory,
                        ReportsDirectoryExists = reportsDirectoryExists,
                        TotalReports = reportFiles.Length,
                        ReportFiles = reportFiles.Select(Path.GetFileName).ToArray()
                    },
                    Endpoints = new
                    {
                        DocumentViewer = "/DXXRDV",
                        ReportDesigner = "/DXXRD",
                        QueryBuilder = "/DXXQB"
                    },
                    TestingUrls = new
                    {
                        AvailableReports = $"{Request.Scheme}://{Request.Host}/api/reports-test/available-reports",
                        Status = $"{Request.Scheme}://{Request.Host}/api/reports-test/status"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system status");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string GenerateTestReportHtml(string reportName)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var viewerUrl = $"{baseUrl}/DXXRDV?reportName={reportName}";

            return $@"
<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Test Report: {reportName}</title>

    <!-- DevExtreme CSS -->
    <link rel='stylesheet' href='/Lib/css/dx.light.css'>
    <link rel='stylesheet' href='/Lib/css/dx.common.css'>

    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
            direction: rtl;
        }}

        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}

        .header {{
            text-align: center;
            border-bottom: 2px solid #007bff;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }}

        .info {{
            background: #e7f3ff;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
            border-left: 4px solid #007bff;
        }}

        .actions {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 30px;
        }}

        .btn {{
            display: inline-block;
            padding: 12px 20px;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            text-decoration: none;
            font-weight: 500;
            text-align: center;
            transition: all 0.3s ease;
        }}

        .btn-primary {{ background: #007bff; color: white; }}
        .btn-success {{ background: #28a745; color: white; }}
        .btn-info {{ background: #17a2b8; color: white; }}
        .btn-warning {{ background: #ffc107; color: black; }}

        .btn:hover {{
            opacity: 0.8;
            transform: translateY(-1px);
        }}

        .viewer-container {{
            border: 1px solid #ddd;
            border-radius: 5px;
            padding: 20px;
            background: #fafafa;
            min-height: 600px;
        }}

        .loading {{
            text-align: center;
            padding: 50px;
            font-size: 18px;
            color: #666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🧪 اختبار التقرير</h1>
            <h2>{reportName}</h2>
        </div>

        <div class='info'>
            <strong>معلومات الاختبار:</strong><br>
            - اسم التقرير: {reportName}<br>
            - ملف التقرير: {reportName}.repx<br>
            - DevExpress Viewer URL: {viewerUrl}
        </div>

        <div class='actions'>
            <a href='{viewerUrl}' target='_blank' class='btn btn-primary'>
                📊 عرض التقرير (DevExpress Viewer)
            </a>
            <a href='/api/reports-test/available-reports' class='btn btn-info'>
                📋 قائمة التقارير
            </a>
            <a href='/swagger' target='_blank' class='btn btn-success'>
                🔧 Swagger API
            </a>
            <button onclick='reloadViewer()' class='btn btn-warning'>
                🔄 إعادة تحميل
            </button>
        </div>

        <div class='viewer-container'>
            <div id='reportViewer' class='loading'>
                جاري تحميل التقرير...
                <br><br>
                <a href='{viewerUrl}' target='_blank'>افتح في نافذة منفصلة إذا لم يظهر التقرير</a>
            </div>
        </div>
    </div>

    <!-- DevExtreme JS -->
    <script src='/Lib/js/jquery.js'></script>
    <script src='/js/dx.all.js'></script>

    <script>
        $(document).ready(function() {{
            // Try to load DevExpress Report Viewer
            setTimeout(function() {{
                try {{
                    $('#reportViewer').dxReportViewer({{
                        reportUrl: '{reportName}',
                        requestOptions: {{
                            host: '{baseUrl}',
                            invokeAction: 'DXXRDV'
                        }},
                        width: '100%',
                        height: 550
                    }});
                }} catch (e) {{
                    $('#reportViewer').html(`
                        <div style='text-align: center; padding: 50px;'>
                            <h3>خطأ في تحميل التقرير</h3>
                            <p>يمكنك فتح التقرير مباشرة:</p>
                            <a href='{viewerUrl}' target='_blank' class='btn btn-primary'>فتح التقرير</a>
                            <br><br>
                            <small>خطأ: ` + e.message + `</small>
                        </div>
                    `);
                }}
            }}, 1000);
        }});

        function reloadViewer() {{
            location.reload();
        }}
    </script>
</body>
</html>";
        }

        /// <summary>
        /// Test Cash Transactions Classification - اختبار تصنيف الحركات النقدية
        /// يعرض كيف يتم تصنيف كل حركة نقدية من جدول payment_receipts
        /// </summary>
        /// <param name="hotelId">معرف الفندق (افتراضياً 1)</param>
        /// <param name="days">عدد الأيام الماضية للاختبار (افتراضياً 30 يوم)</param>
        /// <returns>HTML page showing cash transaction classification</returns>
        [HttpGet("cash-classification-test")]
        public async Task<IActionResult> CashClassificationTest(int hotelId = 1, int days = 30)
        {
            try
            {
                // For testing, we'll use a simple connection string
                // In real scenario, this would get tenant connection
                var connectionString = "Server=localhost;Database=TestHotel;User Id=sa;Password=123456;Encrypt=True;TrustServerCertificate=True;";

                using var conn = new SqlConnection(connectionString);

                var fromDate = DateTime.Now.AddDays(-days);
                var toDate = DateTime.Now;

                var result = await conn.QueryAsync(
                    """
                    SELECT
                        transaction_date AS TransactionDate,
                        CASE WHEN credit_amount > 0 THEN 'IN' ELSE 'OUT' END AS Direction,
                        source_type AS SourceType,
                        CASE WHEN credit_amount > 0 THEN credit_amount ELSE debit_amount END AS Amount,
                        CAST(N'' AS NVARCHAR(50)) AS PaymentMethod,
                        CAST(NULL AS NVARCHAR(255)) AS BankName,
                        source_subtype AS VoucherCode,
                        source_no AS ReceiptNo,
                        description AS Notes
                    FROM dbo.cash_ledger
                    WHERE hotel_id = @HotelId
                      AND transaction_date >= @DateFrom
                      AND transaction_date < DATEADD(DAY, 1, @DateTo)
                    ORDER BY transaction_date, ledger_id
                    """,
                    new { HotelId = hotelId, DateFrom = fromDate.Date, DateTo = toDate.Date }
                );

                var transactions = result.ToList();

                // Group by direction for summary
                var inTransactions = transactions.Where(t => t.Direction == "IN").ToList();
                var outTransactions = transactions.Where(t => t.Direction == "OUT").ToList();

                var htmlContent = $@"
                <!DOCTYPE html>
                <html lang='ar' dir='rtl'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>اختبار تصنيف الحركات النقدية</title>
                    <style>
                        body {{
                            font-family: 'Arial', sans-serif;
                            direction: rtl;
                            margin: 20px;
                            background-color: #f5f5f5;
                        }}
                        .container {{
                            max-width: 1200px;
                            margin: 0 auto;
                            background: white;
                            padding: 30px;
                            border-radius: 10px;
                            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                        }}
                        .header {{
                            text-align: center;
                            border-bottom: 2px solid #007bff;
                            padding-bottom: 20px;
                            margin-bottom: 30px;
                        }}
                        .summary {{
                            display: grid;
                            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                            gap: 20px;
                            margin-bottom: 30px;
                        }}
                        .summary-card {{
                            background: #f8f9fa;
                            padding: 20px;
                            border-radius: 8px;
                            text-align: center;
                            border-left: 4px solid #007bff;
                        }}
                        .summary-card.in {{ border-left-color: #28a745; }}
                        .summary-card.out {{ border-left-color: #dc3545; }}
                        .summary-value {{ font-size: 24px; font-weight: bold; color: #007bff; }}
                        .transactions {{
                            margin-top: 30px;
                        }}
                        .section-title {{
                            font-size: 20px;
                            font-weight: bold;
                            color: #333;
                            margin: 20px 0 10px 0;
                            padding-bottom: 10px;
                            border-bottom: 1px solid #dee2e6;
                        }}
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin-bottom: 20px;
                        }}
                        th, td {{
                            padding: 12px;
                            text-align: right;
                            border-bottom: 1px solid #dee2e6;
                        }}
                        th {{
                            background-color: #f8f9fa;
                            font-weight: bold;
                        }}
                        tr:hover {{
                            background-color: #f8f9fa;
                        }}
                        .direction-in {{ color: #28a745; font-weight: bold; }}
                        .direction-out {{ color: #dc3545; font-weight: bold; }}
                        .source-type {{
                            font-size: 12px;
                            background: #e9ecef;
                            padding: 2px 6px;
                            border-radius: 3px;
                        }}
                        .total-row {{
                            background-color: #e3f2fd !important;
                            font-weight: bold;
                        }}
                        .test-info {{
                            background: #fff3cd;
                            border: 1px solid #ffeaa7;
                            color: #856404;
                            padding: 15px;
                            border-radius: 5px;
                            margin-bottom: 20px;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='test-info'>
                            <strong>معلومات الاختبار:</strong><br>
                            فندق رقم: {hotelId} | الفترة: من {fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}<br>
                            إجمالي الحركات: {transactions.Count} | وارد: {inTransactions.Count} | صادر: {outTransactions.Count}
                        </div>

                        <div class='header'>
                            <h1>اختبار تصنيف الحركات النقدية</h1>
                            <p>بناءً على جدول payment_receipts فقط</p>
                        </div>

                        <div class='summary'>
                            <div class='summary-card in'>
                                <div>الواردات النقدية (IN)</div>
                                <div class='summary-value'>{inTransactions.Sum(t => t.Amount):N2}</div>
                                <div>{inTransactions.Count} حركة</div>
                            </div>
                            <div class='summary-card out'>
                                <div>المصروفات النقدية (OUT)</div>
                                <div class='summary-value'>{outTransactions.Sum(t => t.Amount):N2}</div>
                                <div>{outTransactions.Count} حركة</div>
                            </div>
                            <div class='summary-card'>
                                <div>الرصيد</div>
                                <div class='summary-value'>{(inTransactions.Sum(t => t.Amount) - outTransactions.Sum(t => t.Amount)):N2}</div>
                                <div>ريال سعودي</div>
                            </div>
                        </div>

                        {(transactions.Any() ? $@"
                        <div class='transactions'>
                            <div class='section-title'>تفاصيل الحركات المصنفة</div>
                            <table>
                                <thead>
                                    <tr>
                                        <th>التاريخ</th>
                                        <th>الاتجاه</th>
                                        <th>النوع</th>
                                        <th>المبلغ</th>
                                        <th>طريقة الدفع</th>
                                        <th>البنك</th>
                                        <th>voucher_code</th>
                                        <th>رقم الإيصال</th>
                                        <th>الملاحظات</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {string.Join("", transactions.Select(t => $@"
                                    <tr>
                                        <td>{t.TransactionDate:yyyy-MM-dd}</td>
                                        <td class='direction-{(t.Direction.ToLower())}'>{t.Direction}</td>
                                        <td><span class='source-type'>{t.SourceType}</span></td>
                                        <td>{t.Amount:N2}</td>
                                        <td>{t.PaymentMethod}</td>
                                        <td>{t.BankName ?? "-"}</td>
                                        <td>{t.VoucherCode}</td>
                                        <td>{t.ReceiptNo}</td>
                                        <td>{t.Notes}</td>
                                    </tr>"))}
                                </tbody>
                            </table>
                        </div>" : "<div class='section-title'>لا توجد حركات نقدية في هذه الفترة</div>")}
                    </div>
                </body>
                </html>";

                return Content(htmlContent, "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cash classification test");
                return Content($@"
                <html><body>
                <h2>خطأ في الاختبار</h2>
                <p>الخطأ: {ex.Message}</p>
                <p>تأكد من صحة connection string وقاعدة البيانات</p>
                </body></html>", "text/html; charset=utf-8");
            }
        }

    }
}