namespace ParcelManagement2.Models
{
    public class WebPagesSettings
    {
        public string Version { get; set; } = "1.0.0.0";
        public bool Enabled { get; set; } = false;
    }

    public class JwtSettings
    {
        public string JwtSecret { get; set; } = string.Empty;
        public string JwtIssuer { get; set; } = string.Empty;
        public string JwtAudience { get; set; } = string.Empty;
        public string JwtKey { get; set; } = string.Empty;
        public int ExpirationInMinutes { get; set; } = 30;
    }

    public class ApplicationSettings
    {
        public bool ClientValidationEnabled { get; set; }
        public bool UnobtrusiveJavaScriptEnabled { get; set; }
        public bool EnableSimpleMembership { get; set; }
    }
}
