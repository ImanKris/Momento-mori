namespace RoboStore.Models;

public class User
{
    public int Id { get; set; }
    public string? Login { get; set; }
    public long TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "User";
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
