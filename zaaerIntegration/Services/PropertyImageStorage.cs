namespace zaaerIntegration.Services
{
    /// <summary>
    /// Saves property (room type / facility) images under <c>wwwroot/uploads/property/{hotelId}/</c>.
    /// </summary>
    public static class PropertyImageStorage
    {
        public const long MaxFileBytes = 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        public sealed record SavedImage(string RelativePath, string ContentType, long FileSize);

        public static bool IsAllowedExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
        }

        public static async Task<SavedImage> SaveAsync(
            IFormFile file,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ArgumentException("Image file is empty.");
            }

            if (file.Length > MaxFileBytes)
            {
                throw new ArgumentException($"Image exceeds maximum size ({MaxFileBytes / (1024 * 1024)} MB).");
            }

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName) || !IsAllowedExtension(originalName))
            {
                throw new ArgumentException("Only image files (JPG, PNG, GIF, WebP, BMP) are allowed.");
            }

            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            var hotelFolder = hotelId > 0 ? hotelId.ToString() : "0";
            var uploadsRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "property",
                hotelFolder);
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);
            var relativePath = $"/uploads/property/{hotelFolder}/{fileName}";

            await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? GuessContentType(ext)
                : file.ContentType;

            return new SavedImage(relativePath, contentType, file.Length);
        }

        private static string GuessContentType(string ext) =>
            ext switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
    }
}
