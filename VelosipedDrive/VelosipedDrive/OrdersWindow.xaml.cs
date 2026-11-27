using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Input;

namespace VelosipedDrive;

/// <summary>
/// Окно списка заказов: менеджер просматривает, администратор
/// дополнительно добавляет, редактирует и удаляет заказы.
/// </summary>
public partial class OrdersWindow : Window
{
    public OrdersWindow()
    {
        InitializeComponent();
        txtUser.Text = App.CurrentUserFullName;
        // Оформить новый заказ может клиент (своя покупка) и администратор
        if (App.IsAdmin || App.IsClient)
        {
            btnAdd.Visibility = Visibility.Visible;
        }
        // Удалять заказы может только администратор
        if (App.IsAdmin)
        {
            btnDelete.Visibility = Visibility.Visible;
        }
        else if (App.IsClient)
        {
            txtHint.Text = "Отображаются только ваши заказы.";
        }
        else
        {
            txtHint.Text = "Режим просмотра: изменение заказов доступно только администратору.";
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadOrders();
    }

    /// <summary>Загружает заказы с пунктами выдачи, клиентами, статусами и составом.</summary>
    private void LoadOrders()
    {
        string sql = @"
            SELECT o.IDOrder, o.OrderDate, o.OrderDeliveryDate, p.Address,
                   ISNULL(u.UserSurname + ' ' + u.UserName +
                          ISNULL(' ' + u.UserPatronymic, ''), '') AS UserFullName,
                   o.OrderCode, s.StatusName,
                   STUFF((SELECT ', ' + CAST(op.IDProduct AS varchar(10)) + ', '
                                 + CAST(op.ProductCount AS varchar(10))
                          FROM OrderProduct op
                          WHERE op.IDOrder = o.IDOrder
                          FOR XML PATH('')), 1, 2, '') AS Composition
            FROM [Order] o
            INNER JOIN PickupPoint p ON p.IDPickupPoint = o.IDPickupPoint
            INNER JOIN OrderStatus s ON s.IDStatus = o.IDStatus
            LEFT JOIN [User] u ON u.IDUser = o.IDUser";
        // Клиент видит только свои заказы; менеджер и администратор — все
        if (App.IsClient)
        {
            sql += "\nWHERE o.IDUser = @UserId;";
        }
        else
        {
            sql += ";";
        }
        try
        {
            DataTable orders = new DataTable();
            using SqlConnection connection = new SqlConnection(App.ConnectionString);
            using SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            if (App.IsClient)
            {
                adapter.SelectCommand.Parameters.AddWithValue("@UserId", App.CurrentUserID);
            }
            adapter.Fill(orders);
            listOrders.ItemsSource = orders.DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить список заказов из базы данных.\nПодробности: "
                + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        // orderId = 0 означает добавление нового заказа
        OrderEditWindow editWindow = new OrderEditWindow(0);
        if (editWindow.ShowDialog() == true)
        {
            LoadOrders();
        }
    }

    private void ListOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!App.IsAdmin || listOrders.SelectedItem is not DataRowView selected)
        {
            return;
        }
        int orderId = Convert.ToInt32(selected["IDOrder"]);
        OrderEditWindow editWindow = new OrderEditWindow(orderId);
        if (editWindow.ShowDialog() == true)
        {
            LoadOrders();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (listOrders.SelectedItem is not DataRowView selected)
        {
            MessageBox.Show("Выберите заказ в списке, который необходимо удалить.",
                "Удаление заказа", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        int orderId = Convert.ToInt32(selected["IDOrder"]);
        MessageBoxResult answer = MessageBox.Show(
            "Вы собираетесь безвозвратно удалить заказ №" + orderId + ".\n" +
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
            // Возвращаем товары из состава заказа на склад
            using SqlCommand restore = new SqlCommand(
                "UPDATE p SET p.QuantityInStock = p.QuantityInStock + op.ProductCount " +
                "FROM Product p " +
                "INNER JOIN OrderProduct op ON op.IDProduct = p.ProductID " +
                "WHERE op.IDOrder = @Id;", connection);
            restore.Parameters.AddWithValue("@Id", orderId);
            restore.ExecuteNonQuery();
            // Сначала удаляем состав заказа, затем сам заказ
            using SqlCommand clear = new SqlCommand(
                "DELETE FROM OrderProduct WHERE IDOrder = @Id;", connection);
            clear.Parameters.AddWithValue("@Id", orderId);
            clear.ExecuteNonQuery();
            using SqlCommand delete = new SqlCommand(
                "DELETE FROM [Order] WHERE IDOrder = @Id;", connection);
            delete.Parameters.AddWithValue("@Id", orderId);
            delete.ExecuteNonQuery();
            MessageBox.Show("Заказ №" + orderId + " успешно удалён.",
                "Удаление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadOrders();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось удалить заказ.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
