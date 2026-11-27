namespace VelosipedDrive.Models;

/// <summary>Пункт выдачи заказов (таблица PickupPoint).</summary>
public class PickupPoint
{
    public int IDPickupPoint { get; set; }
    public string Address { get; set; } = "";
}
