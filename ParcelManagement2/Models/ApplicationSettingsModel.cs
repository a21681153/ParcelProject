namespace ParcelManagement2.Models
{
    public class ApplicationSettingsModel
    {
        public int DefaultPageSize { get; set; } = 20;
        public long MaxFileUploadSize { get; set; } = 5242880; // 5MB
        public string[] AllowedFileExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        public FileUploadSettings FileUpload { get; set; } = new();
    }

    public class FileUploadSettings
    {
        public PackagePhotoSettings PackagePhotos { get; set; } = new();
    }

    public class PackagePhotoSettings
    {
        public long MaxFileSizeBytes { get; set; } = 5242880; // 5MB
        public string[] AllowedExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        public string UploadPath { get; set; } = "uploads/packages";
        public int MaxFilesPerPackage { get; set; } = 1;
    }
}
