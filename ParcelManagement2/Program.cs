using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using ParcelManagement2.Models;
using ParcelManagement2.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();
var isProduction = builder.Environment.IsProduction();

Console.WriteLine($"=== 環境: {builder.Environment.EnvironmentName} ===");
Console.WriteLine($"=== 開發環境: {isDevelopment} ===");
Console.WriteLine($"=== 正式環境: {isProduction} ===");

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// 註冊 ApplicationSettings 配置
builder.Services.Configure<ApplicationSettingsModel>(
    builder.Configuration.GetSection("ApplicationSettings"));

// 設定文件上傳大小限制
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10485760; // 10MB (稍大於設定以允許其他表單數據)
});

// 設定 Kestrel 的文件上傳限制
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10485760; // 10MB
});

// 配置設定服務
builder.Services.Configure<WebPagesSettings>(
    builder.Configuration.GetSection("WebPages"));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
// 註冊 JWT 服務
builder.Services.AddScoped<IJwtService, JwtService>();
// 註冊 Account 服務
builder.Services.AddScoped<IAccountService, AccountService>();
// 註冊 DBConn 服務
builder.Services.AddScoped<DBConn>();
// 註冊 ResidentModel 服務
builder.Services.AddScoped<ResidentModel>();
builder.Services.AddHttpClient();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax; // 開發環境使用 Lax
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    // 開發環境允許 HTTP，正式環境使用 HTTPS
    options.Secure = isDevelopment
        ? CookieSecurePolicy.None
        : CookieSecurePolicy.SameAsRequest;
});
// 配置 Cookie Authentication
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/LogoutGet";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Cookie.SecurePolicy = isDevelopment
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.SameAsRequest;
        options.Cookie.HttpOnly = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ResidentOnly", policy => policy.RequireRole("Resident"));
    options.AddPolicy("AdminOrResident", policy => policy.RequireRole("Admin", "Resident"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
       app.UseDeveloperExceptionPage();
}
app.UseSession();
app.UseCookiePolicy();

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
