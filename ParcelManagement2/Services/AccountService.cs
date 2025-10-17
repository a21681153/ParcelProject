using Microsoft.Data.SqlClient;
using ParcelManagement2.Models;
using System.Data;
using System.Text;

namespace ParcelManagement2.Services
{
    public interface IAccountService
    {
        Account? GetByUsername(string username);
        string UpdatePassword(Guid userId, string plainPwd);
        bool VerifyPassword(string username, string plainPwd);
    }

    public class AccountService : IAccountService
    {
        private readonly string _connectionString;
        private readonly ILogger<AccountService> _logger;

        public AccountService(IConfiguration configuration, ILogger<AccountService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("資料庫連線字串未設定");
            _logger = logger;
        }
        /* 取單筆帳號 -------------------------------------------------- */
        public Account GetByUsername(string username)
        {
            try
            {
                const string sql = @"
                SELECT TOP 1 UserId, Username, PasswordHash, Role, condo_id, Status, CreatedAt
                FROM   Account
                WHERE  Username = @u";

                using var connection = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@u", username);
                connection.Open();

                using var rd = cmd.ExecuteReader();
                if (!rd.Read()) return null;

                return new Account
                {
                    Id = (Guid)rd["UserId"],
                    Username = rd["Username"]?.ToString() ?? string.Empty,
                    PasswordHash = rd["PasswordHash"]?.ToString() ?? string.Empty,
                    Role = rd["Role"]?.ToString() ?? string.Empty,
                    Condo_Id = rd["condo_id"]?.ToString(),
                    Status = rd["Status"]?.ToString() ?? string.Empty,
                    CreatedAt = rd["CreatedAt"] is DBNull ? DateTime.MinValue : (DateTime)rd["CreatedAt"]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得用戶資料失敗: {Username}", username);
                return null;
            }
        }
        /*更新密碼*/
        public string UpdatePassword(Guid userId, string plainPwd)
        {
            try
            {
                string hash = ComputeSha256Hash(plainPwd);
                string sql = "UPDATE Account SET PasswordHash = @p WHERE UserId = @userId";

                using var connection = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(sql, connection);
                
                cmd.Parameters.AddWithValue("@p", hash);
                cmd.Parameters.AddWithValue("@userId", userId);

                connection.Open();
                var rowsAffected = cmd.ExecuteNonQuery();
                
                _logger.LogInformation("密碼更新成功，影響 {RowsAffected} 筆記錄", rowsAffected);
                return "OK";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新密碼失敗: UserId={UserId}", userId);
                throw;
            }
        }

        /* 驗證帳密 ---------------------------------------------------- */
        public bool VerifyPassword(string username, string plainPwd)
        {
            var acct = GetByUsername(username);
            if (acct == null || acct.Status != "Approved")
                return false;

            string hash = ComputeSha256Hash(plainPwd);      // 與資料庫比對
            return string.Equals(hash, acct.PasswordHash, StringComparison.OrdinalIgnoreCase);
        }

        /* 共用 SHA-256 ------------------------------------------------ */
        private string ComputeSha256Hash(string rawData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                return string.Concat(bytes.Select(b => b.ToString("x2")));  // 小寫 64 碼
            }
        }
    }
}
