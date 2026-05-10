using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Text;
using System.Text.Json;

namespace RoboStore.Controllers;

[Authorize(Roles = "Admin")]
public class BackupController : Controller
{
    private readonly RoboStoreDbContext _context;
    private readonly ILogger<BackupController> _logger;

    public BackupController(RoboStoreDbContext context, ILogger<BackupController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Main backup management page
    /// </summary>
    public IActionResult Index()
    {
        return View("../admin/Backup");
    }

    /// <summary>
    /// Export all tables to JSON
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExportJson()
    {
        try
        {
            var backup = new
            {
                ExportedAt = DateTime.UtcNow,
                Version = "1.0",
                Tables = new
                {
                    Users = await _context.Users.ToListAsync(),
                    Robots = await _context.Robots.Include(r => r.RobotType).ToListAsync(),
                    RobotTypes = await _context.RobotTypes.ToListAsync(),
                    Orders = await _context.Orders.ToListAsync(),
                    Logs = await _context.Logs.OrderByDescending(l => l.ActionDate).Take(1000).ToListAsync()
                }
            };

            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"robostore_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            LogAction("BACKUP_EXPORT", $"Экспорт JSON: {fileName}");

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export JSON failed");
            TempData["Error"] = "Ошибка экспорта: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Export single table to CSV
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExportCsv(string table)
    {
        try
        {
            string csv;
            string fileName;

            switch (table)
            {
                case "robots":
                    var robots = await _context.Robots.ToListAsync();
                    csv = "Id,Model,Type,TypeId,Price,Stock,Description,SerialNumber\n";
                    csv += string.Join("\n", robots.Select(r =>
                        $"{r.Id},\"{r.Model}\",\"{r.Type}\",{r.TypeId},{r.Price},{r.Stock},\"{r.Description?.Replace("\"", "\"\"")}\",\"{r.SerialNumber}\""));
                    fileName = $"robots_{DateTime.Now:yyyyMMdd}.csv";
                    break;

                case "orders":
                    var orders = await _context.Orders.Include(o => o.User).Include(o => o.Robot).ToListAsync();
                    csv = "Id,UserId,UserLogin,RobotId,RobotModel,OrderDate,Status\n";
                    csv += string.Join("\n", orders.Select(o =>
                        $"{o.Id},{o.UserId},\"{o.User?.Login}\",{o.RobotId},\"{o.Robot?.Model}\",\"{o.OrderDate:yyyy-MM-dd HH:mm:ss}\",\"{o.Status}\""));
                    fileName = $"orders_{DateTime.Now:yyyyMMdd}.csv";
                    break;

                case "users":
                    var users = await _context.Users.ToListAsync();
                    csv = "Id,Login,Role,IsVerified,CreatedAt\n";
                    csv += string.Join("\n", users.Select(u =>
                        $"{u.Id},\"{u.Login}\",\"{u.Role}\",{u.IsVerified},\"{u.CreatedAt:yyyy-MM-dd HH:mm:ss}\""));
                    fileName = $"users_{DateTime.Now:yyyyMMdd}.csv";
                    break;

                case "logs":
                    var logs = await _context.Logs.OrderByDescending(l => l.ActionDate).Take(500).ToListAsync();
                    csv = "Id,ActionDate,UserLogin,ActionType,Details\n";
                    csv += string.Join("\n", logs.Select(l =>
                        $"{l.Id},\"{l.ActionDate:yyyy-MM-dd HH:mm:ss}\",\"{l.UserLogin}\",\"{l.ActionType}\",\"{l.Details?.Replace("\"", "\"\"")}\""));
                    fileName = $"logs_{DateTime.Now:yyyyMMdd}.csv";
                    break;

                default:
                    TempData["Error"] = "Неизвестная таблица";
                    return RedirectToAction("Index");
            }

            LogAction("CSV_EXPORT", $"Экспорт CSV: {table} -> {fileName}");

            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export CSV failed for table {Table}", table);
            TempData["Error"] = "Ошибка экспорта: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Restore from JSON backup
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestoreJson(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Выберите файл резервной копии";
            return RedirectToAction("Index");
        }

        if (!file.FileName.EndsWith(".json"))
        {
            TempData["Error"] = "Поддерживается только формат JSON";
            return RedirectToAction("Index");
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();

            var backup = JsonSerializer.Deserialize<BackupData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (backup == null || backup.Tables == null)
            {
                TempData["Error"] = "Некорректный формат файла";
                return RedirectToAction("Index");
            }

            // Dry run preview - count what would be restored
            var preview = new RestorePreview
            {
                UsersCount = backup.Tables.Users?.Count ?? 0,
                RobotsCount = backup.Tables.Robots?.Count ?? 0,
                OrdersCount = backup.Tables.Orders?.Count ?? 0,
                LogsCount = backup.Tables.Logs?.Count ?? 0
            };

            TempData["RestorePreview"] = JsonSerializer.Serialize(preview);
            TempData["RestoreData"] = json; // Store for actual restore on confirm

            LogAction("BACKUP_PREVIEW", $"Предпросмотр восстановления: {preview.UsersCount} users, {preview.RobotsCount} robots, {preview.OrdersCount} orders");

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore preview failed");
            TempData["Error"] = "Ошибка чтения файла: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Confirm and execute restore
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmRestore()
    {
        var json = TempData["RestoreData"] as string;
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "Данные для восстановления не найдены. Загрузите файл заново.";
            return RedirectToAction("Index");
        }

        try
        {
            var backup = JsonSerializer.Deserialize<BackupData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (backup == null || backup.Tables == null)
            {
                TempData["Error"] = "Некорректные данные";
                return RedirectToAction("Index");
            }

            // Restore RobotTypes first (foreign key dependency)
            if (backup.Tables.RobotTypes != null && backup.Tables.RobotTypes.Any())
            {
                foreach (var rt in backup.Tables.RobotTypes)
                {
                    var existing = await _context.RobotTypes.FindAsync(rt.Id);
                    if (existing != null)
                    {
                        existing.Name = rt.Name;
                        existing.Description = rt.Description;
                    }
                    else
                    {
                        _context.RobotTypes.Add(rt);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Restore Robots
            if (backup.Tables.Robots != null)
            {
                foreach (var r in backup.Tables.Robots)
                {
                    var existing = await _context.Robots.FindAsync(r.Id);
                    if (existing != null)
                    {
                        existing.Model = r.Model;
                        existing.Type = r.Type;
                        existing.TypeId = r.TypeId;
                        existing.Price = r.Price;
                        existing.Stock = r.Stock;
                        existing.Description = r.Description;
                        existing.SerialNumber = r.SerialNumber;
                    }
                    else
                    {
                        _context.Robots.Add(r);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Restore Orders
            if (backup.Tables.Orders != null)
            {
                foreach (var o in backup.Tables.Orders)
                {
                    var existing = await _context.Orders.FindAsync(o.Id);
                    if (existing == null)
                    {
                        _context.Orders.Add(o);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Restore Logs
            if (backup.Tables.Logs != null)
            {
                foreach (var l in backup.Tables.Logs)
                {
                    var existing = await _context.Logs.FindAsync(l.Id);
                    if (existing == null)
                    {
                        _context.Logs.Add(l);
                    }
                }
                await _context.SaveChangesAsync();
            }

            LogAction("BACKUP_RESTORE", "Восстановление из резервной копии выполнено");

            TempData["Message"] = "Восстановление успешно завершено";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            TempData["Error"] = "Ошибка восстановления: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Cancel restore
    /// </summary>
    [HttpPost]
    public IActionResult CancelRestore()
    {
        TempData.Remove("RestoreData");
        TempData.Remove("RestorePreview");
        return RedirectToAction("Index");
    }

    private void LogAction(string actionType, string details)
    {
        var log = new Log
        {
            ActionDate = DateTime.Now,
            UserLogin = User.Identity?.Name ?? "System",
            ActionType = actionType,
            Details = details
        };
        _context.Logs.Add(log);
        _context.SaveChanges();
    }
}

// Backup data structure
public class BackupData
{
    public DateTime ExportedAt { get; set; }
    public string? Version { get; set; }
    public BackupTables? Tables { get; set; }
}

public class BackupTables
{
    public List<User>? Users { get; set; }
    public List<Robot>? Robots { get; set; }
    public List<RobotType>? RobotTypes { get; set; }
    public List<Order>? Orders { get; set; }
    public List<Log>? Logs { get; set; }
}

public class RestorePreview
{
    public int UsersCount { get; set; }
    public int RobotsCount { get; set; }
    public int OrdersCount { get; set; }
    public int LogsCount { get; set; }
}
