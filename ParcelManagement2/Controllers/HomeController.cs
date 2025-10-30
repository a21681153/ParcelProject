using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ParcelManagement2.Models;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace ParcelManagement2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly DBConn _dbConn;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationSettingsModel _appSettings;
        private readonly HttpClient _httpClient;
        private readonly string _lineBotApiUrl;
        public HomeController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            DBConn dbConn,
            IWebHostEnvironment webHostEnvironment,
            IOptions<ApplicationSettingsModel> appSettings,
            HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _dbConn = dbConn;
            _webHostEnvironment = webHostEnvironment;
            _appSettings = appSettings.Value;
            _httpClient = httpClient; 
            _lineBotApiUrl = _configuration["LineBotApiUrl"] ?? "http://localhost:5181"; // ** 新增：從設定檔讀取 LINE Bot API URL **
        }
        public IActionResult Index()
        {
            ViewBag.Title = "社區包裹統計首頁";
            return View();
        }
        public IActionResult Packages()
        {
            ViewBag.Message = "社區包裹管理";
            ViewBag.Title = "社區包裹管理系統";
            return View();
        }
        // 文件上傳處理方法
        private async Task<string> SaveUploadedFile(IFormFile file, string packId)
        {
            if (file == null || file.Length == 0)
                return string.Empty;
            var photoSettings = _appSettings.FileUpload.PackagePhotos;
            // 檢查文件類型
            string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!photoSettings.AllowedExtensions.Contains(fileExtension))
            {
                string allowedExts = string.Join(", ", photoSettings.AllowedExtensions);
                throw new ArgumentException("不支援的檔案格式，僅支援 : {allowedTypes}");
            }
                

            // 檢查文件大小 (5MB)
            if (file.Length > photoSettings.MaxFileSizeBytes)
            {
                double maxSizeMB = photoSettings.MaxFileSizeBytes / (1024.0 * 1024.0);
                throw new ArgumentException($"檔案大小不能超過 {maxSizeMB:F1}MB");
            }

            // 創建上傳目錄
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, photoSettings.UploadPath);
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            // 生成唯一檔名
            string fileName = $"{packId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
            string filePath = Path.Combine(uploadDir, fileName);

            // 保存文件
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 返回相對路徑
            return $"/{photoSettings.UploadPath.Replace("\\", "/")}/{fileName}";
        }
        private string GenerateDailyPackageId()
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Database connection string not configured");

                using var cn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand("GetDailySequence", cn) { CommandType = CommandType.StoredProcedure };

                var outputParam = new SqlParameter("@NewId", SqlDbType.Char, 11) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(outputParam);

                cn.Open();
                cmd.ExecuteNonQuery();

                return outputParam.Value?.ToString() ?? DateTime.Now.ToString("yyyyMMddHHmm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "產生包裹編號失敗");
                return DateTime.Now.ToString("yyyyMMddHHmm");
            }
        }

        // 新增包裹
        [HttpPost]
        public async Task<IActionResult> AddProduct()
        {
            try
            {
                string? packTypeValue = Request.Form["Pack_Type"];
                string? redIdValue = Request.Form["Red_Id"];
                string? remarksValue = Request.Form["Remarks"];
                IFormFile? photoFile = Request.Form.Files.GetFile("photo");

                string Pack_Type = packTypeValue ?? "";
                string Red_Id = redIdValue ?? "";
                string Remarks = string.IsNullOrWhiteSpace(remarksValue) ? "-" : remarksValue.Trim();

                _logger.LogInformation("新增包裹: Pack_Type={PackType}, Red_Id={RedId}", Pack_Type, Red_Id);

                if (string.IsNullOrEmpty(Pack_Type) || string.IsNullOrEmpty(Red_Id))
                {
                    return Json(new[] { new { msg = "FAIL", err = "類別及住戶必選" } });
                }

                // 產生流水號
                string newPackId = GenerateDailyPackageId();
                _logger.LogInformation("產生包裹編號: {PackId}", newPackId);

                // 處理照片上傳
                string photoPath = "";
                if (photoFile != null && photoFile.Length > 0)
                {
                    try
                    {
                        photoPath = await SaveUploadedFile(photoFile, newPackId);
                        _logger.LogInformation("照片上傳成功: {PhotoPath}", photoPath);
                    }
                    catch (ArgumentException ex)
                    {
                        return Json(new[] { new { msg = "FAIL", err = ex.Message } });
                    }
                }

                const string sqlstr = @"
                    INSERT INTO Boxdetail 
                    (pack_id, pack_type, red_id, status, create_time, deleted, photo_path,remarks) 
                    VALUES 
                    (@id, @type, @rid, 0, GETDATE(), 0, @photo, @remarks)";

                var parameters = new Dictionary<string, object>
                {
                    ["@id"] = newPackId,
                    ["@type"] = Pack_Type,
                    ["@rid"] = Red_Id,
                    ["@photo"] = photoPath,
                    ["@remarks"] = Remarks
                };

                string result = await _dbConn.ExecSQLAsync(sqlstr, parameters);
                _logger.LogInformation("插入結果: {Result}", result);

                if (result == "OK")
                {
                    bool shouldNotify = await ShouldSendNotification(Pack_Type);
                    if (shouldNotify)
                    {
                        string condoId = await GetCondoIdByRedId(Red_Id);
                        if (!string.IsNullOrEmpty(condoId))
                        {
                            // 呼叫 LINE Bot 推播 API 
                            await SendLineNotification(newPackId, condoId);
                        }
                    }
                    return Json(new[] { new { msg = "OK", pack_id = newPackId } });
                }
                else
                {
                    return Json(new[] { new { msg = "FAIL", err = "新增失敗: " + result } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新增包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        private async Task<bool> ShouldSendNotification(string packType)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                using var command = new SqlCommand(
                    "SELECT pack_name FROM Mail WHERE pack_type = @packType",
                    connection);
                command.Parameters.AddWithValue("@packType", packType);

                var packName = (await command.ExecuteScalarAsync())?.ToString();

                bool isPackage = packName == "包裹";

                _logger.LogInformation("包裹類型: {PackType}, 名稱: {PackName}, 是否推播: {ShouldNotify}",
                    packType, packName, isPackage);

                return isPackage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查包裹類型失敗: {PackType}", packType);
                // 發生錯誤時預設不推播,避免誤發通知
                return false;
            }
        }
        private async Task<string> GetCondoIdByRedId(string redId)
        {
            try
            {
                const string sqlstr = "SELECT condo_id FROM Resident WHERE red_id = @redId";
                var parameters = new Dictionary<string, object>
                {
                    ["@redId"] = redId
                };

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                using var command = new SqlCommand(sqlstr, connection);
                command.Parameters.AddWithValue("@redId", redId);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得 condo_id 失敗");
                return "";
            }
        }
        //呼叫 LINE Bot 推播 API 
        private async Task SendLineNotification(string packId, string condoId)
        {
            try
            {
                // 準備要發送的資料
                var requestData = new
                {
                    PackId = packId,
                    CondoId = condoId
                };

                // 序列化為 JSON
                string jsonContent = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 呼叫 LINE Bot API
                string apiUrl = $"{_lineBotApiUrl}/api/LineBot/push";
                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("LINE 推播成功: {Response}", responseBody);
                }
                else
                {
                    _logger.LogWarning("LINE 推播失敗: StatusCode={StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE 推播發生錯誤");
                // 不影響主要流程,只記錄錯誤
            }
        }
        // 取得該包裹所屬戶的所有住戶
        [HttpPost]
        public async Task<IActionResult> GetResidentsByPackId([FromBody] JsonElement requestData)
        {
            try
            {
                string packId = requestData.GetProperty("packId").GetString() ?? "";

                _logger.LogInformation("查詢包裹住戶: PackId={PackId}", packId);

                string sql = @"
                    SELECT r.red_id, r.red_name, r.condo_id
                    FROM Resident r
                    WHERE r.condo_id = (
                    SELECT r2.condo_id 
                    FROM Boxdetail b
                    INNER JOIN Resident r2 ON b.red_id = r2.red_id
                    WHERE b.pack_id = @packId
                    )
                    ORDER BY r.red_name";

                var parameters = new Dictionary<string, object> { ["@packId"] = packId };
                DataTable dataTable = await _dbConn.GetDataTableAsync(sql, parameters);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得住戶清單失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }// 確認包裹領取
        [HttpPost]
        public async Task<IActionResult> ConfirmPackagePickup([FromBody] JsonElement requestData)
        {
            try
            {
                string packId = requestData.GetProperty("packId").GetString() ?? "";
                string redId = "";

                if (requestData.TryGetProperty("redId", out var redIdProp))
                {
                    redId = redIdProp.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(packId))
                {
                    return Json(new[] { new { msg = "FAIL", err = "包裹編號不能為空" } });
                }
                string collectorName = "";
                if (!string.IsNullOrEmpty(redId))
                {
                    const string getNameByRedIdSql = "SELECT red_name FROM Resident WHERE red_id = @redId";
                    var nameDt = await _dbConn.GetDataTableAsync(getNameByRedIdSql,
                        new Dictionary<string, object> { ["@redId"] = redId });
                    if (nameDt.Rows.Count > 0)
                        collectorName = nameDt.Rows[0]["red_name"]?.ToString() ?? "";
                }
                else
                {
                    const string getNameByPackIdSql = @"
                        SELECT r.red_name
                        FROM Boxdetail b
                        LEFT JOIN Resident r ON r.red_id = b.red_id
                        WHERE b.pack_id = @packId";
                    var nameDt = await _dbConn.GetDataTableAsync(getNameByPackIdSql,
                        new Dictionary<string, object> { ["@packId"] = packId });
                    if (nameDt.Rows.Count > 0)
                        collectorName = nameDt.Rows[0]["red_name"]?.ToString() ?? "";
                }

                string sql = "UPDATE Boxdetail SET status = 1, pickup_datetime = GETDATE(), collector_name = @collectorName WHERE pack_id = @packId";
                var parameters = new Dictionary<string, object> { ["@packId"] = packId , ["@collectorName"] = (object?)collectorName ?? DBNull.Value };

                string result = await _dbConn.ExecSQLAsync(sql, parameters);

                if (result == "OK")
                {
                    return Json(new[] { new { msg = "OK", collector = collectorName } });
                }
                else
                {
                    return Json(new[] { new { msg = "FAIL", err = "更新失敗: " + result } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "確認領取失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        // 建立包裹類別下拉選單 (對應Mail表格)
        [HttpPost]
        public async Task<JsonResult> BuildProdTypeList()
        {
            try
            {
                string sql = "SELECT pack_type, pack_name FROM Mail ORDER BY pack_type";
                DataTable dataTable = await _dbConn.GetDataTableAsync(sql);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得包裹類別清單失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        // 獲取包裹照片
        [HttpGet]
        public async Task<IActionResult> GetPackagePhoto(string packId)
        {
            try
            {
                string sql = @"
                    SELECT photo_path, image_data 
                    FROM Boxdetail 
                    WHERE pack_id = @packId";
                var parameters = new Dictionary<string, object> { ["@packId"] = packId };

                DataTable dataTable = await _dbConn.GetDataTableAsync(sql, parameters);

                if (dataTable.Rows.Count > 0)
                {
                    var row = dataTable.Rows[0];
                    var photos = new List<object>();

                    // 檔案路徑照片
                    string photoPath = row["photo_path"]?.ToString();
                    if (!string.IsNullOrEmpty(photoPath))
                    {
                        photos.Add(new { type = "file", url = photoPath });
                    }

                    // 二進制照片
                    if (row["image_data"] != DBNull.Value && row["image_data"] != null)
                    {
                        byte[] imageData = (byte[])row["image_data"];
                        if (imageData.Length > 0)
                        {
                            string base64String = Convert.ToBase64String(imageData);
                            string mimeType = GetImageMimeType(imageData);
                            string dataUrl = $"data:{mimeType};base64,{base64String}";
                            photos.Add(new { type = "binary", url = dataUrl });
                        }
                    }
                    return Json(new { success = true, photos = photos });
                }
                else
                {
                    return Json(new { success = false, message = "包裹不存在" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取包裹照片失敗");
                return Json(new { success = false, message = "獲取照片失敗", error = ex.Message });
            }
        }
        private string GetImageMimeType(byte[] imageData)
        {
            if (imageData.Length < 4) return "image/jpeg";

            // 檢查檔案頭判斷圖片格式
            if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
                return "image/jpeg";
            if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
                return "image/png";
            if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
                return "image/gif";
            if (imageData[0] == 0x42 && imageData[1] == 0x4D)
                return "image/bmp";

            return "image/jpeg"; // 預設為 JPEG
        }
        // 建立住戶下拉選單 (對應Resident表格)
        [HttpPost]
        public async Task<JsonResult> BuildResidentList()
        {
            try
            {
                string sql = "SELECT red_id, red_name, phone, condo_id FROM Resident ORDER BY red_id";
                DataTable dataTable = await _dbConn.GetDataTableAsync(sql);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得住戶清單失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        // === 依住戶編號查詢包裹 =====================================
        [HttpPost]
        public async Task<IActionResult> GetPackagesByResident([FromBody] JsonElement requestData)
        {
            try
            {
                string redId = requestData.GetProperty("redId").GetString() ?? "";
                string statusFilter = "";
                if (requestData.TryGetProperty("status", out var statusProp))
                {
                    statusFilter = statusProp.GetString() ?? "";
                }

                _logger.LogInformation("查詢住戶包裹: RedId={RedId}, Status={Status}", redId, statusFilter);

                string sql = @"
                    SELECT b.pack_id, b.pack_type, b.red_id, b.status, b.create_time, b.pickup_datetime, b.photo_path, 
                           b.remarks,
                           m.pack_name, r.red_name, r.phone, r.condo_id
                    FROM Boxdetail b
                    LEFT JOIN Mail m ON b.pack_type = m.pack_type
                    LEFT JOIN Resident r ON b.red_id = r.red_id
                    WHERE b.red_id = @redId AND ISNULL(b.deleted, 0) = 0";

                var parameters = new Dictionary<string, object> { ["@redId"] = redId };

                // 根據狀態篩選
                if (statusFilter == "0" || statusFilter == "未取")
                {
                    sql += " AND b.status = 0";
                }
                else if (statusFilter == "1" || statusFilter == "已取")
                {
                    sql += " AND b.status = 1";
                }

                sql += " ORDER BY b.create_time DESC";

                DataTable dataTable = await _dbConn.GetDataTableAsync(sql, parameters);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢住戶包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        [HttpPost]
        public async Task<IActionResult> BatchUpdateProduct([FromBody] JsonElement[] payload)
        {
            try
            {
                _logger.LogInformation("開始批次更新包裹，共 {Count} 筆", payload.Length);

                foreach (var item in payload)
                {
                    string packId = item.GetProperty("Pack_Id").GetString() ?? "";
                    bool flagUpdate = item.GetProperty("Flag_Update").GetBoolean();
                    bool flagDel = item.GetProperty("Flag_Del").GetBoolean();

                    // 處理 Status 欄位（可能是數字或字串）
                    int status = 0;
                    if (item.TryGetProperty("Status", out var statusProp))
                    {
                        if (statusProp.ValueKind == JsonValueKind.Number)
                        {
                            status = statusProp.GetInt32();
                        }
                        else if (statusProp.ValueKind == JsonValueKind.String)
                        {
                            string statusStr = statusProp.GetString() ?? "";
                            // 轉換字串狀態為數字
                            status = statusStr switch
                            {
                                "未取" => 0,
                                "已取" => 1,
                                _ => int.TryParse(statusStr, out int parsed) ? parsed : 0
                            };
                        }
                    }

                    _logger.LogInformation("處理包裹: {PackId}, Update={Update}, Delete={Delete}, Status={Status}",
                        packId, flagUpdate, flagDel, status);

                    if (flagDel)
                    {
                        // 軟刪除
                        string deleteSql = "UPDATE Boxdetail SET deleted = 1 WHERE pack_id = @id";
                        var deleteParams = new Dictionary<string, object> { ["@id"] = packId };
                        await _dbConn.ExecSQLAsync(deleteSql, deleteParams);
                    }
                    else if (flagUpdate)
                    {
                        // 更新狀態
                        string updateSql = status == 1
                            ? "UPDATE Boxdetail SET status = @status, pickup_datetime = GETDATE() WHERE pack_id = @id"
                            : "UPDATE Boxdetail SET status = @status, pickup_datetime = NULL WHERE pack_id = @id";

                        var updateParams = new Dictionary<string, object>
                        {
                            ["@id"] = packId,
                            ["@status"] = status
                        };
                        await _dbConn.ExecSQLAsync(updateSql, updateParams);
                    }
                }

                _logger.LogInformation("批次更新完成");
                return Json(new[] { new { msg = "OK" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批次更新失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }

        // 查詢未領取包裹
        [HttpPost]
        public async Task<JsonResult> GetUndeliveredPackages()
        {
            try
            {
                string sql = @"
                    SELECT b.pack_id, b.pack_type, b.red_id, b.status, b.create_time, b.photo_path,
                           b.remarks,
                           m.pack_name, r.red_name, r.phone, r.condo_id
                    FROM Boxdetail b
                    LEFT JOIN Mail m ON b.pack_type = m.pack_type
                    LEFT JOIN Resident r ON b.red_id = r.red_id
                    WHERE b.status = 0 AND ISNULL(b.deleted, 0) = 0
                    ORDER BY b.create_time DESC";

                DataTable dataTable = await _dbConn.GetDataTableAsync(sql);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得未取包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        // 取得所有包裹資料
        [HttpPost]
        public async Task<JsonResult> GetAllProduct()
        {
            try
            {
                string sql = @"
                    SELECT b.pack_id, b.pack_type, b.red_id, b.status, b.create_time, b.pickup_datetime, b.photo_path,
                           b.remarks,
                           m.pack_name, r.red_name, r.phone, r.condo_id
                    FROM Boxdetail b
                    LEFT JOIN Mail m ON b.pack_type = m.pack_type
                    LEFT JOIN Resident r ON b.red_id = r.red_id
                    WHERE ISNULL(b.deleted, 0) = 0
                    ORDER BY b.create_time DESC";

                DataTable dataTable = await _dbConn.GetDataTableAsync(sql);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得所有包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        [HttpPost]
        public async Task<IActionResult> GetDeletedPackages()
        {
            try
            {
                string sql = @"
                    SELECT b.pack_id, b.pack_type, b.red_id, b.status, b.create_time, b.pickup_datetime, b.photo_path,
                           b.remarks,
                           m.pack_name, r.red_name, r.phone, r.condo_id
                    FROM Boxdetail b
                    LEFT JOIN Mail m ON b.pack_type = m.pack_type
                    LEFT JOIN Resident r ON b.red_id = r.red_id
                    WHERE b.deleted = 1
                    ORDER BY b.create_time DESC";

                DataTable dataTable = await _dbConn.GetDataTableAsync(sql);

                if (dataTable.Rows.Count > 0)
                {
                    string jsonResult = _dbConn.DataTableToJsonString(dataTable);
                    var resultObject = System.Text.Json.JsonSerializer.Deserialize<object>(jsonResult);
                    return Json(resultObject);
                }
                else
                {
                    return Json(new[] { new { msg = "ZERO" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得刪除紀錄失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        [HttpPost]
        public async Task<IActionResult> RestorePackage([FromBody] JsonElement requestData)
        {
            try
            {
                string id = requestData.GetProperty("id").GetString() ?? "";

                _logger.LogInformation("準備復原包裹: {PackageId}", id);

                if (string.IsNullOrEmpty(id))
                {
                    return Json(new[] { new { msg = "FAIL", err = "包裹編號不能為空" } });
                }

                string sql = "UPDATE Boxdetail SET deleted = 0 WHERE pack_id = @id";
                var parameters = new Dictionary<string, object> { ["@id"] = id };

                string result = await _dbConn.ExecSQLAsync(sql, parameters);

                if (result == "OK")
                {
                    return Json(new[] { new { msg = "OK" } });
                }
                else
                {
                    return Json(new[] { new { msg = "FAIL", err = "復原失敗: " + result } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "復原包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }
        [HttpPost]
        public async Task<IActionResult> RemovePackage([FromBody] JsonElement requestData)
        {
            try
            {
                string id = requestData.GetProperty("id").GetString() ?? "";

                // 永久刪除
                string sql = "DELETE FROM Boxdetail WHERE pack_id = @id";
                var parameters = new Dictionary<string, object> { ["@id"] = id };
                string result = await _dbConn.ExecSQLAsync(sql, parameters);

                if (result == "OK")
                {
                    return Json(new[] { new { msg = "OK" } });
                }
                else
                {
                    return Json(new[] { new { msg = "FAIL", err = "清除失敗" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除包裹失敗");
                return Json(new[] { new { msg = "FAIL", err = ex.Message } });
            }
        }

        [HttpPost]
        public IActionResult GetResidentPackageStats()
        {
            try
            {
                string sql = @"
                    SELECT r.red_id, r.condo_id, r.red_name,
                        SUM(CASE WHEN b.status = 0 AND ISNULL(b.deleted,0)=0 THEN 1 ELSE 0 END) AS uncollected,
                        SUM(CASE WHEN b.status = 1 AND ISNULL(b.deleted,0)=0 THEN 1 ELSE 0 END) AS collected
                    FROM Resident r
                    LEFT JOIN Boxdetail b ON r.red_id = b.red_id
                    GROUP BY r.red_id, r.condo_id, r.red_name
                    ORDER BY r.condo_id
                ";
                var dt = _dbConn.GetDataTable(sql);
                return Content(_dbConn.DataTableToJsonString(dt), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving resident package statistics: {ErrorMessage}", ex.Message);
                return Json(new { success = false, message = "Error retrieving resident package statistics", error = ex.Message });
            }
        }
    }
}
