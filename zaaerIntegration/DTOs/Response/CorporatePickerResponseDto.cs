#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Response
{
    /// <summary>
    /// PMS corporate picker: list plus resolved tenant <see cref="HotelId"/> when the client only has <c>hotelCode</c>.
    /// </summary>
    public sealed class CorporatePickerResponseDto
    {
        public int? ResolvedHotelId { get; init; }

        public IReadOnlyList<CorporateCustomerResponseDto> Items { get; init; } = Array.Empty<CorporateCustomerResponseDto>();
    }
}
