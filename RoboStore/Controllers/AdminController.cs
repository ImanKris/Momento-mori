using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using RoboStore.Services;

namespace RoboStore.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly RoboStoreDbContext _context;
    private readonly LogBroadcastService _logBroadcast;

    public AdminController(RoboStoreDbContext context, LogBroadcastService logBroadcast)
    {
        _context = context;
        _logBroadcast = logBroadcast;
    }

    public IActionResult Index()
    {
        var robotsCount = _context.Robots.Count();
        var usersCount = _context.Users.Count();
        var ordersCount = _context.Orders.Count();
        var totalSales = _context.Orders
            .Where(o => o.Status == "Выполнен")
            .Include(o => o.Robot)
            .Sum(o => o.Robot != null ? o.Robot.Price : 0);

        var pendingOrders = _context.Orders.Count(o => o.Status == "В обработке");
        var completedToday = _context.Orders.Count(o => o.Status == "Выполнен" && o.OrderDate.Date == DateTime.Today);

        ViewBag.RobotsCount = robotsCount;
        ViewBag.UsersCount = usersCount;
        ViewBag.OrdersCount = ordersCount;
        ViewBag.TotalSales = totalSales;
        ViewBag.PendingOrders = pendingOrders;
        ViewBag.CompletedToday = completedToday;

        return View();
    }

    public IActionResult Robots()
    {
        var robots = _context.Robots.ToList();
        return View(robots);
    }

    [HttpGet]
    public IActionResult CreateRobot()
    {
        ViewBag.RobotTypes = _context.RobotTypes.OrderBy(t => t.Name).ToList();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateRobot(Robot robot)
    {
        // Получаем список типов для повторного отображения формы при ошибке
        ViewBag.RobotTypes = _context.RobotTypes.OrderBy(t => t.Name).ToList();

        // Валидация
        if (string.IsNullOrWhiteSpace(robot.Model))
        {
            ModelState.AddModelError("", "Укажите модель робота");
            return View(robot);
        }

        if (robot.TypeId <= 0)
        {
            ModelState.AddModelError("", "Выберите тип робота");
            return View(robot);
        }

        // Получаем название типа из RobotTypes
        var robotType = await _context.RobotTypes.FindAsync(robot.TypeId);
        if (robotType == null)
        {
            ModelState.AddModelError("", "Выбран неверный тип робота");
            return View(robot);
        }

        robot.Type = robotType.Name;

        // Автогенерация описания если пустое
        if (string.IsNullOrWhiteSpace(robot.Description))
        {
            robot.Description = GenerateRobotDescription(robot, robotType);
        }

        _context.Robots.Add(robot);
        await _context.SaveChangesAsync();

        // Логирование
        var log = new Log
        {
            ActionDate = DateTime.Now,
            UserLogin = User.Identity?.Name ?? "Unknown",
            ActionType = "ROBOT_CREATED",
            Details = $"Создан робот: {robot.Model} ({robot.Type}) - {robot.Price} руб."
        };
        _context.Logs.Add(log);
        await _context.SaveChangesAsync();
        await _logBroadcast.BroadcastAsync(log);

        TempData["Message"] = "Робот успешно создан";
        return RedirectToAction("Robots");
    }

    [HttpGet]
    public IActionResult EditRobot(int id)
    {
        var robot = _context.Robots.Find(id);
        if (robot == null)
        {
            return NotFound();
        }
        ViewBag.RobotTypes = _context.RobotTypes.OrderBy(t => t.Name).ToList();
        return View(robot);
    }

    [HttpPost]
    public async Task<IActionResult> EditRobot(Robot robot)
    {
        ViewBag.RobotTypes = _context.RobotTypes.OrderBy(t => t.Name).ToList();

        if (string.IsNullOrWhiteSpace(robot.Model))
        {
            ModelState.AddModelError("", "Укажите модель робота");
            return View(robot);
        }

        if (robot.TypeId <= 0)
        {
            ModelState.AddModelError("", "Выберите тип робота");
            return View(robot);
        }

        var existingRobot = await _context.Robots.FindAsync(robot.Id);
        if (existingRobot == null)
        {
            return NotFound();
        }

        // Получаем название типа
        var robotType = await _context.RobotTypes.FindAsync(robot.TypeId);
        if (robotType != null)
        {
            existingRobot.Type = robotType.Name;
            existingRobot.TypeId = robot.TypeId;
        }

        var oldModel = existingRobot.Model;
        existingRobot.Model = robot.Model;
        existingRobot.Price = robot.Price;
        existingRobot.Stock = robot.Stock;
        existingRobot.SerialNumber = robot.SerialNumber;

        // Автогенерация описания если пустое
        if (string.IsNullOrWhiteSpace(robot.Description))
        {
            existingRobot.Description = GenerateRobotDescription(existingRobot, robotType ?? new RobotType { Name = existingRobot.Type });
        }
        else
        {
            existingRobot.Description = robot.Description;
        }

        await _context.SaveChangesAsync();

        // Логирование
        var log = new Log
        {
            ActionDate = DateTime.Now,
            UserLogin = User.Identity?.Name ?? "Unknown",
            ActionType = "ROBOT_UPDATED",
            Details = $"Изменён робот: {oldModel} → {robot.Model}"
        };
        _context.Logs.Add(log);
        await _context.SaveChangesAsync();
        await _logBroadcast.BroadcastAsync(log);

        TempData["Message"] = "Робот обновлён";
        return RedirectToAction("Robots");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRobot(int id)
    {
        var robot = await _context.Robots.FindAsync(id);
        if (robot == null)
        {
            return NotFound();
        }

        try
        {
            // Удаляем связанные заказы (если есть)
            var relatedOrders = _context.Orders.Where(o => o.RobotId == id).ToList();
            if (relatedOrders.Any())
            {
                _context.Orders.RemoveRange(relatedOrders);
            }

            // Логирование
            var log = new Log
            {
                ActionDate = DateTime.Now,
                UserLogin = User.Identity?.Name ?? "Unknown",
                ActionType = "ROBOT_DELETED",
                Details = $"Удалён робот: {robot.Model}"
            };
            _context.Logs.Add(log);

            _context.Robots.Remove(robot);
            await _context.SaveChangesAsync();
            await _logBroadcast.BroadcastAsync(log);

            TempData["Message"] = "Робот удалён";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка при удалении робота: {ex.Message}";
        }

        return RedirectToAction("Robots");
    }

    public IActionResult Users()
    {
        var users = _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateUserRole(int userId, string newRole)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Json(new { success = false, message = "Пользователь не найден" });
        }

        var validRoles = new[] { "User", "Manager", "Admin" };
        if (!validRoles.Contains(newRole))
        {
            return Json(new { success = false, message = "Недопустимая роль" });
        }

        var oldRole = user.Role;
        user.Role = newRole;
        await _context.SaveChangesAsync();

        // Логирование
        var log = new Log
        {
            ActionDate = DateTime.Now,
            UserLogin = User.Identity?.Name ?? "Unknown",
            ActionType = "USER_ROLE_CHANGED",
            Details = $"Пользователь {user.Login}: {oldRole} → {newRole}"
        };
        _context.Logs.Add(log);
        await _context.SaveChangesAsync();
        await _logBroadcast.BroadcastAsync(log);

        return Json(new { success = true, message = "Роль обновлена" });
    }

    public IActionResult Logs()
    {
        var logs = _context.Logs
            .OrderByDescending(l => l.ActionDate)
            .Take(100)
            .ToList();

        return View(logs);
    }

    /// <summary>
    /// Автогенерация описания робота на основе его характеристик
    /// </summary>
    private string GenerateRobotDescription(Robot robot, RobotType robotType)
    {
        var description = $"Робот {robot.Model} относится к типу «{robotType.Name}».";

        if (!string.IsNullOrWhiteSpace(robotType.Description))
        {
            description += $" {robotType.Description}.";
        }

        if (!string.IsNullOrWhiteSpace(robot.SerialNumber))
        {
            description += $" Серийный номер: {robot.SerialNumber}.";
        }

        description += $" Цена: {robot.Price:N0} руб.{(robot.Stock > 0 ? $" В наличии: {robot.Stock} шт." : " Нет в наличии.")}";

        return description;
    }
}
