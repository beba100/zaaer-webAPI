namespace zaaerIntegration.Services.Interfaces
{
    public interface IHotelAccessService
    {
        Task<IReadOnlyCollection<int>> GetAllowedTenantIdsAsync(int userId, CancellationToken cancellationToken = default);
        Task<bool> CanAccessTenantAsync(int userId, int tenantId, CancellationToken cancellationToken = default);
    }
}
