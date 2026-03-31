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

    // Создаём таблицу Robots если её нет
    var createRobotsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Robots')
        BEGIN
            CREATE TABLE Robots (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Model NVARCHAR(MAX) NOT NULL,
                Type NVARCHAR(MAX) NOT NULL,
                Price DECIMAL(18,2) NOT NULL DEFAULT 0,
                Stock INT NOT NULL DEFAULT 0
            )
        END", conn);
    createRobotsCmd.ExecuteNonQuery();

    // Создаём таблицу Orders если её нет
    var createOrdersCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Orders')
        BEGIN
            CREATE TABLE Orders (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                UserId INT NOT NULL,
                RobotId INT NOT NULL,
                OrderDate DATETIME NOT NULL DEFAULT GETDATE(),
                Status NVARCHAR(MAX) NOT NULL DEFAULT 'В обработке',
                FOREIGN KEY (UserId) REFERENCES Users(Id),
                FOREIGN KEY (RobotId) REFERENCES Robots(Id)
            )
        END", conn);
    createOrdersCmd.ExecuteNonQuery();

    // Добавляем тестовых роботов если таблица пуста
    var seedRobotsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM Robots)
        BEGIN
            INSERT INTO Robots (Model, Type, Price, Stock) VALUES
            ('R2-D2', 'Дроид-помощник', 15000, 5),
            ('C-3PO', 'Дроид-переводчик', 12000, 3),
            ('BB-8', 'Разведывательный', 18000, 4),
            ('Optimus Prime', 'Трансформер', 50000, 2),
            ('Wall-E', 'Утилизатор', 8000, 10),
            ('RoboCop', 'Полицейский', 35000, 1)
        END", conn);
    seedRobotsCmd.ExecuteNonQuery();
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
