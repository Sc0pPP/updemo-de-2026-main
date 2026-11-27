using VelosipedDrive.Models;
using System.Windows;

namespace VelosipedDrive;

/// <summary>
/// Окно входа — первое окно, которое видит пользователь.
/// Проверяет логин и пароль по базе данных или открывает просмотр как гостю.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        txtLogin.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        string login = txtLogin.Text.Trim();
        string password = txtPassword.Password;
        if (login.Length == 0 || password.Length == 0)
        {
            MessageBox.Show("Введите логин и пароль. Оба поля обязательны для входа.",
                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            // Поиск пользователя с указанным логином и паролем в базе данных
            User user = Database.LoginUser(login, password);
            if (user == null)
            {
                MessageBox.Show("Пользователь с таким логином и паролем не найден.\n" +
                    "Проверьте правильность ввода учётных данных и повторите попытку.",
                    "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                return;
            }
            App.CurrentUserFullName = user.FullName;
            App.CurrentRole = user.RoleName;
            App.CurrentUserID = user.IDUser;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось подключиться к базе данных.\n" +
                "Проверьте, что сервер MS SQL Server запущен, база данных создана " +
                "скриптом db/schema.sql, а строка подключения в App.xaml.cs указана верно.\n" +
                "Подробности: " + ex.Message,
                "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        OpenCatalog();
    }

    private void BtnGuest_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUserFullName = "Гость";
        App.CurrentRole = "";
        App.CurrentUserID = 0;
        OpenCatalog();
    }

    private void OpenCatalog()
    {
        CatalogWindow catalog = new CatalogWindow();
        catalog.Closed += (s, args) =>
        {
            // Выход из учётной записи: сброс пользователя и возврат к окну входа
            App.CurrentUserFullName = "";
            App.CurrentRole = "";
            App.CurrentUserID = 0;
            txtPassword.Clear();
            Show();
            Activate();
        };
        catalog.Show();
        Hide();
    }
}
