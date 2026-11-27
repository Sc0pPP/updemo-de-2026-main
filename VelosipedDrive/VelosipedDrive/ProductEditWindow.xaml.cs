using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VelosipedDrive;

/// <summary>
/// Окно добавления нового товара (productId = 0) или редактирования
/// существующего. Доступно только администратору.
/// </summary>
public partial class ProductEditWindow : Window
{
    private readonly int _productId;
    private string _oldPhoto = "";
    private string _pickedPhotoPath = "";

    public ProductEditWindow(int productId)
    {
        InitializeComponent();
        _productId = productId;
        FillLookups();
        if (productId == 0)
        {
            Title = "Добавление товара — ООО «ВелосипедДрайв»";
            rowId.Visibility = Visibility.Collapsed; // ID при добавлении не отображается
            imgPhoto.Source = App.LoadPhoto(null);
            txtDiscount.Text = "0";
            txtStock.Text = "0";
        }
        else
        {
            Title = "Редактирование товара — ООО «ВелосипедДрайв»";
            LoadProduct();
        }
    }

    /// <summary>Заполняет выпадающие списки справочниками из базы данных.</summary>
    private void FillLookups()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlDataAdapter adapter = new SqlDataAdapter(
                "SELECT IDCategory, CategoryName FROM Category; " +
                "SELECT IDSupplier, SupplierName FROM Supplier; " +
                "SELECT IDUnit, UnitName FROM Unit;", connection);
            DataSet data = new DataSet();
            adapter.Fill(data);
            cmbCategory.ItemsSource = data.Tables[0].DefaultView;
            cmbSupplier.ItemsSource = data.Tables[1].DefaultView;
            cmbUnit.ItemsSource = data.Tables[2].DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить справочники из базы данных.\nПодробности: "
                + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Подгружает данные выбранного товара в поля формы.</summary>
    private void LoadProduct()
    {
        try
        {
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlCommand command = new SqlCommand(@"
                SELECT p.*, m.ManufacturerName
                FROM Product p
                INNER JOIN Manufacturer m ON m.IDManufacturer = p.IDManufacturer
                WHERE p.ProductID = @Id;", connection);
            command.Parameters.AddWithValue("@Id", _productId);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                MessageBox.Show("Товар не найден в базе данных.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            txtId.Text = reader["ProductID"].ToString(); // ID только для чтения
            txtName.Text = reader["ProductName"].ToString();
            cmbCategory.SelectedValue = reader["IDCategory"];
            txtManufacturer.Text = reader["ManufacturerName"].ToString();
            cmbSupplier.SelectedValue = reader["IDSupplier"];
            txtPrice.Text = Convert.ToDecimal(reader["ProductPrice"]).ToString("0.00");
            cmbUnit.SelectedValue = reader["IDUnit"];
            txtStock.Text = reader["QuantityInStock"].ToString();
            txtDiscount.Text = reader["CurrentDiscount"].ToString();
            txtDescription.Text = reader["ProductDescription"].ToString();
            _oldPhoto = reader["ProductPhoto"] as string ?? "";
            imgPhoto.Source = App.LoadPhoto(_oldPhoto);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить данные товара.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPickPhoto_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Выбор фотографии товара",
            Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _pickedPhotoPath = dialog.FileName;
        BitmapImage preview = new BitmapImage();
        preview.BeginInit();
        preview.CacheOption = BitmapCacheOption.OnLoad;
        preview.UriSource = new Uri(_pickedPhotoPath, UriKind.Absolute);
        preview.EndInit();
        imgPhoto.Source = preview;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // ---- Проверка полей формы ----
        string name = txtName.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("Введите наименование товара.", txtName);
            return;
        }
        if (cmbCategory.SelectedValue == null)
        {
            ShowError("Выберите категорию товара из выпадающего списка.", cmbCategory);
            return;
        }
        string manufacturer = txtManufacturer.Text.Trim();
        if (manufacturer.Length == 0)
        {
            ShowError("Введите название производителя.", txtManufacturer);
            return;
        }
        if (cmbSupplier.SelectedValue == null)
        {
            ShowError("Выберите поставщика из выпадающего списка.", cmbSupplier);
            return;
        }
        string priceText = txtPrice.Text.Trim().Replace(".", ",");
        if (!decimal.TryParse(priceText, out decimal price) || price < 0)
        {
            ShowError("Цена указана неверно. Введите неотрицательное число " +
                "(допускаются сотые части, например 1999,99).", txtPrice);
            return;
        }
        if (cmbUnit.SelectedValue == null)
        {
            ShowError("Выберите единицу измерения из выпадающего списка.", cmbUnit);
            return;
        }
        if (!int.TryParse(txtStock.Text.Trim(), out int stock) || stock < 0)
        {
            ShowError("Количество на складе указано неверно. Введите целое неотрицательное число.", txtStock);
            return;
        }
        if (!int.TryParse(txtDiscount.Text.Trim(), out int discount) || discount < 0 || discount > 100)
        {
            ShowError("Скидка указана неверно. Введите целое число от 0 до 100.", txtDiscount);
            return;
        }
        try
        {
            // Новое фото сохраняется в папку приложения (не более 300x200)
            string newPhoto = _pickedPhotoPath.Length > 0 ? App.SavePhoto(_pickedPhotoPath) : _oldPhoto;
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            connection.Open();
            if (_productId == 0)
            {
                // ID товара вычисляется автоматически (+1 к имеющемуся в БД)
                using SqlCommand command = new SqlCommand(@"
                    INSERT INTO Product (ProductName, ProductDescription, ProductPhoto,
                        ProductPrice, CurrentDiscount, QuantityInStock,
                        IDUnit, IDCategory, IDSupplier, IDManufacturer)
                    VALUES (@Name, @Description, @Photo, @Price, @Discount, @Stock,
                        @Unit, @Category, @Supplier,
                        (SELECT IDManufacturer FROM Manufacturer WHERE ManufacturerName = @Manufacturer));
                    IF @@ROWCOUNT = 0 RAISERROR(N'Ошибка', 16, 1);
                    IF NOT EXISTS (SELECT 1 FROM Manufacturer WHERE ManufacturerName = @Manufacturer)
                        INSERT INTO Manufacturer (ManufacturerName) VALUES (@Manufacturer);
                    ", connection);
                FillParameters(command, name, price, discount, stock, newPhoto);
                command.ExecuteNonQuery();
            }
            else
            {
                // Производителя при необходимости добавляем в справочник
                using SqlCommand ensureManufacturer = new SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM Manufacturer WHERE ManufacturerName = @Name) " +
                    "INSERT INTO Manufacturer (ManufacturerName) VALUES (@Name);", connection);
                ensureManufacturer.Parameters.AddWithValue("@Name", manufacturer);
                ensureManufacturer.ExecuteNonQuery();
                using SqlCommand command = new SqlCommand(@"
                    UPDATE Product
                    SET ProductName = @Name, ProductDescription = @Description, ProductPhoto = @Photo,
                        ProductPrice = @Price, CurrentDiscount = @Discount, QuantityInStock = @Stock,
                        IDUnit = @Unit, IDCategory = @Category, IDSupplier = @Supplier,
                        IDManufacturer = (SELECT IDManufacturer FROM Manufacturer WHERE ManufacturerName = @Manufacturer)
                    WHERE ProductID = @Id;", connection);
                command.Parameters.AddWithValue("@Id", _productId);
                FillParameters(command, name, price, discount, stock, newPhoto);
                command.ExecuteNonQuery();
            }
            // При замене изображения старое фото удаляется из папки приложения
            if (newPhoto != _oldPhoto)
            {
                App.DeletePhoto(_oldPhoto);
            }
            MessageBox.Show("Данные товара успешно сохранены в базе данных.",
                "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось сохранить товар.\n" +
                "Проверьте корректность введённых данных и повторите попытку.\n" +
                "Подробности: " + ex.Message,
                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FillParameters(SqlCommand command, string name, decimal price,
        int discount, int stock, string photo)
    {
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description",
            string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text.Trim());
        command.Parameters.AddWithValue("@Photo", string.IsNullOrWhiteSpace(photo) ? (object)DBNull.Value : photo);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@Discount", discount);
        command.Parameters.AddWithValue("@Stock", stock);
        command.Parameters.AddWithValue("@Unit", cmbUnit.SelectedValue);
        command.Parameters.AddWithValue("@Category", cmbCategory.SelectedValue);
        command.Parameters.AddWithValue("@Supplier", cmbSupplier.SelectedValue);
        command.Parameters.AddWithValue("@Manufacturer", txtManufacturer.Text.Trim());
    }

    private void ShowError(string message, System.Windows.UIElement field)
    {
        MessageBox.Show(message + "\nИсправьте выделенное поле и повторите сохранение.",
            "Ошибка ввода данных", MessageBoxButton.OK, MessageBoxImage.Warning);
        field.Focus();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
