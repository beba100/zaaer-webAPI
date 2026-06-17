using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsOutletCatalogService
    {
        Task<IReadOnlyList<PmsOutletDto>> ListOutletsAsync(CancellationToken cancellationToken = default);
        Task<PmsOutletDto?> GetOutletAsync(int outletId, CancellationToken cancellationToken = default);
        Task<PmsOutletDto> CreateOutletAsync(PmsUpsertOutletDto dto, CancellationToken cancellationToken = default);
        Task<PmsOutletDto?> UpdateOutletAsync(int outletId, PmsUpsertOutletDto dto, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsOutletCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default);
        Task<PmsOutletCategoryDto> CreateCategoryAsync(PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken = default);
        Task<PmsOutletCategoryDto?> UpdateCategoryAsync(int categoryId, PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsOutletItemDto>> ListItemsAsync(int? outletId, int? categoryId, CancellationToken cancellationToken = default);
        Task<PmsOutletItemDto> CreateItemAsync(PmsUpsertOutletItemDto dto, CancellationToken cancellationToken = default);
        Task<PmsOutletItemDto?> UpdateItemAsync(int itemId, PmsUpsertOutletItemDto dto, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsOutletTableDto>> ListTablesAsync(int? outletId, CancellationToken cancellationToken = default);
        Task<PmsOutletTableDto> CreateTableAsync(PmsUpsertOutletTableDto dto, CancellationToken cancellationToken = default);
        Task<PmsOutletTableDto?> UpdateTableAsync(int tableId, PmsUpsertOutletTableDto dto, CancellationToken cancellationToken = default);

        Task<PmsPosCatalogDto?> GetPosCatalogAsync(int outletId, CancellationToken cancellationToken = default);

        Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default);
    }
}
