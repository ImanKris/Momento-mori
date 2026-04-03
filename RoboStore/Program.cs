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

app.UseStaticFiles();

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

    // Удаляем CHECK constraint с колонки Type (был создан для старых типов, мешает новым)
    try
    {
        var dropConstraintCmd = new SqlCommand(@"
            IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK__Robots__Type__3D5E1FD2')
            BEGIN
                ALTER TABLE Robots DROP CONSTRAINT CK__Robots__Type__3D5E1FD2
            END", conn);
        dropConstraintCmd.ExecuteNonQuery();
    }
    catch { /* может уже не существовать */ }

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

    // Безопасное обновление типов роботов — сохраняем Id, меняем Name/Description
    // Добавляем новые типы если их нет
    var updateRobotTypesCmd = new SqlCommand(@"
        -- Industrial
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Промышленный','Industrial'))
        BEGIN UPDATE RobotTypes SET Name = 'Industrial', Description = 'Роботы для промышленного производства, сварки, сборки и автоматизации' WHERE Name IN ('Промышленный','Industrial') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Industrial', 'Роботы для промышленного производства, сварки, сборки и автоматизации') END

        -- Home Assistant
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Бытовой','Home Assistant'))
        BEGIN UPDATE RobotTypes SET Name = 'Home Assistant', Description = 'Роботы-помощники для домашних задач и быта' WHERE Name IN ('Бытовой','Home Assistant') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Home Assistant', 'Роботы-помощники для домашних задач и быта') END

        -- Educational
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Образовательный','Educational'))
        BEGIN UPDATE RobotTypes SET Name = 'Educational', Description = 'Роботы для обучения, программирования и развития навыков' WHERE Name IN ('Образовательный','Educational') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Educational', 'Роботы для обучения, программирования и развития навыков') END

        -- Medical
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Медицинский','Medical'))
        BEGIN UPDATE RobotTypes SET Name = 'Medical', Description = 'Роботы для медицинских учреждений, хирургии и ухода' WHERE Name IN ('Медицинский','Medical') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Medical', 'Роботы для медицинских учреждений, хирургии и ухода') END

        -- Companion
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Дроид-помощник','Companion'))
        BEGIN UPDATE RobotTypes SET Name = 'Companion', Description = 'Роботы-компаньоны для общения и помощи людям' WHERE Name IN ('Дроид-помощник','Companion') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Companion', 'Роботы-компаньоны для общения и помощи людям') END

        -- Security
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Полицейский','Security'))
        BEGIN UPDATE RobotTypes SET Name = 'Security', Description = 'Роботы для охраны, патрулирования и безопасности' WHERE Name IN ('Полицейский','Security') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Security', 'Роботы для охраны, патрулирования и безопасности') END

        -- Logistics
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Разведывательный','Logistics'))
        BEGIN UPDATE RobotTypes SET Name = 'Logistics', Description = 'Роботы для склада, доставки и логистики' WHERE Name IN ('Разведывательный','Logistics') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Logistics', 'Роботы для склада, доставки и логистики') END

        -- Entertainment
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN ('Трансформер','Entertainment'))
        BEGIN UPDATE RobotTypes SET Name = 'Entertainment', Description = 'Роботы для развлечений, игр и хобби' WHERE Name IN ('Трансформер','Entertainment') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Entertainment', 'Роботы для развлечений, игр и хобби') END

        -- Cleaning
        IF NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Cleaning')
        BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Cleaning', 'Роботы для уборки, мойки и чистки') END

        -- Research
        IF NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = 'Research')
        BEGIN INSERT INTO RobotTypes (Name, Description) VALUES ('Research', 'Роботы для научных исследований и анализа') END
    ", conn);
    updateRobotTypesCmd.ExecuteNonQuery();

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

    // Обновляем тестовых роботов если таблица пуста
    var seedRobotsCmd = new SqlCommand(@"
        IF NOT EXISTS (SELECT * FROM Robots)
        BEGIN
            INSERT INTO Robots (Model, Type, TypeId, Price, Stock, Description) VALUES
            ('Guardian X1', 'Security', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Security'), 45000, 3, 'Автономный робот-охранник с камерами и датчиками движения'),
            ('CleanBot 3000', 'Cleaning', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Cleaning'), 25000, 8, 'Робот-пылесос для комплексной уборки помещений'),
            ('MediCare Robot', 'Medical', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Medical'), 120000, 2, 'Медицинский робот для помощи в больницах и ухода за пациентами'),
            ('TeachBot Pro', 'Educational', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Educational'), 18000, 5, 'Образовательный робот для обучения детей программированию'),
            ('HomeHelper Mini', 'Home Assistant', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Home Assistant'), 22000, 10, 'Компактный робот-помощник для домашних задач'),
            ('LogiMover 5', 'Logistics', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = 'Logistics'), 85000, 4, 'Складской робот для перемещения и сортировки грузов')
        END", conn);
    seedRobotsCmd.ExecuteNonQuery();

    // Обновляем существующих роботов — связываем Type с TypeId
    var linkRobotTypesCmd = new SqlCommand(@"
        UPDATE Robots SET TypeId = (SELECT TOP 1 Id FROM RobotTypes WHERE Name = Robots.Type)
        WHERE TypeId IS NULL OR TypeId = 0", conn);
    linkRobotTypesCmd.ExecuteNonQuery();
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

// Render.com sets PORT env variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
