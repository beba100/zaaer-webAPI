namespace zaaerIntegration.Services.Interfaces
{
    public sealed record StaleNumberGenerationAuditRow(
        long AuditId,
        int? TenantId,
        int? HotelZaaerId,
        int? LocalHotelId,
        string? DocCode,
        string? DocumentNo,
        long? ZaaerId,
        string? RequestRef,
        string? GeneratedBy,
        DateTime CreatedAtUtc,
        string Status);

    public sealed record NumberingAuditReconciliationResult(
        int StaleReservedCount,
        int ReportedCount,
        int StaleMinutesThreshold,
        IReadOnlyList<StaleNumberGenerationAuditRow> Rows);

    /// <summary>
    /// Reports Master DB number allocations stuck in reserved status.
    /// </summary>
    public interface INumberingAuditReconciliationService
    {
        Task<NumberingAuditReconciliationResult> GetStaleReservedAsync(
            int staleMinutes,
            int maxRows,
            CancellationToken cancellationToken = default);
    }
}
