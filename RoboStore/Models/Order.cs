namespace RoboStore.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int RobotId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "В обработке";

    public Robot? Robot { get; set; }
    public User? User { get; set; }
}
