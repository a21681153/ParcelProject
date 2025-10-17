namespace ParcelManagement2.Models
{
    public class PackagePhotoVm
    {
        public string PackageId { get; set; } = "";
        public string PackageName { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string RecipientId { get; set; } = "";
        public DateTime CreateTime { get; set; }
        public List<string> Photos { get; set; } = new List<string>();
    }
}
