using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
<<<<<<< HEAD
using RoboStore.Services;
=======
using RoboStore.Models;
using RoboStore.Services;
using System.Security.Cryptography;
using System.Text;
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<RoboStoreDbContext>();
<<<<<<< HEAD
builder.Services.AddScoped<TelegramAuthService>();
=======
builder.Services.AddScoped<TelegramService>();
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
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

var app = builder.Build();

// Добавляем колонки в базу если их нет
try
{
    using var conn = new SqlConnection(@"Server=RoboStore.mssql.somee.com;Database=RoboStore;User Id=MomentoMori_SQLLogin_1;Password=8rhd2k6i2g;TrustServerCertificate=True");
    conn.Open();

    // Добавляем Telegram-колонки если их нет
    var alterCmd = new SqlCommand(@"
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

    // Обновляем существующих пользователей с пустым Login
    var fixLoginCmd = new SqlCommand(@"
        UPDATE Users SET Login = 'user_' + CAST(Id AS VARCHAR(10)) WHERE Login IS NULL OR Login = ''", conn);
    fixLoginCmd.ExecuteNonQuery();

    // Добавляем админа и менеджера если их нет
    var seedCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM Users WHERE TelegramUsername = 'admin')
        BEGIN
            INSERT INTO Users (TelegramId, TelegramUsername, FirstName, LastName, Role, IsVerified, CreatedAt, Login)
            VALUES (111111111, 'admin', 'Admin', 'User', 'Admin', 1, GETDATE(), 'admin')
        END
        IF NOT EXISTS (SELECT * FROM Users WHERE TelegramUsername = 'manager')
        BEGIN
            INSERT INTO Users (TelegramId, TelegramUsername, FirstName, LastName, Role, IsVerified, CreatedAt, Login)
            VALUES (222222222, 'manager', 'Manager', 'User', 'Manager', 1, GETDATE(), 'manager')
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
