namespace RoboStore.Models;

public class ConfirmRequest
{
    public string Code { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string ContactType { get; set; } = string.Empty;
}
