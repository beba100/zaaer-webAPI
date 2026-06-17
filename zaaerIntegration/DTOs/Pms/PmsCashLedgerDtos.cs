namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsCashLedgerStatementDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal ClosingBalance { get; set; }
        public IReadOnlyList<PmsCashLedgerRowDto> Items { get; set; } = Array.Empty<PmsCashLedgerRowDto>();
    }

    public sealed class PmsCashLedgerRowDto
    {
        public long LedgerId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string? SourceSubtype { get; set; }
        public long? SourceId { get; set; }
        public long? SourceZaaerId { get; set; }
        public string? SourceNo { get; set; }
        public string? MovementLabel { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
