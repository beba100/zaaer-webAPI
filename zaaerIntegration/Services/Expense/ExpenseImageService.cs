#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Models;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Expense
{
    public interface IExpenseImageService
    {
        Task<IReadOnlyList<PmsExpenseImageDto>> GetImagesAsync(long expenseId, int hotelZaaerId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsExpenseImageDto>> UploadImagesAsync(
            long expenseId,
            int hotelZaaerId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteImageAsync(long expenseId, int imageId, int hotelZaaerId, CancellationToken cancellationToken = default);
    }

    public sealed class ExpenseImageService : IExpenseImageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExpenseImageService> _logger;

        public ExpenseImageService(ApplicationDbContext context, ILogger<ExpenseImageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PmsExpenseImageDto>> GetImagesAsync(
            long expenseId,
            int hotelZaaerId,
            CancellationToken cancellationToken = default)
        {
            await EnsureExpenseExistsAsync(expenseId, hotelZaaerId, cancellationToken);

            var images = await _context.ExpenseImages
                .AsNoTracking()
                .Where(i => i.ExpenseId == expenseId)
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.ExpenseImageId)
                .ToListAsync(cancellationToken);

            return images.Select(MapImage).ToList();
        }

        public async Task<IReadOnlyList<PmsExpenseImageDto>> UploadImagesAsync(
            long expenseId,
            int hotelZaaerId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default)
        {
            if (images == null || images.Count == 0)
            {
                throw new ArgumentException("No images provided.");
            }

            await EnsureExpenseExistsAsync(expenseId, hotelZaaerId, cancellationToken);

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "expenses");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var displayOrder = await _context.ExpenseImages
                .Where(i => i.ExpenseId == expenseId)
                .Select(i => (int?)i.DisplayOrder)
                .MaxAsync(cancellationToken) ?? 0;

            var oldExpenseId = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.ExpenseId == expenseId)
                .Select(e => e.OldExpenseId)
                .FirstOrDefaultAsync(cancellationToken);

            if (oldExpenseId <= 0)
            {
                throw new InvalidOperationException(
                    $"Expense {expenseId} has no old_expense_id — cannot insert expense_images row.");
            }

            var uploaded = new List<PmsExpenseImageDto>();

            foreach (var image in images)
            {
                if (image.Length <= 0)
                {
                    continue;
                }

                displayOrder++;
                var fileName = $"{expenseId}_{KsaTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativePath = $"/uploads/expenses/{fileName}";

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream, cancellationToken);
                }

                var entity = new ExpenseImage
                {
                    ExpenseId = expenseId,
                    OldExpenseId = oldExpenseId,
                    ImagePath = relativePath,
                    OriginalFilename = image.FileName,
                    FileSize = image.Length,
                    ContentType = image.ContentType,
                    DisplayOrder = displayOrder,
                    CreatedAt = KsaTime.Now
                };

                await _context.ExpenseImages.AddAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                uploaded.Add(MapImage(entity));
            }

            _logger.LogInformation("Uploaded {Count} images for ExpenseId={ExpenseId}", uploaded.Count, expenseId);
            return uploaded;
        }

        public async Task<bool> DeleteImageAsync(
            long expenseId,
            int imageId,
            int hotelZaaerId,
            CancellationToken cancellationToken = default)
        {
            await EnsureExpenseExistsAsync(expenseId, hotelZaaerId, cancellationToken);

            var image = await _context.ExpenseImages
                .FirstOrDefaultAsync(i => i.ExpenseImageId == imageId && i.ExpenseId == expenseId, cancellationToken);

            if (image == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(image.ImagePath))
            {
                var physicalPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }

            _context.ExpenseImages.Remove(image);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task EnsureExpenseExistsAsync(long expenseId, int hotelZaaerId, CancellationToken cancellationToken)
        {
            var exists = await _context.Expenses
                .AsNoTracking()
                .AnyAsync(e => e.ExpenseId == expenseId && e.HotelId == hotelZaaerId, cancellationToken);

            if (!exists)
            {
                throw new KeyNotFoundException($"Expense with id {expenseId} not found.");
            }
        }

        private static PmsExpenseImageDto MapImage(ExpenseImage image) =>
            new()
            {
                ExpenseImageId = image.ExpenseImageId,
                ImagePath = image.ImagePath,
                OriginalFilename = image.OriginalFilename,
                FileSize = image.FileSize,
                ContentType = image.ContentType,
                DisplayOrder = image.DisplayOrder,
                CreatedAt = image.CreatedAt
            };
    }
}
