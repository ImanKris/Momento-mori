using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Text.Json;

namespace RoboStore.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : Controller
{
    private readonly RoboStoreDbContext _context;

    public CheckoutController(RoboStoreDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// API checkout - tries normal processing, returns JSON result
    /// Used by fallback system when DB might be unavailable
    /// </summary>
    [HttpPost]
    public IActionResult CheckoutApi([FromBody] CheckoutRequest request)
    {
        try
        {
            // Validate auth
            var userLogin = User.Identity?.Name;
            if (string.IsNullOrEmpty(userLogin))
            {
                return Json(new { success = false, error = "not_authenticated" });
            }

            var user = _context.Users.FirstOrDefault(u => u.Login == userLogin);
            if (user == null)
            {
                return Json(new { success = false, error = "user_not_found" });
            }

            // Parse cart from request
            List<int> cart;
            if (request.Cart != null)
            {
                cart = request.Cart;
            }
            else
            {
                // Try session as fallback
                var sessionData = HttpContext.Session.GetString("Cart");
                cart = string.IsNullOrEmpty(sessionData)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
            }

            if (cart.Count == 0)
            {
                return Json(new { success = false, error = "cart_empty" });
            }

            var createdOrders = new List<int>();
            var errors = new List<string>();

            // Process each item
            foreach (var robotId in cart)
            {
                var robot = _context.Robots.Find(robotId);
                if (robot == null)
                {
                    errors.Add($"Robot {robotId} not found");
                    continue;
                }
                if (robot.Stock <= 0)
                {
                    errors.Add($"Robot {robot.Model} out of stock");
                    continue;
                }

                var order = new Order
                {
                    UserId = user.Id,
                    RobotId = robotId,
                    OrderDate = DateTime.Now,
                    Status = "В обработке",
                    TempOrderId = request.TempOrderIdPrefix + "_" + robotId // For tracking
                };
                _context.Orders.Add(order);
                robot.Stock -= 1;
                createdOrders.Add(robotId);
            }

            _context.SaveChanges();

            // Clear session cart
            HttpContext.Session.Remove("Cart");

            // Log
            var log = new Log
            {
                ActionDate = DateTime.Now,
                UserLogin = userLogin,
                ActionType = "ORDER_CREATED",
                Details = $"Checkout API: created {createdOrders.Count} orders"
            };
            _context.Logs.Add(log);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                ordersCreated = createdOrders.Count,
                cartCleared = true,
                errors = errors.Count > 0 ? errors : null
            });
        }
        catch
        {
            // DB error - tell client to use fallback
            return Json(new
            {
                success = false,
                error = "database_error",
                message = "Database unavailable",
                useFallback = true
            });
        }
    }
}

public class CheckoutRequest
{
    public List<int>? Cart { get; set; }
    public string? TempOrderIdPrefix { get; set; }
}
