namespace VelosipedDrive.Models;

/// <summary>Товар каталога (таблица Product).</summary>
public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductDescription { get; set; } = "";   // в БД может быть NULL
    public string ProductPhoto { get; set; } = "";         // в БД может быть NULL
    public decimal ProductPrice { get; set; }
    public int CurrentDiscount { get; set; }
    public int QuantityInStock { get; set; }
    public int IDUnit { get; set; }
    public int IDCategory { get; set; }
    public int IDSupplier { get; set; }
    public int IDManufacturer { get; set; }

    // Наименования справочников (заполняются JOIN-запросом — для вывода в сетках)
    public string UnitName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string ManufacturerName { get; set; } = "";
}
