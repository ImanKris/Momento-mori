using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;

namespace RoboStore.Services;

public class SyncService
{
    private readonly RoboStoreDbContext _context;
    private readonly ILogger<SyncService> _logger;

    public SyncService(RoboStoreDbContext context, ILogger<SyncService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Process a queued order. Idempotent via temp ID tracking.
    /// Returns server OrderId if successful, or error info.
    /// </summary>
    public async Task<SyncResult> ProcessQueuedOrderAsync(QueuedOrderItem item)
    {
        try
        {
            // Validate required fields
            if (item.UserId <= 0 || item.RobotId <= 0)
            {
                return new SyncResult { Success = false, Error = "Invalid user or robot ID" };
            }

            // Check if robot exists and has stock
            var robot = await _context.Robots.FindAsync(item.RobotId);
            if (robot == null)
            {
                return new SyncResult { Success = false, Error = "Robot not found" };
            }

            if (robot.Stock <= 0)
            {
                return new SyncResult { Success = false, Error = "Robot out of stock" };
            }

            // Check if this temp order was already synced (idempotency)
            // by checking if an order with same temp ID exists
            if (!string.IsNullOrEmpty(item.TempId))
            {
                var existing = await _context.Orders
                    .FirstOrDefaultAsync(o => o.TempOrderId == item.TempId);

                if (existing != null)
                {
                    return new SyncResult
                    {
                        Success = true,
                        OrderId = existing.Id,
                        AlreadySynced = true,
                        Message = "Order already synced"
                    };
                }
            }

            // Create the order
            var order = new Order
            {
                UserId = item.UserId,
                RobotId = item.RobotId,
                OrderDate = string.IsNullOrEmpty(item.OrderDate)
                    ? DateTime.Now
                    : DateTime.Parse(item.OrderDate),
                Status = "В обработке",
                TempOrderId = item.TempId // track for idempotency
            };

            _context.Orders.Add(order);

            // Decrease stock
            robot.Stock -= 1;

            await _context.SaveChangesAsync();

            // Log the sync
            var log = new Log
            {
                ActionDate = DateTime.Now,
                UserLogin = item.UserLogin ?? "sync",
                ActionType = "ORDER_SYNCED",
                Details = $"Синхронизирован заказ #{order.Id} (временный ID: {item.TempId})"
            };
            _context.Logs.Add(log);
            await _context.SaveChangesAsync();

            return new SyncResult
            {
                Success = true,
                OrderId = order.Id,
                AlreadySynced = false,
                Message = "Order created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing queued order: {TempId}", item.TempId);
            return new SyncResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Validate queued orders list - remove obviously invalid entries
    /// </summary>
    public List<QueuedOrderItem> ValidateQueue(List<QueuedOrderItem> queue)
    {
        return queue.Where(q => q.UserId > 0 && q.RobotId > 0).ToList();
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public int OrderId { get; set; }
    public bool AlreadySynced { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Represents a queued order from localStorage
/// </summary>
public class QueuedOrderItem
{
    public string? TempId { get; set; }
    public int UserId { get; set; }
    public int RobotId { get; set; }
    public string? OrderDate { get; set; }
    public string? UserLogin { get; set; }
}
