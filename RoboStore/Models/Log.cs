namespace RoboStore.Models;

public class Log
{
    public int Id { get; set; }
    public DateTime ActionDate { get; set; } = DateTime.Now;
    public string? UserLogin { get; set; }
    public string ActionType { get; set; } = "";
    public string? Details { get; set; }
}
