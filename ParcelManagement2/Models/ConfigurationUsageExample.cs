using Microsoft.Extensions.Options;

namespace ParcelManagement2.Models
{
    /// <summary>
    /// 配置使用範例類別
    /// 展示如何在各種服務中使用 appsettings 配置
    /// </summary>
    public class ConfigurationUsageExample
    {
        private readonly IConfiguration _configuration;
        private readonly WebPagesSettings _webPagesSettings;
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationSettings _applicationSettings;

        public ConfigurationUsageExample(
            IConfiguration configuration,
            IOptions<WebPagesSettings> webPagesOptions,
            IOptions<JwtSettings> jwtOptions,
            IOptions<ApplicationSettings> applicationOptions)
        {
            _configuration = configuration;
            _webPagesSettings = webPagesOptions.Value;
            _jwtSettings = jwtOptions.Value;
            _applicationSettings = applicationOptions.Value;
        }

        /// <summary>
        /// 範例方法：取得資料庫連線字串
        /// </summary>
        public string GetDatabaseConnectionString()
        {
            return _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        /// <summary>
        /// 範例方法：檢查 WebPages 是否啟用
        /// </summary>
        public bool IsWebPagesEnabled()
        {
            return _webPagesSettings.Enabled;
        }

        /// <summary>
        /// 範例方法：取得 JWT 簽章密鑰
        /// </summary>
        public string GetJwtSecret()
        {
            return _jwtSettings.JwtSecret;
        }

        /// <summary>
        /// 範例方法：建立 JWT Token 設定
        /// </summary>
        public object CreateJwtTokenConfiguration()
        {
            return new
            {
                Secret = _jwtSettings.JwtSecret,
                Issuer = _jwtSettings.JwtIssuer,
                Audience = _jwtSettings.JwtAudience,
                Key = _jwtSettings.JwtKey
            };
        }

        /// <summary>
        /// 範例方法：檢查客戶端驗證是否啟用
        /// </summary>
        public bool IsClientValidationEnabled()
        {
            return _applicationSettings.ClientValidationEnabled;
        }

        /// <summary>
        /// 範例方法：取得所有應用程式設定
        /// </summary>
        public object GetAllApplicationSettings()
        {
            return new
            {
                ClientValidationEnabled = _applicationSettings.ClientValidationEnabled,
                UnobtrusiveJavaScriptEnabled = _applicationSettings.UnobtrusiveJavaScriptEnabled,
                EnableSimpleMembership = _applicationSettings.EnableSimpleMembership
            };
        }
    }
}
