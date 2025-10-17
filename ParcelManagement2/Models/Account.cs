namespace ParcelManagement2.Models
{
    public class Account
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;     // Admin / Resident
        public string Condo_Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;     // Pending / Approved / Rejected
        public DateTime CreatedAt { get; set; }
    }
}
