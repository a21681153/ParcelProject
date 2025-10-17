using System.ComponentModel.DataAnnotations;

namespace ParcelManagement2.Models.Request
{
    public class AccountRequest
    {
        public class LoginDto
        {
            [Required(ErrorMessage = "使用者名稱為必填")]
            [Display(Name = "使用者名稱")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "密碼為必填")]
            [Display(Name = "密碼")]
            public string Password { get; set; } = string.Empty;
        }
    }
}
