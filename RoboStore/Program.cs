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
                Status NVARCHAR(MAX) NOT NULL DEFAULT N'В обработке',
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
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Промышленный',N'Industrial'))
        BEGIN UPDATE RobotTypes SET Name = N'Industrial', Description = N'Роботы для промышленного производства, сварки, сборки и автоматизации' WHERE Name IN (N'Промышленный',N'Industrial') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Industrial', N'Роботы для промышленного производства, сварки, сборки и автоматизации') END

        -- Home Assistant
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Бытовой',N'Home Assistant'))
        BEGIN UPDATE RobotTypes SET Name = N'Home Assistant', Description = N'Роботы-помощники для домашних задач и быта' WHERE Name IN (N'Бытовой',N'Home Assistant') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Home Assistant', N'Роботы-помощники для домашних задач и быта') END

        -- Educational
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Образовательный',N'Educational'))
        BEGIN UPDATE RobotTypes SET Name = N'Educational', Description = N'Роботы для обучения, программирования и развития навыков' WHERE Name IN (N'Образовательный',N'Educational') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Educational', N'Роботы для обучения, программирования и развития навыков') END

        -- Medical
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Медицинский',N'Medical'))
        BEGIN UPDATE RobotTypes SET Name = N'Medical', Description = N'Роботы для медицинских учреждений, хирургии и ухода' WHERE Name IN (N'Медицинский',N'Medical') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Medical', N'Роботы для медицинских учреждений, хирургии и ухода') END

        -- Companion
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Дроид-помощник',N'Companion'))
        BEGIN UPDATE RobotTypes SET Name = N'Companion', Description = N'Роботы-компаньоны для общения и помощи людям' WHERE Name IN (N'Дроид-помощник',N'Companion') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Companion', N'Роботы-компаньоны для общения и помощи людям') END

        -- Security
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Полицейский',N'Security'))
        BEGIN UPDATE RobotTypes SET Name = N'Security', Description = N'Роботы для охраны, патрулирования и безопасности' WHERE Name IN (N'Полицейский',N'Security') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Security', N'Роботы для охраны, патрулирования и безопасности') END

        -- Logistics
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Разведывательный',N'Logistics'))
        BEGIN UPDATE RobotTypes SET Name = N'Logistics', Description = N'Роботы для склада, доставки и логистики' WHERE Name IN (N'Разведывательный',N'Logistics') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Logistics', N'Роботы для склада, доставки и логистики') END

        -- Entertainment
        IF EXISTS (SELECT 1 FROM RobotTypes WHERE Name IN (N'Трансформер',N'Entertainment'))
        BEGIN UPDATE RobotTypes SET Name = N'Entertainment', Description = N'Роботы для развлечений, игр и хобби' WHERE Name IN (N'Трансформер',N'Entertainment') END
        ELSE BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Entertainment', N'Роботы для развлечений, игр и хобби') END

        -- Cleaning
        IF NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = N'Cleaning')
        BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Cleaning', N'Роботы для уборки, мойки и чистки') END

        -- Research
        IF NOT EXISTS (SELECT 1 FROM RobotTypes WHERE Name = N'Research')
        BEGIN INSERT INTO RobotTypes (Name, Description) VALUES (N'Research', N'Роботы для научных исследований и анализа') END
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
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'Manufacturer')
        BEGIN
            ALTER TABLE Robots ADD Manufacturer NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'WeightKg')
        BEGIN
            ALTER TABLE Robots ADD WeightKg FLOAT NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'Dimensions')
        BEGIN
            ALTER TABLE Robots ADD Dimensions NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'BatteryLifeHours')
        BEGIN
            ALTER TABLE Robots ADD BatteryLifeHours INT NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'PowerSource')
        BEGIN
            ALTER TABLE Robots ADD PowerSource NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'MaxSpeedKmh')
        BEGIN
            ALTER TABLE Robots ADD MaxSpeedKmh INT NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'Connectivity')
        BEGIN
            ALTER TABLE Robots ADD Connectivity NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'OperatingSystem')
        BEGIN
            ALTER TABLE Robots ADD OperatingSystem NVARCHAR(MAX) NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'WarrantyMonths')
        BEGIN
            ALTER TABLE Robots ADD WarrantyMonths INT NULL
        END
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Robots' AND COLUMN_NAME = 'CountryOfOrigin')
        BEGIN
            ALTER TABLE Robots ADD CountryOfOrigin NVARCHAR(MAX) NULL
        END", conn);
    addRobotColumnsCmd.ExecuteNonQuery();

    // Пересоздаём роботов с полными характеристиками
    var seedRobotsCmd = new SqlCommand(@"
        DELETE FROM Orders WHERE RobotId IN (SELECT Id FROM Robots)
        DELETE FROM Robots

        INSERT INTO Robots (Model, Type, TypeId, Price, Stock, Description, SerialNumber, Manufacturer, WeightKg, Dimensions, BatteryLifeHours, PowerSource, MaxSpeedKmh, Connectivity, OperatingSystem, WarrantyMonths, CountryOfOrigin)
        VALUES
        (N'Sentinel Pro X500', N'Security', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Security'), 189000, 5,
         N'Автономный охранный робот с системой кругового обзора 360 градусов. Оснащён 8 HD-камерами с ночным видением, лидаром, тепловизором и встроенной системой распознавания лиц. Патрулирует заданный периметр, автоматически фиксирует нарушения и отправляет уведомления оператору. Корпус из ударопрочного композита IP67.',
         N'SEC-X500-2024-001', N'RoboGuard Industries', 85.0, N'120x60x90', 12, N'Аккумулятор Li-Ion 48V', 8, N'Wi-Fi 6, 4G LTE, Bluetooth 5.2', N'RoboGuard OS 4.0', 24, N'Япония'),

        (N'AquaClean R3', N'Cleaning', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Cleaning'), 42000, 15,
         N'Робот-пылесос с функцией влажной уборки для помещений до 250 м. Навигация по технологии vSLAM с построением карты в реальном времени. Автоматическое распознавание типа покрытия, регулировка мощности всасывания до 6000 Па. Самоочищающаяся станция с УФ-стерилизацией. Управление голосом через Алису и Google Assistant.',
         N'CLN-R3-2024-047', N'SmartHome Robotics', 4.2, N'35x35x10', 3, N'Аккумулятор Li-Ion 14.4V', 0, N'Wi-Fi 5, Bluetooth 5.0', N'CleanOS 2.1', 12, N'Южная Корея'),

        (N'MedAssist Aria', N'Medical', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Medical'), 520000, 2,
         N'Медицинский робот-ассистент для клиник и стационаров. Автоматическая доставка медикаментов и анализов между отделениями. Встроенный модуль дезинфекции УФ-С. Бесконтактное измерение температуры и пульса пациентов. Интеграция с МИС через HL7 FHIR. Сертификат соответствия для медицинских учреждений.',
         N'MED-ARIA-2024-003', N'MedRobotics GmbH', 120.0, N'60x55x150', 10, N'Аккумулятор LiFePO4 + док-станция', 5, N'Wi-Fi 6E, Bluetooth 5.3, Ethernet', N'MedOS 3.2 (Linux)', 36, N'Германия'),

        (N'EduBot Junior', N'Educational', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Educational'), 28000, 20,
         N'Образовательный робот для детей от 8 лет. Программируется через визуальный редактор блоков (аналог Scratch) и Python. 12 датчиков: расстояния, цвета, гироскоп, акселерометр, касания, звука. LED-матрица 8x8 для отображения эмоций. Совместим с LEGO Technic. Более 50 готовых уроков по робототехнике и программированию.',
         N'EDU-JR-2024-112', N'LearnBots Inc.', 1.8, N'18x15x20', 6, N'Аккумулятор Li-Po 7.4V', 0, N'Wi-Fi 5, Bluetooth 5.0, USB-C', N'EduOS (Raspberry Pi)', 24, N'США'),

        (N'HomeHub Max', N'Home Assistant', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Home Assistant'), 67000, 8,
         N'Домашний робот-помощник с продвинутым голосовым ИИ. Управляет всей экосистемой умного дома: освещение, климат, безопасность, мультимедиа. 10-дюймовый HD-дисплей для видеозвонков и рецептов. Автономное перемещение по квартире, распознавание членов семьи. Встроенный проектор для показа фильмов на стене. Напоминания, таймеры, контроль расхода электричества.',
         N'HOM-MAX-2024-055', N'SmartHome Robotics', 12.5, N'40x35x85', 8, N'Аккумулятор Li-Ion + док-станция', 3, N'Wi-Fi 6E, Bluetooth 5.3, Zigbee, Thread', N'HomeOS 5.0 (Android)', 18, N'Южная Корея'),

        (N'LogiMover X10', N'Logistics', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Logistics'), 310000, 4,
         N'Складской автономный робот грузоподъёмностью до 500 кг. Навигация по QR-меткам и лидару одновременно. Автоматическое формирование маршрутов, объезд препятствий, работа в группе до 50 роботов. Интеграция с WMS через REST API. Подъёмная платформа для стеллажей. Работает 16 часов без подзарядки, автоматический возврат на зарядку.',
         N'LOG-X10-2024-019', N'AutoLogistics Corp.', 250.0, N'80x60x35', 16, N'Аккумулятор LiFePO4 72V', 6, N'Wi-Fi 6, 5G, Bluetooth 5.2', N'ROS 2 (Ubuntu)', 24, N'Китай'),

        (N'Buddy Companion v2', N'Companion', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Companion'), 95000, 6,
         N'Социальный робот-компаньон с эмоциональным ИИ. Распознаёт настроение по голосу и мимике, адаптирует стиль общения. Поддерживает диалоги на русском, английском и китайском языках. Воспроизводит музыку, читает аудиокниги, играет в викторины. Подходит для помощи пожилым людям: напоминания о лекарствах, SOS-кнопка, видеосвязь с родственниками.',
         N'CMP-BDY-2024-031', N'SocialBots Ltd.', 8.0, N'30x25x55', 10, N'Аккумулятор Li-Ion 21.6V', 0, N'Wi-Fi 6, Bluetooth 5.2, LTE (опция)', N'CompanionOS 3.0 (Android)', 12, N'Япония'),

        (N'InduArm R6', N'Industrial', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Industrial'), 780000, 3,
         N'Промышленный робот-манипулятор с 6 степенями свободы. Грузоподъёмность 20 кг, точность позиционирования 0.02 мм. Подходит для сварки, покраски, сборки, паллетирования. Программирование через обучение (ведение за руку) или G-code. Скорость движения до 2 м/с. Совместим с конвейерными системами. Встроенная система безопасности с лазерными датчиками.',
         N'IND-R6-2024-008', N'HeavyRobotics AG', 180.0, N'85x85x140', 0, N'Сеть 380В, 3 фазы', 0, N'Ethernet, Profinet, EtherCAT', N'RobotStudio (Windows)', 36, N'Германия'),

        (N'FunBot Party', N'Entertainment', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Entertainment'), 35000, 12,
         N'Развлекательный робот для мероприятий и вечеринок. Танцует, поёт, проводит викторины и интерактивные игры. Встроенный проектор и LED-подсветка для светового шоу. Распознаёт музыку и двигается в такт. Управление через приложение. Библиотека из 200+ танцев и 50+ сценариев праздников. Подходит для детских и корпоративных мероприятий.',
         N'ENT-FBP-2024-066', N'FunRobotics Co.', 6.5, N'35x30x60', 5, N'Аккумулятор Li-Ion 14.4V', 0, N'Wi-Fi 5, Bluetooth 5.0', N'FunOS 2.0 (Android)', 12, N'Китай'),

        (N'SciExplorer Pro', N'Research', (SELECT TOP 1 Id FROM RobotTypes WHERE Name = N'Research'), 450000, 2,
         N'Исследовательский мобильный робот для лабораторий и полевых условий. Модульная платформа: установка манипулятора, сенсоров, пробоотборников. Навигация SLAM с лидаром Velodyne. Защита IP65 для работы на открытом воздухе. Программирование на Python/C++ через ROS 2. Встроенный GPU NVIDIA Jetson для обработки данных на борту.',
         N'RES-SPR-2024-005', N'SciRobotics Labs', 45.0, N'70x55x40', 8, N'Аккумулятор Li-Ion 48V', 12, N'Wi-Fi 6, 4G LTE, USB 3.0, Ethernet', N'ROS 2 Humble (Ubuntu 22.04)', 24, N'США')
    ", conn);
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
