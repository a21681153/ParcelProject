# 資料庫連線使用說明

## 📋 概述
本專案已設定好使用 `Microsoft.Data.SqlClient` 進行資料庫連線，您可以直接使用 `SqlConnection` 來存取資料庫。

## 🔧 設定
資料庫連線字串已在 `appsettings.json` 中設定：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.,1433;Database=mailTest;User ID=DBadm;Password=87654321;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

## 💻 基本使用方式

### 1. 在控制器中取得連線字串
```csharp
public class YourController : ControllerBase
{
    private readonly string _connectionString;

    public YourController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }
}
```

### 2. 基本查詢
```csharp
using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync();

var sql = "SELECT * FROM YourTable WHERE Id = @Id";
using var command = new SqlCommand(sql, connection);
command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = 1 });

using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var data = reader["ColumnName"].ToString();
    // 處理資料...
}
```

### 3. 執行非查詢 SQL (INSERT/UPDATE/DELETE)
```csharp
using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync();

var sql = "INSERT INTO YourTable (Name, Email) VALUES (@Name, @Email)";
using var command = new SqlCommand(sql, connection);
command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar) { Value = "John" });
command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar) { Value = "john@example.com" });

var rowsAffected = await command.ExecuteNonQueryAsync();
```

### 4. 取得單一值 (ExecuteScalar)
```csharp
using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync();

var sql = "SELECT COUNT(*) FROM YourTable";
using var command = new SqlCommand(sql, connection);

var count = await command.ExecuteScalarAsync();
var result = Convert.ToInt32(count);
```

## 🚀 測試 API 端點

專案包含 `SimpleDatabaseController` 提供以下測試端點：

- `GET /api/simpledatabase/test-connection` - 測試資料庫連線
- `GET /api/simpledatabase/query-example` - 參數化查詢範例
- `POST /api/simpledatabase/execute-example` - 執行自訂 SQL
- `GET /api/simpledatabase/scalar-example` - Scalar 查詢範例
- `GET /api/simpledatabase/list-tables` - 列出所有資料表
- `GET /api/simpledatabase/table-exists/{tableName}` - 檢查表格是否存在

## 🔒 安全性注意事項

1. **始終使用參數化查詢**避免 SQL Injection：
   ```csharp
   // ✅ 正確
   command.Parameters.Add(new SqlParameter("@Id", id));
   
   // ❌ 錯誤
   var sql = $"SELECT * FROM Users WHERE Id = {id}";
   ```

2. **使用 using 語句**確保資源正確釋放：
   ```csharp
   using var connection = new SqlConnection(_connectionString);
   using var command = new SqlCommand(sql, connection);
   ```

3. **錯誤處理**：
   ```csharp
   try
   {
       // 資料庫操作
   }
   catch (SqlException ex)
   {
       _logger.LogError(ex, "資料庫操作失敗");
       // 處理錯誤
   }
   ```

## 📦 必要套件
專案已包含必要的 NuGet 套件：
```xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.2" />
```

## 💡 使用建議

1. 使用非同步方法 (`OpenAsync`, `ExecuteReaderAsync` 等)
2. 適當使用事務處理複雜操作
3. 實作連線池設定以提高效能
4. 記錄所有資料庫操作以便除錯

現在您可以直接使用 `Microsoft.Data.SqlClient` 來存取您的 mailTest 資料庫了！
