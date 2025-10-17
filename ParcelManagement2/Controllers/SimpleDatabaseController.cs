using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ParcelManagement2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Roles = "Admin")]
    public class SimpleDatabaseController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<SimpleDatabaseController> _logger;
        private readonly IWebHostEnvironment _environment; // ✅ 新增環境檢查

        public SimpleDatabaseController(
            IConfiguration configuration,
            ILogger<SimpleDatabaseController> logger,
            IWebHostEnvironment environment) // ✅ 注入環境服務
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("資料庫連線字串未設定");
            _logger = logger;
            _environment = environment;
        }
        [HttpGet("account-structure")]
        public async Task<IActionResult> CheckAccountStructure()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Account'
            ORDER BY ORDINAL_POSITION";

                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var columns = new List<object>();
                while (await reader.ReadAsync())
                {
                    columns.Add(new
                    {
                        ColumnName = reader["COLUMN_NAME"].ToString(),
                        DataType = reader["DATA_TYPE"].ToString(),
                        IsNullable = reader["IS_NULLABLE"].ToString(),
                        MaxLength = reader["CHARACTER_MAXIMUM_LENGTH"]?.ToString()
                    });
                }

                return Ok(new { Success = true, Columns = columns });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查 Account 表結構失敗");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
        [HttpGet("test-users")]
        public async Task<IActionResult> GetTestUsers()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
            SELECT TOP 5 UserId, Username, Role, Status, condo_id, CreatedAt
            FROM Account
            ORDER BY UserId";

                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var users = new List<object>();
                while (await reader.ReadAsync())
                {
                    users.Add(new
                    {
                        UserId = reader["UserId"],
                        Username = reader["Username"],
                        Role = reader["Role"],
                        Status = reader["Status"],
                        CondoId = reader["condo_id"],
                        CreatedAt = reader["CreatedAt"]
                    });
                }

                return Ok(new { Success = true, UserCount = users.Count, Users = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查測試用戶失敗");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "SimpleDatabaseController 正常運作！", timestamp = DateTime.Now });
        }
        /// <summary>
        /// 基本連線測試（限制資訊暴露）
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT GETDATE() as CurrentTime";
                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var result = new Dictionary<string, object>();
                if (await reader.ReadAsync())
                {
                    result["CurrentTime"] = reader["CurrentTime"];
                    result["Status"] = "Connected";
                }

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "測試資料庫連線失敗");
                return StatusCode(500, new { Success = false, Message = "連線測試失敗", Error = ex.Message });
            }
        }

        [HttpGet("app-tables")]
        public async Task<IActionResult> GetApplicationTables()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT TABLE_NAME as [TableName]
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME IN ('Account', 'Resident', 'Boxdetail', 'Mail')
                      AND TABLE_TYPE = 'BASE TABLE'
                    ORDER BY TABLE_NAME";

                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var tables = new List<string>();
                while (await reader.ReadAsync())
                {
                    var tableName = reader["TableName"]?.ToString();
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        tables.Add(tableName);
                    }
                }

                return Ok(new { Success = true, Tables = tables, Count = tables.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢應用程式表格失敗");
                return StatusCode(500, new { Success = false, Message = "查詢失敗", Error = ex.Message });
            }
        }
    }
}
