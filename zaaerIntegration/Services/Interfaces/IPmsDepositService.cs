#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IDepositImageService
    {
        Task<IReadOnlyList<PmsDepositImageDto>> GetImagesAsync(int receiptId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsDepositImageDto>> UploadImagesAsync(
            int receiptId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteImageAsync(int receiptId, int imageId, CancellationToken cancellationToken = default);
    }

    public interface IPmsDepositService
    {
        Task<PmsDepositListResultDto> ListAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);

        Task<PmsDepositDetailDto?> GetByIdAsync(int receiptId, CancellationToken cancellationToken = default);

        Task<PmsDepositDetailDto> CreateAsync(PmsCreateDepositDto dto, CancellationToken cancellationToken = default);

        Task<PmsDepositDetailDto?> UpdateAsync(
            int receiptId,
            PmsUpdateDepositDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(int receiptId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsDepositBankDto>> GetBanksAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsDepositPaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsDepositImageDto>> GetImagesAsync(int receiptId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsDepositImageDto>> UploadImagesAsync(
            int receiptId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteImageAsync(int receiptId, int imageId, CancellationToken cancellationToken = default);
    }
}
