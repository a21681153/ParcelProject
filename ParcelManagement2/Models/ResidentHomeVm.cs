using System.ComponentModel.DataAnnotations;
namespace ParcelManagement2.Models
{
    public class ResidentHomeVm
    {
        public string UserName { get; set; } = string.Empty;
        public string Condo_Id { get; set; } = string.Empty;
        public string ResidentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool HasMultipleMembers { get; set; }
        public int ProfilePhotoCount { get; set; }
    }
    public class ResidentEditVm
    {
        [Required]
        public string ResidentId { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "姓名")]
        public string FullName { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "電話")]
        public string Phone { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "目前密碼")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "新密碼")]
        [StringLength(100, MinimumLength = 6)]
        public string? NewPassword { get; set; }
        public string ConfirmPassword { get; set; } = string.Empty;
        public List<string> ExistingPhotos { get; set; } = new List<string>();
    }
}
