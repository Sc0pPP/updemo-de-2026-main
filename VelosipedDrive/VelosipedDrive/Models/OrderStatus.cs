namespace VelosipedDrive.Models;

/// <summary>Статус заказа (таблица OrderStatus).</summary>
public class OrderStatus
{
    public int IDStatus { get; set; }
    public string StatusName { get; set; } = "";
}
