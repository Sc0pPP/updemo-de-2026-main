using MirIgrushek.Models;
using System.Windows;

namespace MirIgrushek;

/// <summary>
/// Окно добавления нового пользователя (userId = 0) или редактирования
/// существующего. Логин проверяется на уникальность.
/// </summary>
public partial class UserEditWindow : Window
{
    // Точный текст по КОД 09.02.07-5-2026 (дословно)
    private const string MsgLoginTaken = "Пользователь с таким логином уже существует";

    private readonly int _userId;

    public UserEditWindow(int userId)
    {
        InitializeComponent();
        _userId = userId;
        FillRoles();
        if (userId == 0)
        {
            Title = "Добавление пользователя — ООО «МирИгрушек»";
        }
        else
        {
            Title = "Редактирование пользователя — ООО «МирИгрушек»";
            LoadUser();
        }
    }

    /// <summary>Список ролей из справочника Role.</summary>
    private void FillRoles()
    {
        try
        {
            cmbRole.ItemsSource = Database.GetRoles();
            if (cmbRole.Items.Count > 0)
            {
                cmbRole.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить список ролей.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Подгружает данные редактируемого пользователя в поля формы.</summary>
    private void LoadUser()
    {
        try
        {
            User user = Database.GetUsers().Find(u => u.IDUser == _userId);
            if (user == null)
            {
                MessageBox.Show("Пользователь не найден в базе данных.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            txtSurname.Text = user.UserSurname;
            txtName.Text = user.UserName;
            txtPatronymic.Text = user.UserPatronymic;
            txtLogin.Text = user.Login;
            txtPassword.Text = user.Password;
            cmbRole.SelectedValue = user.IDRole;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить данные пользователя.\nПодробности: " + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string surname = txtSurname.Text.Trim();
        string name = txtName.Text.Trim();
        string patronymic = txtPatronymic.Text.Trim();
        string login = txtLogin.Text.Trim();
        string password = txtPassword.Text;
        if (surname.Length == 0 || name.Length == 0)
        {
            ShowError("Укажите фамилию и имя пользователя.", txtSurname);
            return;
        }
        if (login.Length == 0)
        {
            ShowError("Укажите логин пользователя.", txtLogin);
            return;
        }
        if (password.Length == 0)
        {
            ShowError("Укажите пароль пользователя.", txtPassword);
            return;
        }
        if (cmbRole.SelectedValue == null)
        {
            ShowError("Выберите роль из выпадающего списка.", cmbRole);
            return;
        }
        try
        {
            // Логин должен быть уникальным (при редактировании — кроме самого пользователя)
            if (Database.IsLoginTaken(login, _userId))
            {
                ShowError(MsgLoginTaken, txtLogin);
                return;
            }
            User user = new User
            {
                IDUser = _userId,
                IDRole = (int)cmbRole.SelectedValue,
                UserSurname = surname,
                UserName = name,
                UserPatronymic = patronymic,
                Login = login,
                Password = password
            };
            if (_userId == 0)
            {
                user.IDUser = Database.AddUser(user);
            }
            else
            {
                Database.UpdateUser(user);
            }
            MessageBox.Show("Данные пользователя успешно сохранены в базе данных.",
                "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось сохранить пользователя.\nПодробности: " + ex.Message,
                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowError(string message, System.Windows.UIElement field)
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
