namespace RoboStore.Models;

public class RegisterRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty; // @username или телефон
    public string ContactType { get; set; } = "username"; // "username" или "phone"
}
