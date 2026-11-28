using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MirIgrushek;

/// <summary>
/// Окно списка товаров. Гость и клиент видят товары без фильтрации, сортировки
/// и поиска; менеджер получает поиск/сортировку/фильтрацию и просмотр заказов;
/// администратор — полное управление товарами и заказами.
/// </summary>
public partial class CatalogWindow : Window
{
    // Все товары загружаются из базы один раз, поиск/сортировка/фильтр — в памяти,
    // поэтому всё работает в реальном времени.
    private DataTable _allProducts = new DataTable();

    public CatalogWindow()
    {
        InitializeComponent();
        txtUser.Text = App.CurrentUserFullName;
        // Кнопка «Заказы» видна авторизованным: у клиента — свои заказы,
        // у менеджера и администратора — все; гость не идентифицирован — без заказов
        btnOrders.Visibility = App.IsGuest ? Visibility.Collapsed : Visibility.Visible;
        if (App.CanUseTools)
        {
            panelTools.Visibility = Visibility.Visible;
            FillFilter();
        }
        else
        {
            txtViewOnly.Visibility = Visibility.Visible;
        }
        if (App.IsAdmin)
        {
            btnAdd.Visibility = Visibility.Visible;
            btnDelete.Visibility = Visibility.Visible;
            btnUsers.Visibility = Visibility.Visible;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProducts();
    }

    /// <summary>Загружает все товары из базы данных вместе со справочными названиями.</summary>
    private void LoadProducts()
    {
        string sql = @"
            SELECT p.ProductID, p.ProductName, p.ProductDescription, p.ProductPhoto,
                   p.ProductPrice, p.CurrentDiscount, p.QuantityInStock,
                   p.IDCategory, c.CategoryName, p.IDSupplier, s.SupplierName,
                   p.IDManufacturer, m.ManufacturerName, p.IDUnit, u.UnitName
            FROM Product p
            INNER JOIN Category c     ON c.IDCategory = p.IDCategory
            INNER JOIN Supplier s     ON s.IDSupplier = p.IDSupplier
            INNER JOIN Manufacturer m ON m.IDManufacturer = p.IDManufacturer
            INNER JOIN Unit u         ON u.IDUnit = p.IDUnit;";
        try
        {
            _allProducts = new DataTable();
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            adapter.Fill(_allProducts);
            // Вычисляемые колонки: итоговая цена, фото (или заглушка), признак большой скидки
            _allProducts.Columns.Add("FinalPrice", typeof(decimal));
            _allProducts.Columns.Add("PhotoImage", typeof(System.Windows.Media.ImageSource));
            _allProducts.Columns.Add("IsBigDiscount", typeof(bool));
            foreach (DataRow row in _allProducts.Rows)
            {
                decimal price = Convert.ToDecimal(row["ProductPrice"]);
                int discount = Convert.ToInt32(row["CurrentDiscount"]);
                row["FinalPrice"] = price * (1 - discount / 100m);
                row["PhotoImage"] = App.LoadPhoto(row["ProductPhoto"] as string);
                row["IsBigDiscount"] = discount > App.BigDiscountPercent;
            }
            ApplyView();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить список товаров из базы данных.\n" +
                "Проверьте подключение и повторите попытку.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Применяет фильтр, поиск и сортировку в реальном времени.</summary>
    private void ApplyView()
    {
        // Событие выбора сортировки приходит ещё при разборе XAML-разметки,
        // когда элементы панели (txtSearch и др.) не созданы — ждём загрузки окна
        if (!IsLoaded)
        {
            return;
        }
        IEnumerable<DataRow> rows = _allProducts.AsEnumerable();
        rows = ApplyFilter(rows);
        string query = txtSearch.Text.Trim();
        if (query.Length > 0)
        {
            // Поиск по всем текстовым полям, не зависящий от регистра
            rows = rows.Where(r =>
                r["ProductName"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r["ProductDescription"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r["CategoryName"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r["ManufacturerName"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r["SupplierName"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r["UnitName"].ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        rows = ApplySort(rows);
        DataTable result = rows.Any() ? rows.CopyToDataTable() : _allProducts.Clone();
        listProducts.ItemsSource = result.DefaultView;
    }

    /// <summary>Фильтр по поставщику (первый элемент списка сбрасывает фильтр).</summary>
    private IEnumerable<DataRow> ApplyFilter(IEnumerable<DataRow> rows)
    {
        if (cmbFilter.SelectedItem is DataRowView supplier &&
            Convert.ToInt32(supplier["IDSupplier"]) > 0)
        {
            int supplierId = Convert.ToInt32(supplier["IDSupplier"]);
            return rows.Where(r => Convert.ToInt32(r["IDSupplier"]) == supplierId);
        }
        return rows;
    }


    /// <summary>Сортировка по выбранному полю и направлению.</summary>
    private IEnumerable<DataRow> ApplySort(IEnumerable<DataRow> rows)
    {
        switch (cmbSort.SelectedIndex)
        {
            case 1:
                return rows.OrderBy(r => Convert.ToDecimal(r["ProductPrice"]));
            case 2:
                return rows.OrderByDescending(r => Convert.ToDecimal(r["ProductPrice"]));
            case 3:
                return rows.OrderBy(r => Convert.ToInt32(r["QuantityInStock"]));
            case 4:
                return rows.OrderByDescending(r => Convert.ToInt32(r["QuantityInStock"]));
            default:
                return rows;
        }
    }


    /// <summary>Заполняет выпадающий список фильтра.</summary>
    private void FillFilter()
    {
        // Первый элемент «Все поставщики» сбрасывает настройки фильтра
        DataTable suppliers = new DataTable();
        using (SqlConnection connection = new SqlConnection(App.ConnectionString))
        using (SqlDataAdapter adapter = new SqlDataAdapter(
            "SELECT 0 AS IDSupplier, N'Все поставщики' AS SupplierName " +
            "UNION ALL SELECT IDSupplier, SupplierName FROM Supplier;", connection))
        {
            adapter.Fill(suppliers);
        }
        cmbFilter.ItemsSource = suppliers.DefaultView;
        cmbFilter.DisplayMemberPath = "SupplierName";
        cmbFilter.SelectedIndex = 0;

    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyView();
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyView();
    }

    private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyView();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnOrders_Click(object sender, RoutedEventArgs e)
    {
        OrdersWindow ordersWindow = new OrdersWindow();
        ordersWindow.ShowDialog();
    }

    /// <summary>Управление пользователями: список, добавление, блокировка (администратор).</summary>
    private void BtnUsers_Click(object sender, RoutedEventArgs e)
    {
        UsersWindow usersWindow = new UsersWindow();
        usersWindow.ShowDialog();
    }

    /// <summary>Проверяет, что окно редактирования товара ещё не открыто:
    /// одновременно допускается только одно такое окно.</summary>
    private bool EditWindowAlreadyOpen()
    {
        bool open = Application.Current.Windows.OfType<ProductEditWindow>().Any();
        if (open)
        {
            MessageBox.Show("Окно добавления/редактирования товара уже открыто.\n" +
                "Завершите работу с текущим товаром, чтобы открыть другое окно.",
                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return open;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (EditWindowAlreadyOpen())
        {
            return;
        }
        // productId = 0 означает добавление нового товара
        ProductEditWindow editWindow = new ProductEditWindow(0);
        if (editWindow.ShowDialog() == true)
        {
            LoadProducts();
        }
    }

    private void ListProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!App.IsAdmin || listProducts.SelectedItem is not DataRowView selected)
        {
            return;
        }
        if (EditWindowAlreadyOpen())
        {
            return;
        }
        int productId = Convert.ToInt32(selected["ProductID"]);
        ProductEditWindow editWindow = new ProductEditWindow(productId);
        if (editWindow.ShowDialog() == true)
        {
            LoadProducts();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (listProducts.SelectedItem is not DataRowView selected)
        {
            MessageBox.Show("Выберите товар в списке, который необходимо удалить.",
                "Удаление товара", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        int productId = Convert.ToInt32(selected["ProductID"]);
        string productName = selected["ProductName"].ToString();
        MessageBoxResult answer = MessageBox.Show(
            "Вы собираетесь безвозвратно удалить товар:\n«" + productName + "».\n" +
            "Это действие нельзя отменить. Продолжить?",
            "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            connection.Open();
            // Товар, который присутствует в заказе, удалить нельзя
            using SqlCommand check = new SqlCommand(
                "SELECT COUNT(*) FROM OrderProduct WHERE IDProduct = @Id;", connection);
            check.Parameters.AddWithValue("@Id", productId);
            if ((int)check.ExecuteScalar() > 0)
            {
                MessageBox.Show("Товар «" + productName +
                    "» присутствует хотя бы в одном заказе и не может быть удалён.\n" +
                    "Чтобы удалить товар, сначала удалите связанные с ним заказы.",
                    "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using SqlCommand delete = new SqlCommand(
                "DELETE FROM Product WHERE ProductID = @Id;", connection);
            delete.Parameters.AddWithValue("@Id", productId);
            delete.ExecuteNonQuery();
            App.DeletePhoto(selected["ProductPhoto"] as string);
            MessageBox.Show("Товар «" + productName + "» успешно удалён.",
                "Удаление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось удалить товар.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
