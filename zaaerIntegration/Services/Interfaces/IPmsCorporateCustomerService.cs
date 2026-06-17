using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// PMS corporate customer CRUD with Master DB numbering (<c>doc_code = corporate</c>, <c>cor_no</c> + <c>zaaer_id</c>).
    /// </summary>
    public interface IPmsCorporateCustomerService
    {
        Task<CorporatePickerResponseDto> GetForPickerAsync(
            int? hotelId,
            string? hotelCode,
            CancellationToken cancellationToken = default);

        /// <summary>Load by internal <c>corporate_id</c> or <c>zaaer_id</c>.</summary>
        Task<CorporateCustomerResponseDto?> GetByZaaerOrCorporateIdAsync(
            int id,
            int? hotelId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Create with Master DB <c>corporate</c> numbering (<c>cor_no</c>, <c>zaaer_id</c>).</summary>
        Task<CorporateCustomerResponseDto> CreateAsync(
            CreateCorporateCustomerDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>Update fields; route <paramref name="id"/> may be corporate or zaaer id.</summary>
        Task<CorporateCustomerResponseDto?> UpdateAsync(
            int id,
            UpdateCorporateCustomerDto dto,
            int? hotelId = null,
            CancellationToken cancellationToken = default);
    }
}
