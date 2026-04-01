using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;

namespace RoboStore.Controllers;

[Authorize(Roles = "Manager")]
public class ManagerController : Controller
{
    private readonly RoboStoreDbContext _context;

    public ManagerController(RoboStoreDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var today = DateTime.Today;

        var inProcessingCount = _context.Orders.Count(o => o.Status == "В обработке");
        var completedTodayCount = _context.Orders.Count(o => o.Status == "Выполнен" && o.OrderDate.Date == today);
        var totalOrdersCount = _context.Orders.Count();
        var totalCustomersCount = _context.Users.Count(u => u.Role == "User");

        var recentOrders = _context.Orders
            .Include(o => o.Robot)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToList();

        ViewBag.InProcessingCount = inProcessingCount;
        ViewBag.CompletedTodayCount = completedTodayCount;
        ViewBag.TotalOrdersCount = totalOrdersCount;
        ViewBag.TotalCustomersCount = totalCustomersCount;
        ViewBag.RecentOrders = recentOrders;

        return View();
    }

    public IActionResult Orders()
    {
        var orders = _context.Orders
            .Include(o => o.Robot)
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    public IActionResult OrderDetails(int id)
    {
        var order = _context.Orders
            .Include(o => o.Robot)
            .Include(o => o.User)
            .FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            return Json(new { success = false, message = "Заказ не найден" });
        }

        var validStatuses = new[] { "В обработке", "Отправлен", "Выполнен", "Отменён" };
        if (!validStatuses.Contains(status))
        {
            return Json(new { success = false, message = "Недопустимый статус" });
        }

        var oldStatus = order.Status;
        order.Status = status;
        await _context.SaveChangesAsync();

        // Логирование
        var userLogin = User.Identity?.Name ?? "Unknown";
        var log = new Log
        {
            ActionDate = DateTime.Now,
            UserLogin = userLogin,
            ActionType = "ORDER_STATUS_CHANGE",
            Details = $"Заказ #{orderId}: {oldStatus} → {status}"
        };
        _context.Logs.Add(log);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Статус обновлён" });
    }

    public IActionResult Customers()
    {
        var customers = _context.Users
            .Where(u => u.Role == "User")
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        return View(customers);
    }
}
