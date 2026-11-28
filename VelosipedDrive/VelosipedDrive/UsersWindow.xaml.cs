using VelosipedDrive.Models;
using System.Windows;

namespace VelosipedDrive;

/// <summary>
/// Окно управления пользователями: список всех учётных записей
/// (сущности из Models через Database.GetUsers), добавление, редактирование,
/// блокировка/разблокировка. Доступно только администратору.
/// </summary>
public partial class UsersWindow : Window
{
    public UsersWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadUsers();
    }

    /// <summary>Загружает всех пользователей из базы данных.</summary>
    private void LoadUsers()
    {
        try
        {
            gridUsers.ItemsSource = Database.GetUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить список пользователей из базы данных.\n" +
                "Подробности: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAddUser_Click(object sender, RoutedEventArgs e)
    {
        if (new UserEditWindow(0).ShowDialog() == true)
        {
            LoadUsers();
        }
    }

    private void BtnEditUser_Click(object sender, RoutedEventArgs e)
    {
        if (gridUsers.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите пользователя в списке, которого необходимо изменить.",
                "Изменение пользователя", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (new UserEditWindow(user.IDUser).ShowDialog() == true)
        {
            LoadUsers();
        }
    }

    /// <summary>Переключает блокировку учётной записи: заблокировать или разблокировать.</summary>
    private void BtnToggleBlock_Click(object sender, RoutedEventArgs e)
    {
        if (gridUsers.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите пользователя в списке.",
                "Блокировка", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (user.IDUser == App.CurrentUserID)
        {
            MessageBox.Show("Нельзя заблокировать собственную учётную запись.",
                "Блокировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        bool block = !user.IsBlocked;
        try
        {
            Database.SetUserBlocked(user.IDUser, block);
            LoadUsers();
            MessageBox.Show(block ? "Учётная запись пользователя заблокирована."
                : "Учётная запись пользователя разблокирована.",
                "Блокировка", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось изменить блокировку.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
