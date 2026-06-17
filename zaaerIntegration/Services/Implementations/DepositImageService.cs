#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Models;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class DepositImageService : IDepositImageService
    {
        private const string VoucherCode = "transfers_to_bank";
        private readonly ApplicationDbContext _context;

        public DepositImageService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PmsDepositImageDto>> GetImagesAsync(
            int receiptId,
            CancellationToken cancellationToken = default)
        {
            var receiptZaaerId = await ResolveReceiptZaaerIdAsync(receiptId, cancellationToken);

            var images = await _context.DepositImages
                .AsNoTracking()
                .Where(i => i.ReceiptId == receiptZaaerId)
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.DepositImageId)
                .ToListAsync(cancellationToken);

            return images.Select(img => MapImage(img, receiptId)).ToList();
        }

        public async Task<IReadOnlyList<PmsDepositImageDto>> UploadImagesAsync(
            int receiptId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default)
        {
            if (images == null || images.Count == 0)
            {
                throw new ArgumentException("No images provided.");
            }

            var receiptZaaerId = await ResolveReceiptZaaerIdAsync(receiptId, cancellationToken);

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "deposits");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var displayOrder = await _context.DepositImages
                .Where(i => i.ReceiptId == receiptZaaerId)
                .Select(i => (int?)i.DisplayOrder)
                .MaxAsync(cancellationToken) ?? 0;

            var uploaded = new List<PmsDepositImageDto>();

            foreach (var image in images)
            {
                if (image.Length <= 0)
                {
                    continue;
                }

                displayOrder++;
                var fileName = $"{receiptZaaerId}_{KsaTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativePath = $"/uploads/deposits/{fileName}";

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream, cancellationToken);
                }

                var entity = new DepositImage
                {
                    ReceiptId = receiptZaaerId,
                    ImagePath = relativePath,
                    OriginalFilename = image.FileName,
                    FileSize = image.Length,
                    ContentType = image.ContentType,
                    DisplayOrder = displayOrder,
                    CreatedAt = KsaTime.Now
                };

                _context.DepositImages.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);
                uploaded.Add(MapImage(entity, receiptId));
            }

            return uploaded;
        }

        public async Task<bool> DeleteImageAsync(int receiptId, int imageId, CancellationToken cancellationToken = default)
        {
            var receiptZaaerId = await ResolveReceiptZaaerIdAsync(receiptId, cancellationToken);

            var image = await _context.DepositImages
                .FirstOrDefaultAsync(
                    i => i.DepositImageId == imageId && i.ReceiptId == receiptZaaerId,
                    cancellationToken);

            if (image == null)
            {
                return false;
            }

            _context.DepositImages.Remove(image);
            await _context.SaveChangesAsync(cancellationToken);

            TryDeletePhysicalFile(image.ImagePath);
            return true;
        }

        private async Task<int> ResolveReceiptZaaerIdAsync(int routeReceiptId, CancellationToken cancellationToken)
        {
            var receipt = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r =>
                    r.ReceiptId == routeReceiptId
                    && r.VoucherCode == VoucherCode
                    && (r.ReceiptStatus == null || r.ReceiptStatus != "cancelled"))
                .Select(r => new { r.ZaaerId })
                .FirstOrDefaultAsync(cancellationToken);

            if (receipt?.ZaaerId is not > 0)
            {
                throw new InvalidOperationException($"Deposit receipt {routeReceiptId} was not found or has no zaaer_id.");
            }

            return receipt.ZaaerId.Value;
        }

        private static void TryDeletePhysicalFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", normalized);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        private static PmsDepositImageDto MapImage(DepositImage image, int routeReceiptId) =>
            new()
            {
                DepositImageId = image.DepositImageId,
                ReceiptId = routeReceiptId,
                ReceiptZaaerId = image.ReceiptId,
                ImagePath = image.ImagePath,
                OriginalFilename = image.OriginalFilename,
                FileSize = image.FileSize,
                ContentType = image.ContentType,
                DisplayOrder = image.DisplayOrder,
                CreatedAt = image.CreatedAt
            };
    }
}
