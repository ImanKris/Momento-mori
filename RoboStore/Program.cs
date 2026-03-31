using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using RoboStore.Services;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<RoboStoreDbContext>();
builder.Services.AddScoped<TelegramAuthService>();
builder.Services.AddScoped<TelegramService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Вычисляем хеши заранее
string adminHash = ComputeHash("AdminMori");
string managerHash = ComputeHash("ManagerMori");

var app = builder.Build();

// Добавляем колонки в базу если их нет
try
{
    using var conn = new SqlConnection(@"Server=RoboStore.mssql.somee.com;Database=RoboStore;User Id=MomentoMori_SQLLogin_1;Password=8rhd2k6i2g;TrustServerCertificate=True");
    conn.Open();

    var alterCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Login')
        BEGIN
            ALTER TABLE Users ADD Login NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TelegramId')
        BEGIN
            ALTER TABLE Users ADD TelegramId BIGINT NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TelegramUsername')
        BEGIN
            ALTER TABLE Users ADD TelegramUsername NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'FirstName')
        BEGIN
            ALTER TABLE Users ADD FirstName NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastName')
        BEGIN
            ALTER TABLE Users ADD LastName NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'PhotoUrl')
        BEGIN
            ALTER TABLE Users ADD PhotoUrl NVARCHAR(MAX) NULL
        END", conn);
    alterCmd.ExecuteNonQuery();

    var fixLoginCmd = new SqlCommand(@"
        UPDATE Users SET Login = 'user_' + CAST(Id AS VARCHAR(10)) WHERE Login IS NULL OR Login = ''
        UPDATE Users SET PasswordHash = 'TELEGRAM_AUTH' WHERE PasswordHash IS NULL", conn);
    fixLoginCmd.ExecuteNonQuery();

    var seedCmd = new SqlCommand($@"
        IF NOT EXISTS (SELECT * FROM Users WHERE Login = 'admin1')
        BEGIN
            INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
            VALUES ('admin1', '{adminHash}', 'Admin', 1, GETDATE())
        END
        IF NOT EXISTS (SELECT * FROM Users WHERE Login = 'manager1')
        BEGIN
            INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
            VALUES ('manager1', '{managerHash}', 'Manager', 1, GETDATE())
        END
        IF NOT EXISTS (SELECT * FROM Users WHERE Login = 'admin')
        BEGIN
            INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
            VALUES ('admin', '{adminHash}', 'Admin', 1, GETDATE())
        END
        IF NOT EXISTS (SELECT * FROM Users WHERE Login = 'manager')
        BEGIN
            INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
            VALUES ('manager', '{managerHash}', 'Manager', 1, GETDATE())
        END", conn);
    seedCmd.ExecuteNonQuery();
}
catch (Exception ex)
{
    Console.WriteLine($"Note: Could not alter table: {ex.Message}");
}

app.UseSession();
app.UseAuthentication();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string ComputeHash(string input)
{
    using var sha = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(input);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}
