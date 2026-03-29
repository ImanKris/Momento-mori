namespace RoboStore.Models;

public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsVerified { get; set; }
    public string? VerificationCode { get; set; }
    public DateTime? CodeExpires { get; set; }
    public DateTime CreatedAt { get; set; }
}
