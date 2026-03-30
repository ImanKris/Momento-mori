namespace RoboStore.Models;

public class TelegramLoginViewModel
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Hash { get; set; }
    public long AuthDate { get; set; }
}
