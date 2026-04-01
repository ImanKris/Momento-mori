using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;

namespace RoboStore.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly RoboStoreDbContext _context;

    public AdminController(RoboStoreDbContext context)
    {
        _context = context;
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
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateRobot(Robot robot)
    {
        if (string.IsNullOrWhiteSpace(robot.Model) || string.IsNullOrWhiteSpace(robot.Type))
        {
            ModelState.AddModelError("", "Заполните все обязательные поля");
            return View(robot);
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
        return View(robot);
    }

    [HttpPost]
    public async Task<IActionResult> EditRobot(Robot robot)
    {
        if (string.IsNullOrWhiteSpace(robot.Model) || string.IsNullOrWhiteSpace(robot.Type))
        {
            ModelState.AddModelError("", "Заполните все обязательные поля");
            return View(robot);
        }

        var existingRobot = await _context.Robots.FindAsync(robot.Id);
        if (existingRobot == null)
        {
            return NotFound();
        }

        var oldModel = existingRobot.Model;
        existingRobot.Model = robot.Model;
        existingRobot.Type = robot.Type;
        existingRobot.Price = robot.Price;
        existingRobot.Stock = robot.Stock;

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

        TempData["Message"] = "Робот удалён";
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
}
