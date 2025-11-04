using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelManagement2.Models;
using ParcelManagement2.Services;
using System.Data;
using System.Text.Json;

namespace ParcelManagement2.Controllers
{
    [Authorize (Roles = "Resident")]
    public class ResidentController : Controller
    {
        private readonly ResidentModel _residentModel;
        private readonly DBConn _dbConn;
        private readonly IAccountService _accountService;
        private readonly ILogger<ResidentController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ResidentController(
            ResidentModel residentModel,
            DBConn dbConn,
            ILogger<ResidentController> logger,
            IAccountService accountService,
            IWebHostEnvironment webHostEnvironment)
        {
            _residentModel = residentModel;
            _dbConn = dbConn;
            _accountService = accountService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Home()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("使用者名稱為空");
                    return RedirectToAction("Login", "Account");
                }
                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                {
                    _logger.LogWarning("找不到使用者資料: {Username}", username);
                    return NotFound("找不到使用者資料");
                }

                DataTable residents = _residentModel.ListByCondo(acct.Condo_Id);
                // 檢查是否有多個成員
                bool hasMultipleMembers = residents.Rows.Count > 1;
                // 取得目前選中的成員
                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");
                DataRow? selectedResident = null;
                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    foreach (DataRow row in residents.Rows)
                    {
                        if (row["red_id"].ToString() == selectedResidentId)
                        {
                            selectedResident = row;
                            break;
                        }
                    }
                }
                // 如果沒有選中成員，選擇第一個
                if (selectedResident == null && residents.Rows.Count > 0)
                {
                    selectedResident = residents.Rows[0];
                    HttpContext.Session.SetString("SelectedResidentId", selectedResident["red_id"].ToString() ?? "");
                }
                int photoCount = 0;
                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    photoCount = GetResidentPhotoCount(selectedResidentId);
                }
                var vm = new ResidentHomeVm
                {
                    UserName = username,
                    Condo_Id = acct.Condo_Id ?? string.Empty,
                    ResidentId = selectedResident?["red_id"]?.ToString() ?? string.Empty,
                    FullName = selectedResident?["red_name"]?.ToString() ?? string.Empty,
                    Phone = selectedResident?["phone"]?.ToString() ?? string.Empty,
                    HasMultipleMembers = hasMultipleMembers,
                    ProfilePhotoCount = photoCount
                };

                ViewBag.JsonList = _dbConn.DataTableToJsonString(residents);
                ViewBag.SelectedResidentId = selectedResidentId;
                ViewBag.ProfilePhotoCount = photoCount;
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入住戶首頁失敗");
                return View("Error");
            }
        }
        public IActionResult MyPackages()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("使用者名稱為空");
                    return RedirectToAction("Login", "Account");
                }

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                {
                    _logger.LogWarning("找不到使用者資料: {Username}", username);
                    return NotFound("找不到使用者資料");
                }

                // 取得住戶清單和選中成員
                DataTable residents = _residentModel.ListByCondo(acct.Condo_Id);
                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");

                if (string.IsNullOrEmpty(selectedResidentId) && residents.Rows.Count > 0)
                {
                    selectedResidentId = residents.Rows[0]["red_id"].ToString();
                    HttpContext.Session.SetString("SelectedResidentId", selectedResidentId ?? "");
                }

                var vm = new MyPackagesVm
                {
                    UserName = username,
                    Condo_Id = acct.Condo_Id ?? string.Empty,
                    SelectedResidentId = selectedResidentId ?? string.Empty,
                    HasMultipleMembers = residents.Rows.Count > 1
                };

                ViewBag.JsonResidents = _dbConn.DataTableToJsonString(residents);
                ViewBag.SelectedResidentId = selectedResidentId;

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入我的包裹頁面失敗");
                return View("Error");
            }
        }
        // 取得所有包裹（包含包裹和信件）
        [HttpPost]
        public IActionResult GetAllPackages()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");

                string sql = @"
                    SELECT bd.pack_id,
                           bd.pack_type,
                           bd.red_id,
                           bd.condo_id,
                           bd.status,
                           bd.create_time,
                           bd.pickup_datetime,
                           bd.collector_name,
                           m.pack_name,
                           r.red_name,
                           CASE WHEN bd.status = 0 THEN N'未領取' ELSE N'已領取' END as status_text,
                           CASE WHEN m.pack_name LIKE '%信件%' THEN N'信件' ELSE N'包裹' END as item_type
                    FROM   Boxdetail bd
                    INNER JOIN Mail m ON m.pack_type = bd.pack_type
                    LEFT JOIN Resident r ON r.red_id = bd.red_id
                    WHERE  bd.deleted = 0
                      AND  bd.condo_id = @condoId";

                var parameters = new Dictionary<string, object>
                {
                    { "@condoId", acct.Condo_Id ?? "" }
                };

                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    sql += @"
                        AND (
                          bd.red_id = @residentId 
                          OR bd.red_id IS NULL
                          OR m.pack_name LIKE '%信件%'
                      )";
                    parameters.Add("@residentId", selectedResidentId);
                }

                sql += " ORDER BY bd.create_time DESC";

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                return Content(_dbConn.DataTableToJsonString(result), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得所有包裹清單失敗");
                return Json(new { success = false, message = "取得資料時發生錯誤" });
            }
        }

        //  取得領取紀錄（只顯示已領取的）
        [HttpPost]
        public IActionResult GetCollectedHistory()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");

                string sql = @"
                    SELECT bd.pack_id,
                           bd.pack_type,
                           bd.condo_id,
                           bd.red_id,
                           bd.status,
                           bd.create_time,
                           bd.pickup_datetime,
                           bd.collector_name,
                           r.red_name,
                           m.pack_name,
                           N'已領取' AS status_text,
                           CASE WHEN m.pack_name LIKE '%信件%' THEN N'信件' 
                                ELSE N'包裹' 
                           END as item_type
                    FROM   Boxdetail bd
                    INNER JOIN Mail m ON m.pack_type = bd.pack_type
                    LEFT JOIN Resident r ON r.red_id = bd.red_id
                    WHERE  bd.deleted = 0
                      AND  bd.status = 1
                      AND  bd.condo_id = @condoId";

                var parameters = new Dictionary<string, object>
                {
                    { "@condoId", acct.Condo_Id ?? "" }
                };
                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    sql += @"
                      AND (
                          bd.red_id = @residentId 
                          OR bd.red_id IS NULL
                          OR m.pack_name LIKE '%信件%'
                      )";
                    parameters.Add("@residentId", selectedResidentId);
                }
                sql += " ORDER BY bd.pickup_datetime DESC";

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                return Content(_dbConn.DataTableToJsonString(result), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得領取紀錄失敗");
                return Json(new { success = false, message = "取得資料時發生錯誤" });
            }
        }
        // 選擇成員
        [HttpPost]
        public IActionResult SelectMember(string residentId)
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                // 驗證成員是否屬於此戶號
                const string sql = "SELECT red_id, red_name FROM Resident WHERE red_id = @residentId AND condo_id = @condoId";
                var parameters = new Dictionary<string, object>
                {
                    { "@residentId", residentId },
                    { "@condoId", acct.Condo_Id ?? "" }
                };

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                if (result.Rows.Count == 0)
                    return Json(new { success = false, message = "無效的成員選擇" });

                // 儲存到Session
                HttpContext.Session.SetString("SelectedResidentId", residentId);

                return Json(new
                {
                    success = true,
                    message = "成員已切換",
                    memberName = result.Rows[0]["red_name"].ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "選擇成員失敗");
                return Json(new { success = false, message = "選擇成員時發生錯誤" });
            }
        }
        /* ======= 未領取包裹清單 ======= */
        [HttpPost]
        public IActionResult GetUncollectedPackages()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                // 取得選中的成員ID
                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");

                string sql;
                var parameters = new Dictionary<string, object>
                {
                    { "@condoId", acct.Condo_Id ?? "" }
                };

                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    // 只顯示選中成員的包裹，並排除信件類型
                    sql = @"
                        SELECT bd.pack_id,
                               N'待領取' AS packageStatus,
                               r.red_name,
                               m.pack_name
                        FROM   Boxdetail bd
                        INNER JOIN Resident r ON r.red_id = bd.red_id
                        INNER JOIN Mail m ON m.pack_type = bd.pack_type
                        WHERE  bd.deleted = 0
                          AND  bd.status = 0
                          AND  r.condo_id = @condoId
                          AND  r.red_id = @residentId
                          AND  m.pack_name LIKE '%包裹%'
                        ORDER BY bd.create_time DESC";
                    parameters.Add("@residentId", selectedResidentId);
                }
                else
                {
                    // 顯示整戶的包裹
                    sql = @"
                        SELECT bd.pack_id,
                               N'待領取' AS packageStatus,
                               r.red_name,
                               m.pack_name
                        FROM   Boxdetail bd
                        INNER JOIN Resident r ON r.red_id = bd.red_id
                        INNER JOIN Mail m ON m.pack_type = bd.pack_type
                        WHERE  bd.deleted = 0
                          AND  bd.status = 0
                          AND  r.condo_id = @condoId
                          AND  m.pack_name LIKE '%包裹%'
                        ORDER BY bd.create_time DESC";
                }
                DataTable result = _dbConn.GetDataTable(sql, parameters);
                return Content(_dbConn.DataTableToJsonString(result), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得未領取包裹清單失敗");
                return Json(new { success = false, message = "取得資料時發生錯誤" });
            }
        }

        // 未領取信件清單
        [HttpPost]
        public IActionResult GetUncollectedMails()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                    // 顯示整戶的信件
                string sql = @"
                        SELECT bd.pack_id,
                               bd.condo_id,
                               bd.red_id,
                               N'待領取' AS mailStatus,
                               r.red_name,
                               m.pack_name,
                               bd.create_time
                        FROM   Boxdetail bd
                        INNER JOIN Mail m ON m.pack_type = bd.pack_type
                        LEFT JOIN Resident r ON r.red_id = bd.red_id
                        WHERE  bd.deleted = 0
                          AND  bd.status = 0
                          AND  bd.condo_id = @condoId
                          AND  (m.pack_name LIKE '%信件%')
                        ORDER BY bd.create_time DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@condoId", acct.Condo_Id ?? "" }
                };

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                return Content(_dbConn.DataTableToJsonString(result), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得未領取信件清單失敗");
                return Json(new { success = false, message = "取得資料時發生錯誤" });
            }
        }
        [HttpPost]
        public IActionResult GetUncollectedCount()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取", count = 0 });
                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料", count = 0 });
                if (string.IsNullOrEmpty(acct.Condo_Id))
                    return Json(new { success = false, message = "使用者資料不完整", count = 0 });
                const string sql = @"
                    SELECT COUNT(*) as cnt
                    FROM Boxdetail bd
                    WHERE bd.deleted = 0 
                      AND bd.status = 0
                      AND bd.condo_id = @condoId";

                var parameters = new Dictionary<string, object>
                {
                    { "@condoId", acct.Condo_Id }
                };
                DataTable result = _dbConn.GetDataTable(sql, parameters);
                int count = 0;
                if (result.Rows.Count > 0)
                {
                    object? countObj = result.Rows[0]["cnt"];
                    if (countObj != null && countObj != DBNull.Value)
                    {
                        count = Convert.ToInt32(countObj);
                    }
                }
                return Json(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得未領取包裹數失敗");
                return Json(new { success = false, message = "取得資料時發生錯誤", count = 0 });
            }
        }
        // 取得包裹照片 API
        [HttpPost]
        public async Task<IActionResult> GetPackagePhotos([FromBody] JsonElement requestData)
        {
            try
            {
                string packageId = requestData.GetProperty("packageId").GetString() ?? "";

                if (string.IsNullOrEmpty(packageId))
                {
                    return Json(new { success = false, message = "包裹編號不能為空" });
                }

                // 查詢包裹資訊，包含檔案路徑和二進制資料
                string sql = @"
                    SELECT b.pack_id, b.photo_path, b.image_data, b.create_time,
                           m.pack_name, r.red_name, r.red_id
                    FROM Boxdetail b
                    LEFT JOIN Mail m ON b.pack_type = m.pack_type
                    LEFT JOIN Resident r ON b.red_id = r.red_id
                    WHERE b.pack_id = @packageId AND ISNULL(b.deleted, 0) = 0";

                var parameters = new Dictionary<string, object> { ["@packageId"] = packageId };
                DataTable dataTable = await _dbConn.GetDataTableAsync(sql, parameters);

                if (dataTable.Rows.Count == 0)
                {
                    return Json(new { success = false, message = "找不到指定的包裹" });
                }

                var row = dataTable.Rows[0];
                var photos = new List<string>();

                // 處理檔案路徑照片
                string photoPath = row["photo_path"]?.ToString();
                if (!string.IsNullOrEmpty(photoPath))
                {
                    photos.Add(photoPath);
                }

                // 處理二進制照片資料
                if (row["image_data"] != DBNull.Value && row["image_data"] != null)
                {
                    byte[] imageData = (byte[])row["image_data"];
                    if (imageData.Length > 0)
                    {
                        // 轉換為 Base64 Data URL
                        string base64String = Convert.ToBase64String(imageData);
                        string mimeType = GetImageMimeType(imageData);
                        string dataUrl = $"data:{mimeType};base64,{base64String}";
                        photos.Add(dataUrl);
                    }
                }

                return Json(new
                {
                    success = true,
                    packageId = packageId,
                    packageName = row["pack_name"]?.ToString() ?? "",
                    recipientId = row["red_id"]?.ToString() ?? "",
                    recipientName = row["red_name"]?.ToString() ?? "",
                    createTime = row["create_time"]?.ToString() ?? "",
                    photos = photos,
                    photoCount = photos.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取包裹照片失敗");
                return Json(new { success = false, message = "獲取包裹照片失敗", error = ex.Message });
            }
        }
        // 取得住戶已上傳照片數（0~5）
        private int GetResidentPhotoCount(string residentId)
        {
            try
            {
                const string sql = @"
            SELECT 
                (CASE WHEN photo_1 IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN photo_2 IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN photo_3 IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN photo_4 IS NOT NULL THEN 1 ELSE 0 END) +
                (CASE WHEN photo_5 IS NOT NULL THEN 1 ELSE 0 END) AS cnt
            FROM Photo
            WHERE red_id = @residentId";

                var parameters = new Dictionary<string, object>
        {
            { "@residentId", residentId }
        };

                DataTable dt = _dbConn.GetDataTable(sql, parameters);
                if (dt.Rows.Count == 0 || dt.Rows[0]["cnt"] == DBNull.Value) return 0;

                return Convert.ToInt32(dt.Rows[0]["cnt"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetResidentPhotoCount 失敗, residentId={ResidentId}", residentId);
                return 0;
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

            return "image/jpeg"; 
        }
        /* ======= 編輯個人資料 ======= */
        public IActionResult Edit()
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return RedirectToAction("Login", "Account");

                var acct = _accountService.GetByUsername(username);
                if (acct == null) return NotFound("找不到使用者資料");

                // 取得選中的成員ID
                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");

                DataRow? row = null; 
                if (!string.IsNullOrEmpty(selectedResidentId))
                {
                    row = _residentModel.GetRowById(selectedResidentId);
                    // 驗證成員是否屬於此戶號
                    if (row == null || !string.Equals(row["condo_id"]?.ToString(), acct.Condo_Id, StringComparison.OrdinalIgnoreCase))
                        return Forbid("無權編輯此成員資料");
                }
                else
                {
                    row = _residentModel.GetRowByCondoFirst(acct.Condo_Id);
                    // 如果找到成員，設定到 Session
                    if (row != null)
                    {
                        HttpContext.Session.SetString("SelectedResidentId", row["red_id"]?.ToString() ?? "");
                    }
                }

                if (row == null) return NotFound("尚未建立住戶資料");

                var vm = new ResidentEditVm
                {
                    ResidentId = row["red_id"]?.ToString() ?? "",
                    FullName = row["red_name"]?.ToString() ?? "",
                    Phone = row["phone"]?.ToString() ?? "",
                    ExistingPhotos = GetResidentPhotos(row["red_id"]?.ToString() ?? "") 
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "載入編輯個人資料頁面失敗");
                return View("Error");
            }
        }
        // 儲存基本資料
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveBasicInfo([FromBody] SaveBasicInfoRequest request)
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                // 驗證資料
                if (string.IsNullOrWhiteSpace(request.FullName))
                    return Json(new { success = false, message = "姓名不能為空" });

                // 驗證是否為本人或同戶成員
                if (!IsValidResidentAccess(request.ResidentId, acct.Condo_Id))
                    return Json(new { success = false, message = "無權修改此成員資料" });

                // 使用現有的 ResidentModel.Update 方法
                string result = _residentModel.Update(request.ResidentId, request.FullName.Trim(), request.Phone?.Trim() ?? "");

                if (result == "OK")
                {
                    return Json(new { success = true, message = "基本資料更新成功" });
                }
                else
                {
                    return Json(new { success = false, message = "更新失敗：" + result });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存基本資料失敗");
                return Json(new { success = false, message = "儲存時發生錯誤" });
            }
        }

        // 儲存密碼
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePassword([FromBody] SavePasswordRequest request)
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                // 驗證目前密碼
                if (!_accountService.VerifyPassword(username, request.CurrentPassword))
                    return Json(new { success = false, message = "目前密碼不正確" });

                // 驗證新密碼
                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                    return Json(new { success = false, message = "新密碼至少需要6個字元" });

                // 更新密碼
                string result = _accountService.UpdatePassword(acct.Id, request.NewPassword);
                if (result == "OK")
                {
                    return Json(new { success = true, message = "密碼變更成功" });
                }
                else
                {
                    return Json(new { success = false, message = "密碼變更失敗" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "變更密碼失敗");
                return Json(new { success = false, message = "變更密碼時發生錯誤" });
            }
        }
        // 儲存照片
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePhotos(List<IFormFile> photos)
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");
                if (string.IsNullOrEmpty(selectedResidentId))
                    return Json(new { success = false, message = "請先選擇成員" });

                // 驗證是否為本人或同戶成員
                if (!IsValidResidentAccess(selectedResidentId, acct.Condo_Id))
                    return Json(new { success = false, message = "無權修改此成員照片" });

                if (photos == null || !photos.Any())
                    return Json(new { success = true, message = "沒有照片需要上傳" });

                // 嚴格驗證檔案
                foreach (var photo in photos)
                {
                    if (photo.Length > 5 * 1024 * 1024) // 5MB
                        return Json(new { success = false, message = $"檔案 {photo.FileName} 大小超過 5MB" });

                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(photo.ContentType.ToLower()))
                        return Json(new { success = false, message = $"檔案 {photo.FileName} 格式不支援" });
                }

                // 檢查總照片數量限制
                var existingPhotos = GetResidentPhotos(selectedResidentId);
                if (existingPhotos.Count + photos.Count > 5)
                    return Json(new { success = false, message = $"最多只能上傳5張照片，目前已有 {existingPhotos.Count} 張" });

                // 儲存照片
                await SaveResidentPhotos(selectedResidentId, photos);

                // 回傳更新後的照片清單
                var updatedPhotos = GetResidentPhotos(selectedResidentId);

                return Json(new
                {
                    success = true,
                    message = "照片上傳成功",
                    photos = updatedPhotos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上傳照片失敗");
                return Json(new { success = false, message = "上傳照片時發生錯誤" });
            }
        }
        // 刪除照片
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePhoto([FromBody] DeletePhotoRequest request)
        {
            try
            {
                string? username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                    return Json(new { success = false, message = "未授權的存取" });

                var acct = _accountService.GetByUsername(username);
                if (acct == null)
                    return Json(new { success = false, message = "找不到使用者資料" });

                string? selectedResidentId = HttpContext.Session.GetString("SelectedResidentId");
                if (string.IsNullOrEmpty(selectedResidentId))
                    return Json(new { success = false, message = "請先選擇成員" });

                // 驗證是否為本人或同戶成員
                if (!IsValidResidentAccess(selectedResidentId, acct.Condo_Id))
                    return Json(new { success = false, message = "無權修改此成員照片" });

                // 修正：photoIndex 從 0 開始，但 Photo 表的欄位從 1 開始
                int photoColumn = request.PhotoIndex + 1;
                if (photoColumn < 1 || photoColumn > 5)
                    return Json(new { success = false, message = "無效的照片編號" });

                string sql = $"UPDATE Photo SET photo_{photoColumn} = NULL WHERE red_id = @residentId";
                var parameters = new Dictionary<string, object> { { "@residentId", selectedResidentId } };

                string result = _dbConn.ExecSQL(sql, parameters);

                return Json(new { success = result == "OK", message = result == "OK" ? "照片已刪除" : "刪除失敗" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除照片失敗");
                return Json(new { success = false, message = "刪除照片時發生錯誤" });
            }
        }
        // 取得住戶照片
        private List<string> GetResidentPhotos(string residentId)
        {
            try
            {
                const string sql = "SELECT photo_1, photo_2, photo_3, photo_4, photo_5 FROM Photo WHERE red_id = @residentId";
                var parameters = new Dictionary<string, object> { { "@residentId", residentId } };

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                var photos = new List<string>();

                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    for (int i = 1; i <= 5; i++)
                    {
                        object? photoData = row[$"photo_{i}"];
                        if (photoData != null && photoData != DBNull.Value && photoData is byte[] bytes && bytes.Length > 0)
                        {
                            string base64 = Convert.ToBase64String(bytes);
                            photos.Add($"data:image/jpeg;base64,{base64}");
                        }
                    }
                }
                return photos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得住戶照片失敗");
                return new List<string>();
            }
        }
        // 儲存住戶照片
        private async Task SaveResidentPhotos(string residentId, List<IFormFile> photos)
        {
            try
            {
                // 檢查是否已存在記錄
                const string checkSql = "SELECT COUNT(*) as cnt FROM Photo WHERE red_id = @residentId";
                var checkParams = new Dictionary<string, object> { { "@residentId", residentId } };
                DataTable checkResult = _dbConn.GetDataTable(checkSql, checkParams);

                bool recordExists = false;
                if (checkResult.Rows.Count > 0)
                {
                    object? countObj = checkResult.Rows[0]["cnt"];
                    if (countObj != null && countObj != DBNull.Value)
                    {
                        recordExists = Convert.ToInt32(countObj) > 0;
                    }
                }

                // 如果記錄不存在，先創建空記錄
                if (!recordExists)
                {
                    const string insertSql = @"
                INSERT INTO Photo (red_id, photo_1, photo_2, photo_3, photo_4, photo_5) 
                VALUES (@residentId, NULL, NULL, NULL, NULL, NULL)";
                    var insertParams = new Dictionary<string, object> { { "@residentId", residentId } };
                    _dbConn.ExecSQL(insertSql, insertParams);
                }

                // 找出空的照片欄位來插入新照片
                const string selectSql = "SELECT photo_1, photo_2, photo_3, photo_4, photo_5 FROM Photo WHERE red_id = @residentId";
                var selectParams = new Dictionary<string, object> { { "@residentId", residentId } };
                DataTable existingResult = _dbConn.GetDataTable(selectSql, selectParams);

                if (existingResult.Rows.Count > 0)
                {
                    var row = existingResult.Rows[0];
                    var emptySlots = new List<int>();

                    // 找出空的欄位
                    for (int i = 1; i <= 5; i++)
                    {
                        object? photoData = row[$"photo_{i}"];
                        if (photoData == null || photoData == DBNull.Value)
                        {
                            emptySlots.Add(i);
                        }
                    }

                    // 將新照片填入空欄位
                    for (int i = 0; i < Math.Min(photos.Count, emptySlots.Count); i++)
                    {
                        var photo = photos[i];
                        using var memoryStream = new MemoryStream();
                        await photo.CopyToAsync(memoryStream);
                        byte[] photoBytes = memoryStream.ToArray();

                        int slot = emptySlots[i];
                        string updateSql = $"UPDATE Photo SET photo_{slot} = @photoData WHERE red_id = @residentId";
                        var updateParams = new Dictionary<string, object>
                        {
                            { "@residentId", residentId },
                            { "@photoData", photoBytes }
                        };
                        _dbConn.ExecSQL(updateSql, updateParams);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存住戶照片失敗");
                throw;
            }
        }
        // 驗證住戶存取權限
        private bool IsValidResidentAccess(string residentId, string? condoId)
        {
            try
            {
                if (string.IsNullOrEmpty(residentId) || string.IsNullOrEmpty(condoId))
                    return false;

                const string sql = "SELECT COUNT(*) as cnt FROM Resident WHERE red_id = @residentId AND condo_id = @condoId";
                var parameters = new Dictionary<string, object>
                {
                    { "@residentId", residentId },
                    { "@condoId", condoId }
                };

                DataTable result = _dbConn.GetDataTable(sql, parameters);
                if (result.Rows.Count > 0)
                {
                    object? countObj = result.Rows[0]["cnt"];
                    if (countObj != null && countObj != DBNull.Value)
                    {
                        return Convert.ToInt32(countObj) > 0;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "驗證住戶存取權限失敗");
                return false;
            }
        }
        // 請求模型
        public class MarkMailsRequest
        {
            // 戶號（condo_id）
            public string? CondoId { get; set; }

            // 領取人名稱（住戶名或"系統自動標記"）
            public string? CollectorName { get; set; }
            //領取人 ID（住戶編號）
            public string? CollectorId { get; set; }
            //領取時間
            public DateTime? PickupTime { get; set; }
        }
        public class GetPackagePhotosRequest
        {
            public string? PackageId { get; set; }
        }
        public class SaveBasicInfoRequest
        {
            public string ResidentId { get; set; } = "";
            public string FullName { get; set; } = "";
            public string? Phone { get; set; }
        }

        public class SavePasswordRequest
        {
            public string CurrentPassword { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }

        public class DeletePhotoRequest
        {
            public int PhotoIndex { get; set; }
        }
    }
}
