using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class ImageService
    {
        private readonly IWebHostEnvironment _environment;
        private static readonly long _maxFileSize = 5 * 1024 * 1024;
        private static readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
        public ImageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }
        public async Task<string?> SaveImageAsync(IFormFile? image,string subFolder = "avatars")
        {
            if (image == null || image.Length == 0) return null;

            if (image.Length > _maxFileSize)
                throw new ValidationException("Image file size exceeds 5MB.");

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!_allowedImageExtensions.Contains(extension))
                throw new ValidationException("Only JPG, JPEG, and PNG files are allowed.");

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString("N") + extension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            return $"/uploads/{subFolder}/{uniqueFileName}";
        }

        public void DeleteImage(string relativePath)
        {
            var filePath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/'));
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
