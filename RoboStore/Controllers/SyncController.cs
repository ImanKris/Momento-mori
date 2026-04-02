using Microsoft.AspNetCore.Mvc;
using RoboStore.Models;
using RoboStore.Services;
using System.Text.Json;

namespace RoboStore.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : Controller
{
    private readonly SyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(SyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Sync multiple queued orders from localStorage
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> SyncOrders([FromBody] List<QueuedOrderItem> orders)
    {
        if (orders == null || !orders.Any())
        {
            return Json(new { success = false, message = "No orders to sync" });
        }

        var results = new List<SyncResult>();
        var errors = new List<string>();

        foreach (var order in orders)
        {
            var result = await _syncService.ProcessQueuedOrderAsync(order);
            results.Add(result);

            if (!result.Success)
            {
                errors.Add($"Order {order.TempId}: {result.Error}");
            }
        }

        var successful = results.Count(r => r.Success);
        var alreadySynced = results.Count(r => r.AlreadySynced);

        // Log the sync attempt
        _logger.LogInformation(
            "Sync completed: {Successful}/{Total} successful, {AlreadySynced} already synced, {Errors} errors",
            successful, orders.Count, alreadySynced, errors.Count);

        return Json(new
        {
            success = errors.Count == 0,
            total = orders.Count,
            successful,
            alreadySynced,
            errors = errors.Any() ? errors : null,
            results = results.Select(r => new
            {
                tempId = orders[results.IndexOf(r)].TempId,
                orderId = r.OrderId,
                success = r.Success,
                alreadySynced = r.AlreadySynced,
                error = r.Error
            })
        });
    }

    /// <summary>
    /// Validate queue before attempting sync
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidateQueue([FromBody] List<QueuedOrderItem> orders)
    {
        if (orders == null)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        var validated = _syncService.ValidateQueue(orders);
        var removed = orders.Count - validated.Count;

        return Json(new
        {
            success = true,
            originalCount = orders.Count,
            validCount = validated.Count,
            removedCount = removed,
            validOrders = validated
        });
    }
}
