using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Integrations
{
    public sealed class BaladyReportQueryDto
    {
        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }
    }

    public sealed class BaladyReportRowDto
    {
        public string? RoomNumber { get; set; }

        public DateTime? PeriodFrom { get; set; }

        public DateTime? PeriodTo { get; set; }

        public decimal Amount { get; set; }

        public string? CustomerName { get; set; }

        public string? BookingNumber { get; set; }

        public string? RoomType { get; set; }

        public string? Notes { get; set; }
    }
}
