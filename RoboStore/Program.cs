using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Hubs;
using RoboStore.Services;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<RoboStoreDbContext>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<LogBroadcastService>();
builder.Services.AddLogging();
builder.Services.AddSignalR();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = ".RoboStore.Auth";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Простые хеши для паролей
string adminHash = ComputeHash("AdminMori");
string managerHash = ComputeHash("ManagerMori");

static string ComputeHash(string input)
{
    using var sha = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(input);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

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
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'FirstName')
        BEGIN
            ALTER TABLE Users ADD FirstName NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastName')
        BEGIN
            ALTER TABLE Users ADD LastName NVARCHAR(MAX) NULL
        END", conn);
    alterCmd.ExecuteNonQuery();

    var fixLoginCmd = new SqlCommand(@"
        UPDATE Users SET Login = 'user_' + CAST(Id AS VARCHAR(10)) WHERE Login IS NULL OR Login = ''
        UPDATE Users SET PasswordHash = 'NO_AUTH' WHERE PasswordHash IS NULL", conn);
    fixLoginCmd.ExecuteNonQuery();

    var seedCmd = new SqlCommand($@"
        DELETE FROM Orders WHERE UserId IN (SELECT Id FROM Users WHERE Login IN ('admin1', 'manager1', 'admin', 'manager'))
        DELETE FROM Users WHERE Login IN ('admin1', 'manager1', 'admin', 'manager')
        INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
        VALUES ('admin1', '{adminHash}', 'Admin', 1, GETDATE())
        INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
        VALUES ('manager1', '{managerHash}', 'Manager', 1, GETDATE())
        INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
        VALUES ('admin', '{adminHash}', 'Admin', 1, GETDATE())
        INSERT INTO Users (Login, PasswordHash, Role, IsVerified, CreatedAt)
        VALUES ('manager', '{managerHash}', 'Manager', 1, GETDATE())", conn);
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

    // Создаём таблицу Logs если её нет
    var createLogsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Logs')
        BEGIN
            CREATE TABLE Logs (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                ActionDate DATETIME NOT NULL DEFAULT GETDATE(),
                UserLogin NVARCHAR(MAX) NULL,
                ActionType NVARCHAR(MAX) NOT NULL,
                Details NVARCHAR(MAX) NULL
            )
        END", conn);
    createLogsCmd.ExecuteNonQuery();

    // Добавляем колонку TempOrderId в Orders если её нет (для sync idempotency)
    var addTempOrderIdCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'TempOrderId')
        BEGIN
            ALTER TABLE Orders ADD TempOrderId NVARCHAR(MAX) NULL
        END", conn);
    addTempOrderIdCmd.ExecuteNonQuery();

    // Создаём таблицу RobotTypes если её нет
    var createRobotTypesCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RobotTypes')
        BEGIN
            CREATE TABLE RobotTypes (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(MAX) NOT NULL,
                Description NVARCHAR(MAX) NULL
            )
        END", conn);
    createRobotTypesCmd.ExecuteNonQuery();

    // Добавляем базовые типы роботов если таблица пуста
    var seedRobotTypesCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM RobotTypes)
        BEGIN
            INSERT INTO RobotTypes (Name, Description) SELECT 'Промышленный', 'Роботы для промышленного производства и автоматизации' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Промышленный')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Бытовой', 'Роботы для домашнего использования' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Бытовой')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Образовательный', 'Роботы для обучения и развития' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Образовательный')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Медицинский', 'Роботы для медицинских учреждений' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Медицинский')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Дроид-помощник', 'Универсальные дроиды-ассистенты' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Дроид-помощник')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Дроид-переводчик', 'Роботы для перевода и коммуникации' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Дроид-переводчик')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Разведывательный', 'Роботы для разведки и наблюдения' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Разведывательный')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Трансформер', 'Трансформирующиеся роботы' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Трансформер')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Утилизатор', 'Роботы для утилизации и переработки' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Утилизатор')
            INSERT INTO RobotTypes (Name, Description) SELECT 'Полицейский', 'Роботы для правоохранительных органов' WHERE NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Полицейский')
        END", conn);
    seedRobotTypesCmd.ExecuteNonQuery();

    // Добавляем колонки Description и SerialNumber если их нет
    var addRobotColumnsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'Description')
        BEGIN
            ALTER TABLE Robots ADD Description NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'SerialNumber')
        BEGIN
            ALTER TABLE Robots ADD SerialNumber NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'TypeId')
        BEGIN
            ALTER TABLE Robots ADD TypeId INT NULL
        END", conn);
    addRobotColumnsCmd.ExecuteNonQuery();

    // Обновляем существующих роботов - связываем Type (текст) с TypeId через RobotTypes
    var linkRobotTypesCmd = new SqlCommand(@"
        UPDATE Robots SET TypeId = (SELECT TOP 1 Id FROM RobotTypes WHERE Name = Robots.Type)
        WHERE TypeId IS NULL AND Type IS NOT NULL", conn);
    linkRobotTypesCmd.ExecuteNonQuery();

    // Добавляем тестовых роботов если таблица пуста
    var seedRobotsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM Robots)
        BEGIN
            INSERT INTO Robots (Model, Type, TypeId, Price, Stock, Description) VALUES
            ('R2-D2', 'Дроид-помощник', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Дроид-помощник'), 15000, 5, 'Компактный дроид-помощник с развитой функциональностью'),
            ('C-3PO', 'Дроид-переводчик', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Дроид-переводчик'), 12000, 3, 'Гуманоидный дроид для перевода и межвидовой коммуникации'),
            ('BB-8', 'Разведывательный', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Разведывательный'), 18000, 4, 'Сферический разведывательный дроид'),
            ('Optimus Prime', 'Трансформер', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Трансформер'), 50000, 2, 'Лидер автоботов, способный трансформироваться в грузовик'),
            ('Wall-E', 'Утилизатор', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Утилизатор'), 8000, 10, 'Робот-утилизатор для сбора и прессования мусора'),
            ('RoboCop', 'Полицейский', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Полицейский'), 35000, 1, 'Кибернетический полицейский для поддержания правопорядка')
        END", conn);
    seedRobotsCmd.ExecuteNonQuery();
}
catch (Exception ex)
{
    Console.WriteLine($"Note: Could not alter table: {ex.Message}");
}

app.UseSession();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<LogsHub>("/hubs/logs");

app.Run();
