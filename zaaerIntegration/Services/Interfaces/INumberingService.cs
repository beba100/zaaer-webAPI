namespace zaaerIntegration.Services.Interfaces
{
    public sealed record GeneratedDocumentNumber(
        long NumericValue,
        string DocumentNo,
        long AuditId);

    public sealed record GeneratedZaaerId(
        long ZaaerId,
        long AuditId);

    public sealed record GeneratedBusinessIdentity(
        long? ZaaerId,
        long NumericValue,
        string DocumentNo,
        long AuditId);

    /// <summary>
    /// Issues per-entity-type Zaaer IDs and hotel-scoped document numbers from the Master DB.
    /// Uniqueness is <c>(doc_code / entity type, zaaer_id)</c>, not <c>zaaer_id</c> alone.
    /// </summary>
    public interface INumberingService
    {
        /// <summary>
        /// Next <c>zaaer_id</c> for the given <paramref name="docCode"/> (see <see cref="NumberingDocCodes"/>).
        /// </summary>
        Task<GeneratedZaaerId> GetNextEntityZaaerIdAsync(
            string docCode,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default);

        Task<GeneratedDocumentNumber> GetNextDocumentNumberAsync(
            string docCode,
            int hotelId,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default);

        Task<GeneratedBusinessIdentity> GetNextBusinessIdentityAsync(
            string docCode,
            int hotelId,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bumps the Master <c>DocumentCounters</c> row for <paramref name="docCode"/> so
        /// <c>current_value</c> is at least <paramref name="currentValue"/> (never lowers an existing counter).
        /// </summary>
        Task EnsureDocumentCounterAtLeastAsync(
            string docCode,
            int hotelId,
            long currentValue,
            CancellationToken cancellationToken = default);

        Task MarkCommittedAsync(long auditId, CancellationToken cancellationToken = default);

        Task MarkVoidedAsync(long auditId, string? reason = null, CancellationToken cancellationToken = default);
    }
}
