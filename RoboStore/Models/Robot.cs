namespace RoboStore.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class Robot
{
    public int Id { get; set; }
    public string Model { get; set; } = "";
    public string Type { get; set; } = ""; // Название типа для отображения
    public int TypeId { get; set; } // FK к RobotTypes для валидации
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }

    // Навигационные свойства
    [ForeignKey(nameof(TypeId))]
    public RobotType? RobotType { get; set; }
}
