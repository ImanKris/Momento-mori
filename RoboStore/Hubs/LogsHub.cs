using Microsoft.AspNetCore.SignalR;
using RoboStore.Models;

namespace RoboStore.Hubs;

public class LogsHub : Hub
{
    private readonly IHubContext<LogsHub> _hubContext;

    public LogsHub(IHubContext<LogsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Send a new log entry to all connected clients
    /// </summary>
    public async Task SendLog(LogEntry entry)
    {
        await Clients.All.SendAsync("ReceiveLog", entry);
    }

    /// <summary>
    /// Broadcast log to all clients (server-side trigger)
    /// </summary>
    public static async Task BroadcastLogAsync(IHubContext<LogsHub> hubContext, LogEntry entry)
    {
        await hubContext.Clients.All.SendAsync("ReceiveLog", entry);
    }
}

/// <summary>
/// Log entry for SignalR transmission
/// </summary>
public class LogEntry
{
    public int Id { get; set; }
    public string ActionDate { get; set; } = "";
    public string? UserLogin { get; set; }
    public string ActionType { get; set; } = "";
    public string? Details { get; set; }
    public string ColorClass { get; set; } = "";
}
