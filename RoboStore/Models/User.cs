namespace RoboStore.Models;

public class User
{
    public int Id { get; set; }
    public string? Login { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "User";
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
