using System.ComponentModel.DataAnnotations;

namespace ParcelManagement2.Models
{
    public class TokenRequest
    {
        [Required(ErrorMessage = "使用者名稱為必填")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "使用者名稱長度必須介於 3 到 50 個字元")]
        public string Username { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "角色名稱不能超過 20 個字元")]
        public string? Role { get; set; }

        [StringLength(50, ErrorMessage = "社區ID不能超過 50 個字元")]
        public string? CondoId { get; set; }
    }
}
