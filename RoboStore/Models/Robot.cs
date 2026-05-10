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

    // Характеристики
    public string? Manufacturer { get; set; }
    public double? WeightKg { get; set; }
    public string? Dimensions { get; set; } // "ШxВxГ в см"
    public int? BatteryLifeHours { get; set; }
    public string? PowerSource { get; set; } // "Аккумулятор", "Сеть 220В", "Гибрид"
    public int? MaxSpeedKmh { get; set; }
    public string? Connectivity { get; set; } // "Wi-Fi, Bluetooth 5.0, LTE"
    public string? OperatingSystem { get; set; }
    public int? WarrantyMonths { get; set; }
    public string? CountryOfOrigin { get; set; }

    // Навигационные свойства
    [ForeignKey(nameof(TypeId))]
    public RobotType? RobotType { get; set; }
}
