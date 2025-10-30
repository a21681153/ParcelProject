using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParcelManagement2.Models;
using ParcelManagement2.Services;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;

namespace ParcelManagement2.Controllers
{
    public class AccountController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly ILogger<AccountController> _logger;
        private readonly IAccountService _accountService;

        public AccountController(
            IJwtService jwtService,
            ILogger<AccountController> logger,
            IAccountService accountService)
        {
            _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountService = accountService;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> Login([FromBody] Models.Request.AccountRequest.LoginDto dto)
        {
            _logger.LogInformation("登入嘗試開始");
            _logger.LogInformation("ModelState valid={IsValid}", ModelState.IsValid);

            foreach (var modelState in ModelState)
            {
                foreach (var error in modelState.Value.Errors)
                {
                    _logger.LogInformation("{Key}:{Error}", modelState.Key, error.ErrorMessage);
                }
            }

            _logger.LogInformation("接收到的資料 - Username: {Username}, Password: {HasPassword}",
                dto?.Username ?? "NULL", !string.IsNullOrEmpty(dto?.Password));

            // 1. ModelState 驗證
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                _logger.LogWarning("ModelState 驗證失敗: {@Errors}", errors);
                return Json(new { ok = false, msg = "資料格式錯誤", debug = errors });
            }

            // 2. 基本資料驗證
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                _logger.LogWarning("帳號或密碼為空");
                return Json(new { ok = false, msg = "帳號密碼不可為空" });
            }

            try
            {
                // 3. 密碼驗證
                if (!_accountService.VerifyPassword(dto.Username, dto.Password))
                {
                    _logger.LogWarning("密碼驗證失敗: {Username}", dto.Username);
                    return Json(new { ok = false, msg = "帳號或密碼錯誤" });
                }

                // 4. 取得帳號資料
                var acct = _accountService.GetByUsername(dto.Username);
                if (acct == null)
                {
                    _logger.LogWarning("找不到使用者資料: {Username}", dto.Username);
                    return Json(new { ok = false, msg = "找不到使用者資料" });
                }

                // 5. 檢查帳號狀態
                if (acct.Status != "Approved")
                {
                    _logger.LogWarning("帳號未啟用: {Username}, Status: {Status}", dto.Username, acct.Status);
                    return Json(new { ok = false, msg = "帳號尚未啟用" });
                }

                _logger.LogInformation("登入成功 - Username: {Username}, Role: {Role}", acct.Username, acct.Role);

                // 6. 建立 Claims
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, acct.Username),
                    new(ClaimTypes.Role, acct.Role),
                    new("cid", acct.Condo_Id ?? ""),
                    new("uid", acct.Id.ToString()),
                };

                // 7. 建立 Cookie Authentication
                var identity = new ClaimsIdentity(claims, "Cookies");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                });

                // 8. 生成 JWT 
                var jwt = GenerateTokenForAccount(acct);

                // 9. 決定導向 URL
                var targetUrl = acct.Role == "Admin"
                    ? Url.Action("Index", "Home")
                    : Url.Action("Home", "Resident");

                _logger.LogInformation("登入完成，導向: {Url}", targetUrl);

                return Json(new
                {
                    ok = true,
                    token = jwt,
                    url = targetUrl,
                    user = new
                    {
                        Username = acct.Username,
                        Role = acct.Role
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登入處理發生錯誤: {Username}", dto.Username);
                return Json(new { ok = false, msg = "系統錯誤，請稍後再試" });
            }
        }

        // GET: /Account/Logout
        [Authorize]
        public async Task<IActionResult> LogoutGet()
        {
            try
            {
                await HttpContext.SignOutAsync("Cookies");
                _logger.LogInformation("使用者登出: {Username}", User.Identity?.Name);
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登出處理發生錯誤");
                return RedirectToAction("Login");
            }
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync("Cookies");
                _logger.LogInformation("使用者登出: {Username}", User.Identity?.Name);
                return Json(new { ok = true, message = "登出成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登出處理發生錯誤");
                return Json(new { ok = false, message = "登出失敗" });
            }
        }

        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            ViewBag.Title = "存取被拒絕";
            ViewBag.Message = "您沒有足夠的權限存取此頁面";
            return View();
        }

        // 輔助方法：生成 JWT Token
        private string GenerateTokenForAccount(Account account)
        {
            try
            {
                return _jwtService.GenerateToken(account, 2); // 2小時有效期
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 JWT Token 失敗");
                return "token-generation-failed";
            }
        }

        // 輔助方法：計算 SHA256 雜湊 (如果需要)
        private static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        // API：檢查登入狀態
        [HttpGet]
        public IActionResult Status()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Json(new
                {
                    authenticated = true,
                    username = User.Identity.Name,
                    role = User.FindFirst(ClaimTypes.Role)?.Value,
                    userId = User.FindFirst("uid")?.Value,
                    condoId = User.FindFirst("cid")?.Value
                });
            }

            return Json(new { authenticated = false });
        }
    }
}
