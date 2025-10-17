using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;
using System.Text.Json;

namespace ParcelManagement2.Models
{
    public interface IDBConn
    {
        Task<string> ExecSQLAsync(string sql, IDictionary<string, object>? parameters = null);
        Task<DataTable> GetDataTableAsync(string sql, IDictionary<string, object>? parameters = null);
        Task<string> GetReaderAsync(string columnName, string sql, IDictionary<string, object>? parameters = null);
        string DataTableToJsonString(DataTable table);
    }

    public class DBConn : IDBConn
    {
        private readonly string _connectionString;
        private readonly ILogger<DBConn> _logger;

        public DBConn(IConfiguration configuration, ILogger<DBConn> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("連線字串 DefaultConnection 未設定！");
            _logger = logger;

            // 移除 Debug 輸出，使用適當的日志
            _logger.LogDebug("資料庫連線字串已設定");
        }

        /// <summary>
        /// 執行非查詢 SQL 命令（INSERT、UPDATE、DELETE）
        /// </summary>
        public async Task<string> ExecSQLAsync(string sql, IDictionary<string, object>? parameters = null)
        {
            try
            {
                // ✅ 每次都建立新的連線，確保執行緒安全
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(sql, connection);

                // 添加參數
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                await connection.OpenAsync();
                int rowsAffected = await command.ExecuteNonQueryAsync();

                _logger.LogDebug("SQL 執行成功，影響 {Rows} 行", rowsAffected);
                return "OK";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行 SQL 命令失敗: {SQL}", sql);
                return $"錯誤: {ex.Message}";
            }
        }

        /// <summary>
        /// 同步版本，向後相容
        /// </summary>
        public string ExecSQL(string sql, IDictionary<string, object>? parameters = null)
        {
            return ExecSQLAsync(sql, parameters).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 執行查詢並返回 DataTable
        /// </summary>
        public async Task<DataTable> GetDataTableAsync(string sql, IDictionary<string, object>? parameters = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(sql, connection);

                // 添加參數
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                await connection.OpenAsync();
                // ✅ 使用 Fill 方法，自動處理連線
                adapter.Fill(dataTable);

                _logger.LogDebug("查詢成功，返回 {Rows} 行數據", dataTable.Rows.Count);
                return dataTable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行查詢失敗: {SQL}", sql);
                throw; // 重新拋出異常，讓調用方處理
            }
        }

        /// <summary>
        /// 同步版本，向後相容
        /// </summary>
        public DataTable GetDataTable(string sql, IDictionary<string, object>? parameters = null)
        {
            return GetDataTableAsync(sql, parameters).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 獲取單個值
        /// </summary>
        public async Task<string> GetReaderAsync(string columnName, string sql, IDictionary<string, object>? parameters = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(sql, connection);

                // 添加參數
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var value = reader[columnName]?.ToString()?.Trim() ?? string.Empty;
                    _logger.LogDebug("成功獲取單個值: {Value}", value);
                    return value;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行單值查詢失敗: {SQL}", sql);
                throw;
            }
        }

        /// <summary>
        /// 同步版本，向後相容
        /// </summary>
        public string getReader(string columnName, string sql)
        {
            return GetReaderAsync(columnName, sql).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 將 DataTable 轉換為 JSON 字串
        /// </summary>
        public string DataTableToJsonString(DataTable table)
        {
            try
            {
                if (table?.Rows.Count == 0)
                    return "[]";

                var rows = new List<Dictionary<string, object>>();

                foreach (DataRow row in table.Rows)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in table.Columns)
                    {
                        var value = row[col] == DBNull.Value ? null : row[col];

                        if (col.ColumnName.ToLower() == "status" && value != null)
                        {
                            dict[col.ColumnName] = value;

                            int statusValue = Convert.ToInt32(value);
                            dict["status_text"] = statusValue == 0 ? "未取" : "已取";
                        }
                        else
                        {
                            dict[col.ColumnName] = value;
                        }
                    }
                    rows.Add(dict);
                }

                return JsonSerializer.Serialize(rows, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON 序列化失敗");
                return "[]";
            }
        }

        /// <summary>
        /// 字串引用轉義（保留向後相容性，但建議使用參數化查詢）
        /// </summary>
        [Obsolete("建議使用參數化查詢代替字串拼接")]
        public string qo(string? instr)
        {
            if (string.IsNullOrEmpty(instr))
                return "NULL";
            return "'" + instr.Replace("'", "''") + "'";
        }

        // ✅ 移除不安全和過時的方法
        // - getSingleRowData (使用複雜的事務邏輯，容易出錯)
        // - GetDataSet (DataSet 已過時，推薦使用 DataTable 或 Entity Framework)
        // - 其他雜項方法，應該放在適當的 utility 類別中

        /// <summary>
        /// 獲取當前時間
        /// </summary>
        public string GetTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
