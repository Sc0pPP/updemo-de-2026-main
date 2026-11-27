namespace MirIgrushek.Models;

/// <summary>Позиция состава заказа (таблица OrderProduct).</summary>
public class OrderProduct
{
    public int IDOrder { get; set; }
    public int IDProduct { get; set; }
    public int ProductCount { get; set; }

    // Название товара (заполняется JOIN-запросом — для вывода в сетках)
    public string ProductName { get; set; } = "";
}
