-- Таблица пользователей с верификацией
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Login NVARCHAR(50) UNIQUE NOT NULL,
    Email NVARCHAR(100) NULL,
    Phone NVARCHAR(20) NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(20) DEFAULT 'User', -- User, Manager, Admin
    IsVerified BIT DEFAULT 0,
    VerificationCode NVARCHAR(10) NULL,
    CodeExpires DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);

-- Таблица роботов
CREATE TABLE Robots (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Model NVARCHAR(100) NOT NULL,
    Type NVARCHAR(50) CHECK (Type IN ('Бытовой', 'Промышленный')),
    Price DECIMAL(10,2) NOT NULL,
    Stock INT DEFAULT 0
);

-- Таблица заказов
CREATE TABLE Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT FOREIGN KEY REFERENCES Users(Id),
    RobotId INT FOREIGN KEY REFERENCES Robots(Id),
    OrderDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(50) DEFAULT 'В обработке'
);

-- Таблица логов
CREATE TABLE Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ActionDate DATETIME DEFAULT GETDATE(),
    UserLogin NVARCHAR(50) NOT NULL,
    ActionType NVARCHAR(50) NOT NULL, -- LOGIN, SALE, ERROR, BACKUP
    Details NVARCHAR(MAX) NULL
);
салам алейкум
