using zaaerIntegration.Reporting.DTOs.Invoice;

namespace zaaerIntegration.Reporting.Services.Invoice;

public interface IInvoiceReportDataService
{
    Task<InvoiceReportDto> GetAsync(int invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceReportDto> GetByZaaerIdAsync(int invoiceZaaerId, CancellationToken cancellationToken = default);
}
