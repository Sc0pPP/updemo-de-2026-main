namespace MirIgrushek.Models;

/// <summary>Заказ покупателя (таблица [Order]).</summary>
public class Order
{
    public int IDOrder { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime OrderDeliveryDate { get; set; }
    public int IDPickupPoint { get; set; }
    public int IDUser { get; set; }        // 0 — в БД NULL, заказ без авторизации
    public int OrderCode { get; set; }
    public int IDStatus { get; set; }

    // Данные связанных справочников (заполняются JOIN-запросом)
    public string StatusName { get; set; } = "";
    public string PickupPointAddress { get; set; } = "";
    public string UserFullName { get; set; } = "";
}
