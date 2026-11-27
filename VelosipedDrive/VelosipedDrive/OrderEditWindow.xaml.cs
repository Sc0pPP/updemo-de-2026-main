using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace VelosipedDrive;

/// <summary>
/// Окно добавления нового заказа (orderId = 0) или редактирования
/// существующего. Состав собирается кнопками из списка товаров —
/// несколько позиций с изменением количества («корзина»).
/// Оформить заказ могут клиент и администратор, редактировать — только администратор.
/// </summary>
public partial class OrderEditWindow : Window
{
    private readonly int _orderId;
    private readonly List<OrderLine> _items = new List<OrderLine>();
    private readonly List<ProductOption> _products = new List<ProductOption>();
    private readonly Dictionary<int, int> _oldItems = new Dictionary<int, int>();

    public OrderEditWindow(int orderId)
    {
        InitializeComponent();
        _orderId = orderId;
        FillProducts();
        FillLookups();
        if (orderId == 0)
        {
            Title = "Добавление заказа — ООО «ВелосипедДрайв»";
            rowId.Visibility = Visibility.Collapsed;
            dpOrderDate.SelectedDate = DateTime.Today;
            dpDeliveryDate.SelectedDate = DateTime.Today.AddDays(3);
            txtCode.Text = GetNextCode().ToString(); // код вычисляется +1 к максимальному
            // Клиент оформляет покупку: статус всегда «Новый», меняет его администратор
            if (App.IsClient)
            {
                cmbStatus.SelectedValue = 2;
                cmbStatus.IsEnabled = false;
            }
        }
        else
        {
            Title = "Редактирование заказа — ООО «ВелосипедДрайв»";
            LoadOrder();
        }
        RefreshItems();
    }

    /// <summary>Товар в выпадающем списке: ID, название, цена со скидкой и остаток.</summary>
    private class ProductOption
    {
        public int ID;
        public string Name = "";
        public decimal Price;
        public int Discount;
        public int Stock;

        public decimal FinalPrice => Math.Round(Price * (100m - Discount) / 100m, 2);

        public override string ToString() =>
            ID + " · " + Name + " — " + FinalPrice.ToString("0.00") + " ₽ (остаток " + Stock + ")";
    }

    /// <summary>Позиция состава заказа (корзины): товар, цена и количество.</summary>
    private class OrderLine
    {
        public int ID;
        public string Name = "";
        public decimal Price;
        public int Count;

        public string Display => Name + " — " + Price.ToString("0.00") + " ₽/шт";
    }

    /// <summary>Список товаров для выпадающего списка с ценой со скидкой и остатком.</summary>
    private void FillProducts()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlCommand command = new SqlCommand(
                "SELECT ProductID, ProductName, ProductPrice, CurrentDiscount, QuantityInStock " +
                "FROM Product ORDER BY ProductID;", connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _products.Add(new ProductOption
                {
                    ID = (int)reader["ProductID"],
                    Name = (string)reader["ProductName"],
                    Price = (decimal)reader["ProductPrice"],
                    Discount = Convert.ToInt32(reader["CurrentDiscount"]),
                    Stock = (int)reader["QuantityInStock"]
                });
            }
            cmbProduct.ItemsSource = _products;
            if (_products.Count > 0)
            {
                cmbProduct.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить список товаров из базы данных.\nПодробности: "
                + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FillLookups()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlDataAdapter adapter = new SqlDataAdapter(
                "SELECT IDStatus, StatusName FROM OrderStatus; " +
                "SELECT IDPickupPoint, Address FROM PickupPoint;", connection);
            DataSet data = new DataSet();
            adapter.Fill(data);
            cmbStatus.ItemsSource = data.Tables[0].DefaultView;
            cmbPickupPoint.ItemsSource = data.Tables[1].DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить справочники из базы данных.\nПодробности: "
                + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Лимит склада для позиции: остаток в БД плюс старый состав заказа
    /// (при редактировании он возвращается на склад при сохранении).</summary>
    private int CapFor(int productId)
    {
        ProductOption product = _products.Find(p => p.ID == productId);
        if (product == null)
        {
            return 0;
        }
        int oldCount = _oldItems.TryGetValue(productId, out int count) ? count : 0;
        return product.Stock + oldCount;
    }

    /// <summary>Добавляет выбранный товар в состав (корзину) с контролем остатка.</summary>
    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProduct.SelectedItem is not ProductOption product)
        {
            ShowError("Выберите товар из выпадающего списка.", cmbProduct);
            return;
        }
        if (!int.TryParse(txtCount.Text.Trim(), out int count) || count <= 0)
        {
            ShowError("Количество должно быть целым положительным числом.", txtCount);
            return;
        }
        int cap = CapFor(product.ID);
        OrderLine existing = _items.Find(line => line.ID == product.ID);
        int already = existing == null ? 0 : existing.Count;
        if (already + count > cap)
        {
            ShowError("Товара недостаточно на складе: доступно " + cap +
                ", уже добавлено " + already + ", добавить можно не более " +
                (cap - already) + ".", txtCount);
            return;
        }
        if (existing == null)
        {
            _items.Add(new OrderLine { ID = product.ID, Name = product.Name,
                Price = product.FinalPrice, Count = count });
        }
        else
        {
            existing.Count += count;
        }
        txtCount.Text = "1";
        RefreshItems();
    }

    private void BtnMinus_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not OrderLine line)
        {
            return;
        }
        line.Count--;
        if (line.Count <= 0)
        {
            _items.Remove(line);
        }
        RefreshItems();
    }

    private void BtnPlus_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not OrderLine line)
        {
            return;
        }
        if (line.Count >= CapFor(line.ID))
        {
            ShowError("Больше товара на складе нет: максимум " + CapFor(line.ID) + " шт.", cmbProduct);
            return;
        }
        line.Count++;
        RefreshItems();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is OrderLine line)
        {
            _items.Remove(line);
            RefreshItems();
        }
    }

    /// <summary>Перерисовывает состав и итоговую сумму заказа.</summary>
    private void RefreshItems()
    {
        listItems.ItemsSource = null;
        listItems.ItemsSource = _items;
        decimal total = _items.Sum(line => line.Price * line.Count);
        txtTotal.Text = total.ToString("0.00") + " ₽";
    }

    /// <summary>Подгружает выбранный заказ и заполняет состав (корзину) его позициями.</summary>
    private void LoadOrder()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlCommand command = new SqlCommand(@"
                SELECT o.IDOrder, o.OrderCode, o.OrderDate, o.OrderDeliveryDate,
                       o.IDStatus, o.IDPickupPoint,
                       op.IDProduct, p.ProductName, op.ProductCount, p.ProductPrice,
                       p.CurrentDiscount, p.QuantityInStock
                FROM [Order] o
                LEFT JOIN OrderProduct op ON op.IDOrder = o.IDOrder
                LEFT JOIN Product p ON p.ProductID = op.IDProduct
                WHERE o.IDOrder = @Id;", connection);
            command.Parameters.AddWithValue("@Id", _orderId);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            bool headerRead = false;
            while (reader.Read())
            {
                if (!headerRead)
                {
                    headerRead = true;
                    txtId.Text = reader["IDOrder"].ToString();
                    txtCode.Text = reader["OrderCode"].ToString();
                    cmbStatus.SelectedValue = reader["IDStatus"];
                    cmbPickupPoint.SelectedValue = reader["IDPickupPoint"];
                    dpOrderDate.SelectedDate = Convert.ToDateTime(reader["OrderDate"]);
                    dpDeliveryDate.SelectedDate = Convert.ToDateTime(reader["OrderDeliveryDate"]);
                }
                if (reader["IDProduct"] == DBNull.Value)
                {
                    continue; // заказ без состава
                }
                int id = (int)reader["IDProduct"];
                int count = (int)reader["ProductCount"];
                _oldItems[id] = count;
                _items.Add(new OrderLine
                {
                    ID = id,
                    Name = (string)reader["ProductName"],
                    Price = Math.Round((decimal)reader["ProductPrice"] *
                        (100m - Convert.ToInt32(reader["CurrentDiscount"])) / 100m, 2),
                    Count = count
                });
            }
            if (!headerRead)
            {
                MessageBox.Show("Заказ не найден в базе данных.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить данные заказа.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Следующий код для получения: на 1 больше максимального в базе.</summary>
    private int GetNextCode()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlCommand command = new SqlCommand(
                "SELECT ISNULL(MAX(OrderCode), 900) + 1 FROM [Order];", connection);
            connection.Open();
            return (int)command.ExecuteScalar();
        }
        catch (Exception)
        {
            return 901;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // ---- Состав заказа не должен быть пустым ----
        if (_items.Count == 0)
        {
            ShowError("Добавьте в состав заказа хотя бы один товар (кнопка «+ Добавить товар»).",
                cmbProduct);
            return;
        }
        if (cmbStatus.SelectedValue == null)
        {
            ShowError("Выберите статус заказа из выпадающего списка.", cmbStatus);
            return;
        }
        if (cmbPickupPoint.SelectedValue == null)
        {
            ShowError("Выберите адрес пункта выдачи из выпадающего списка.", cmbPickupPoint);
            return;
        }
        if (dpOrderDate.SelectedDate == null || dpDeliveryDate.SelectedDate == null)
        {
            ShowError("Укажите дату заказа и дату выдачи.", dpOrderDate);
            return;
        }
        if (dpDeliveryDate.SelectedDate.Value < dpOrderDate.SelectedDate.Value)
        {
            ShowError("Дата выдачи не может быть раньше даты заказа. Исправьте даты.", dpDeliveryDate);
            return;
        }
        // ---- Код для получения должен быть уникальным ----
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlCommand command = new SqlCommand(
                "SELECT COUNT(*) FROM [Order] WHERE OrderCode = @Code AND IDOrder <> @Id;", connection);
            command.Parameters.AddWithValue("@Code", int.Parse(txtCode.Text));
            command.Parameters.AddWithValue("@Id", _orderId);
            connection.Open();
            if ((int)command.ExecuteScalar() > 0)
            {
                ShowError("Код для получения «" + txtCode.Text +
                    "» уже используется другим заказом. Укажите другой код.", txtCode);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось проверить код заказа.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        // ---- Сохранение заказа и его состава одной транзакцией ----
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                List<KeyValuePair<int, int>> pairs = _items
                    .Select(line => new KeyValuePair<int, int>(line.ID, line.Count)).ToList();
                if (_orderId == 0)
                {
                    using SqlCommand command = new SqlCommand(@"
                        INSERT INTO [Order] (OrderDate, OrderDeliveryDate, IDPickupPoint,
                                             IDUser, OrderCode, IDStatus)
                        VALUES (@OrderDate, @DeliveryDate, @PickupPoint, @UserId, @Code, @Status);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction);
                    command.Parameters.AddWithValue("@OrderDate", dpOrderDate.SelectedDate.Value);
                    command.Parameters.AddWithValue("@DeliveryDate", dpDeliveryDate.SelectedDate.Value);
                    command.Parameters.AddWithValue("@PickupPoint", cmbPickupPoint.SelectedValue);
                    // Заказ привязывается к текущему пользователю (0 — гость, NULL в БД)
                    command.Parameters.AddWithValue("@UserId",
                        App.CurrentUserID == 0 ? (object)DBNull.Value : App.CurrentUserID);
                    command.Parameters.AddWithValue("@Code", int.Parse(txtCode.Text));
                    command.Parameters.AddWithValue("@Status", cmbStatus.SelectedValue);
                    int newId = (int)command.ExecuteScalar();
                    SaveItems(connection, transaction, newId, pairs);
                    ApplyStockDelta(connection, transaction, pairs, -1); // списываем товар со склада
                }
                else
                {
                    using SqlCommand command = new SqlCommand(@"
                        UPDATE [Order]
                        SET OrderDate = @OrderDate, OrderDeliveryDate = @DeliveryDate,
                            IDPickupPoint = @PickupPoint, IDStatus = @Status
                        WHERE IDOrder = @Id;", connection, transaction);
                    command.Parameters.AddWithValue("@OrderDate", dpOrderDate.SelectedDate.Value);
                    command.Parameters.AddWithValue("@DeliveryDate", dpDeliveryDate.SelectedDate.Value);
                    command.Parameters.AddWithValue("@PickupPoint", cmbPickupPoint.SelectedValue);
                    command.Parameters.AddWithValue("@Status", cmbStatus.SelectedValue);
                    command.Parameters.AddWithValue("@Id", _orderId);
                    command.ExecuteNonQuery();
                    // Возвращаем старый состав на склад, затем списываем новый
                    using SqlCommand restore = new SqlCommand(@"
                        UPDATE p SET p.QuantityInStock = p.QuantityInStock + op.ProductCount
                        FROM Product p
                        INNER JOIN OrderProduct op ON op.IDProduct = p.ProductID
                        WHERE op.IDOrder = @Id;", connection, transaction);
                    restore.Parameters.AddWithValue("@Id", _orderId);
                    restore.ExecuteNonQuery();
                    using SqlCommand clear = new SqlCommand(
                        "DELETE FROM OrderProduct WHERE IDOrder = @Id;", connection, transaction);
                    clear.Parameters.AddWithValue("@Id", _orderId);
                    clear.ExecuteNonQuery();
                    SaveItems(connection, transaction, _orderId, pairs);
                    ApplyStockDelta(connection, transaction, pairs, -1);
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            MessageBox.Show("Данные заказа успешно сохранены в базе данных.",
                "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось сохранить заказ.\n" +
                "Проверьте корректность введённых данных и повторите попытку.\n" +
                "Подробности: " + ex.Message,
                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SaveItems(SqlConnection connection, SqlTransaction transaction,
        int orderId, List<KeyValuePair<int, int>> items)
    {
        foreach (KeyValuePair<int, int> item in items)
        {
            using SqlCommand command = new SqlCommand(
                "INSERT INTO OrderProduct (IDOrder, IDProduct, ProductCount) " +
                "VALUES (@OrderId, @ProductId, @Count);", connection, transaction);
            command.Parameters.AddWithValue("@OrderId", orderId);
            command.Parameters.AddWithValue("@ProductId", item.Key);
            command.Parameters.AddWithValue("@Count", item.Value);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Изменяет остаток на складе на sign × количество (-1 списать, +1 вернуть).</summary>
    private static void ApplyStockDelta(SqlConnection connection, SqlTransaction transaction,
        List<KeyValuePair<int, int>> items, int sign)
    {
        foreach (KeyValuePair<int, int> item in items)
        {
            using SqlCommand command = new SqlCommand(
                "UPDATE Product SET QuantityInStock = QuantityInStock + @Delta " +
                "WHERE ProductID = @Id;", connection, transaction);
            command.Parameters.AddWithValue("@Delta", sign * item.Value);
            command.Parameters.AddWithValue("@Id", item.Key);
            command.ExecuteNonQuery();
        }
    }

    private void ShowError(string message, UIElement field)
    {
        MessageBox.Show(message, "Ошибка ввода данных",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        field.Focus();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
