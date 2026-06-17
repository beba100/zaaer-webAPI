using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// PMS guest/customer CRUD with Master DB numbering (GUS + global zaaer_id).
    /// </summary>
    public interface IPmsCustomerService
    {
        Task<(IEnumerable<CustomerResponseDto> Customers, int TotalCount)> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string? searchMode = null,
            int? nationalityId = null,
            int? guestCategoryId = null);

        Task<CustomerResponseDto?> GetByZaaerOrCustomerIdAsync(
            int id,
            int? hotelId = null,
            CancellationToken cancellationToken = default);

        Task<CustomerResponseDto> CreateAsync(
            CreateCustomerDto dto,
            CancellationToken cancellationToken = default);

        Task<CustomerResponseDto?> UpdateAsync(
            int id,
            UpdateCustomerDto dto,
            int? hotelId = null,
            CancellationToken cancellationToken = default);
    }
}
