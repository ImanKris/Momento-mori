using Microsoft.AspNetCore.SignalR;
using RoboStore.Hubs;
using RoboStore.Models;

namespace RoboStore.Services;

public class LogBroadcastService
{
    private readonly IHubContext<LogsHub> _hubContext;

    public LogBroadcastService(IHubContext<LogsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Broadcast a new log entry to all connected SignalR clients
    /// </summary>
    public async Task BroadcastAsync(Log log)
    {
        var entry = new LogEntry
        {
            Id = log.Id,
            ActionDate = log.ActionDate.ToString("dd.MM.yyyy HH:mm:ss"),
            UserLogin = log.UserLogin,
            ActionType = log.ActionType,
            Details = log.Details,
            ColorClass = GetColorClass(log.ActionType)
        };

        await _hubContext.Clients.All.SendAsync("ReceiveLog", entry);
    }

    /// <summary>
    /// Map action type to CSS color class
    /// </summary>
    public static string GetColorClass(string actionType)
    {
        return actionType switch
        {
            "ROBOT_CREATED" or "ORDER_COMPLETED" or "LOGIN_SUCCESS" or "ORDER_SYNCED" => "log-success",
            "ROBOT_UPDATED" or "ORDER_STATUS_CHANGE" or "USER_ROLE_CHANGED" => "log-warning",
            "ROBOT_DELETED" or "ERROR" or "LOGOUT_FAILED" => "log-error",
            "LOGIN" or "LOGOUT" or "INFO" => "log-info",
            _ => ""
        };
    }
}
