namespace RoboStore.Models;

public class Robot
{
    public int Id { get; set; }
    public string Model { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
