using MirIgrushek.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MirIgrushek;

/// <summary>
/// Окно входа — первое окно, которое видит пользователь.
/// Проверяет логин и пароль по базе данных или открывает просмотр как гостю.
/// После неудачной попытки показывает капчу-пазл; после 3 неудачных попыток
/// подряд учётная запись блокируется (IsBlocked в таблице User).
/// </summary>
public partial class MainWindow : Window
{
    // ---- Точные тексты сообщений (КОД 09.02.07-5-2026, дословно) ----
    private const string MsgBadLogin = "Вы ввели неверный логин или пароль. Пожалуйста проверьте ещё раз введенные данные";
    private const string MsgSuccess = "Вы успешно авторизовались";
    private const string MsgBlocked = "Вы заблокированы. Обратитесь к администратору";

    /// <summary>Счётчик неудачных попыток по логину (неверный пароль ИЛИ несобранный пазл).</summary>
    private readonly Dictionary<string, int> _failedAttempts = new Dictionary<string, int>();

    // ---- Состояние капчи-пазла ----
    private const int GridSize = 3;                                  // пазл 3×3
    private readonly List<Border> _cells = new List<Border>();       // ячейки сетки
    private readonly List<ImageSource> _fragments = new List<ImageSource>(); // эталонные фрагменты
    private readonly int[] _puzzleOrder = new int[GridSize * GridSize]; // текущая перестановка
    private int _firstCell = -1;                                     // выбранная первая ячейка
    private bool _captchaSolved;                                     // пазл собран

    private static readonly SolidColorBrush CellBorder =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEB887"));
    private static readonly SolidColorBrush CellSelected =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8860B"));

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
        User user;
        try
        {
            // 1. Блокировка проверяется ДО проверки пароля
            User existing = Database.GetUserByLogin(login);
            if (existing != null && existing.IsBlocked)
            {
                MessageBox.Show(MsgBlocked, "Ошибка авторизации",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // 2. Капча показана — вход только с собранным пазлом
            if (captchaPanel.Visibility == Visibility.Visible && !_captchaSolved)
            {
                MessageBox.Show("Соберите контрольный пазл, чтобы продолжить.",
                    "Контрольная проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                RegisterFailure(login, existing);
                return;
            }
            // 3. Проверка логина и пароля
            user = Database.LoginUser(login, password);
            if (user == null)
            {
                MessageBox.Show(MsgBadLogin, "Ошибка авторизации",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                RegisterFailure(login, existing);
                return;
            }
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
        // Успех: сброс счётчика неудач, приветственное сообщение, переход в каталог
        _failedAttempts.Remove(login);
        App.CurrentUserFullName = user.FullName;
        App.CurrentRole = user.RoleName;
        App.CurrentUserID = user.IDUser;
        MessageBox.Show(MsgSuccess, "Авторизация",
            MessageBoxButton.OK, MessageBoxImage.Information);
        OpenCatalog();
    }

    /// <summary>Регистрирует неудачную попытку; после третьей подряд — блокировка.</summary>
    private void RegisterFailure(string login, User existing)
    {
        _failedAttempts.TryGetValue(login, out int count);
        count++;
        _failedAttempts[login] = count;
        if (count >= 3 && existing != null)
        {
            Database.SetUserBlocked(existing.IDUser, true);
            MessageBox.Show(MsgBlocked, "Ошибка авторизации",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            ShowCaptcha();
        }
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

    // =================== Капча-пазл 3×3 ===================

    /// <summary>Показывает капчу: при первом показе режет картинку на фрагменты,
    /// при каждом показе — новое перемешивание.</summary>
    private void ShowCaptcha()
    {
        if (_fragments.Count == 0)
        {
            CutPuzzle();
        }
        captchaPanel.Visibility = Visibility.Visible;
        // Окно вырастает под панель — возвращаем в центр экрана
        Height = 820;
        Top = (SystemParameters.WorkArea.Height - Height) / 2;
        Left = (SystemParameters.WorkArea.Width - Width) / 2;
        ShufflePuzzle();
    }

    /// <summary>Режет исходную картинку на 3×3 фрагментов (эталонный порядок) и строит сетку.</summary>
    private void CutPuzzle()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "picture.png");
        BitmapImage source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.UriSource = new Uri(path, UriKind.Absolute);
        source.EndInit();
        int w = source.PixelWidth / GridSize;
        int h = source.PixelHeight / GridSize;
        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                _fragments.Add(new CroppedBitmap(source,
                    new Int32Rect(col * w, row * h, w, h)));
            }
        }
        for (int i = 0; i < GridSize; i++)
        {
            puzzleGrid.RowDefinitions.Add(new RowDefinition());
            puzzleGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        for (int i = 0; i < GridSize * GridSize; i++)
        {
            Border cell = new Border
            {
                BorderBrush = CellBorder,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = i,
                Child = new Image { Source = _fragments[i], Stretch = Stretch.Fill }
            };
            cell.MouseLeftButtonDown += Cell_Click;
            Grid.SetRow(cell, i / GridSize);
            Grid.SetColumn(cell, i % GridSize);
            puzzleGrid.Children.Add(cell);
            _cells.Add(cell);
        }
    }

    /// <summary>Перемешивает фрагменты (Фишер-Йетс); сразу собранный вариант исключён.</summary>
    private void ShufflePuzzle()
    {
        Random random = new Random();
        do
        {
            for (int i = 0; i < GridSize * GridSize; i++)
            {
                _puzzleOrder[i] = i;
            }
            for (int i = GridSize * GridSize - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (_puzzleOrder[i], _puzzleOrder[j]) = (_puzzleOrder[j], _puzzleOrder[i]);
            }
        } while (IsSolved());
        _firstCell = -1;
        _captchaSolved = false;
        RedrawPuzzle();
    }

    /// <summary>Клик по ячейке: первый клик выбирает, второй — меняет местами.</summary>
    private void Cell_Click(object sender, MouseButtonEventArgs e)
    {
        int cell = (int)((Border)sender).Tag;
        if (_firstCell < 0)
        {
            _firstCell = cell;
            _cells[cell].BorderBrush = CellSelected;
            _cells[cell].BorderThickness = new Thickness(2);
        }
        else if (_firstCell == cell)
        {
            _firstCell = -1; // повторный клик по той же ячейке снимает выбор
            _cells[cell].BorderBrush = CellBorder;
            _cells[cell].BorderThickness = new Thickness(1);
        }
        else
        {
            (_puzzleOrder[_firstCell], _puzzleOrder[cell]) =
                (_puzzleOrder[cell], _puzzleOrder[_firstCell]);
            _firstCell = -1;
            RedrawPuzzle();
            _captchaSolved = IsSolved();
        }
    }

    /// <summary>Перерисовывает ячейки по текущей перестановке.</summary>
    private void RedrawPuzzle()
    {
        for (int i = 0; i < GridSize * GridSize; i++)
        {
            ((Image)_cells[i].Child).Source = _fragments[_puzzleOrder[i]];
            _cells[i].BorderBrush = CellBorder;
            _cells[i].BorderThickness = new Thickness(1);
        }
    }

    /// <summary>True, если текущая перестановка совпадает с эталонной.</summary>
    private bool IsSolved()
    {
        for (int i = 0; i < GridSize * GridSize; i++)
        {
            if (_puzzleOrder[i] != i)
            {
                return false;
            }
        }
        return true;
    }

    private void BtnPuzzleCheck_Click(object sender, RoutedEventArgs e)
    {
        _captchaSolved = IsSolved();
        if (_captchaSolved)
        {
            MessageBox.Show("Пазл собран верно.", "Контрольная проверка",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show("Пазл собран неверно. Продолжайте или нажмите «Обновить».",
            "Контрольная проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
        try
        {
            string login = txtLogin.Text.Trim();
            RegisterFailure(login, Database.GetUserByLogin(login));
        }
        catch (Exception)
        {
            ShowCaptcha();
        }
    }

    private void BtnPuzzleRefresh_Click(object sender, RoutedEventArgs e)
    {
        ShufflePuzzle();
    }
}
