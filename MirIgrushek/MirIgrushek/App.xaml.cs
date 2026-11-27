using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MirIgrushek;

/// <summary>
/// Точка входа приложения. Здесь же собраны общие настройки:
/// строка подключения, данные текущего пользователя, цвета стиля
/// и работа с фотографиями товаров.
/// </summary>
public partial class App : Application
{
    /// <summary>Строка подключения к MS SQL Server (при необходимости измените Data Source).</summary>
    public const string ConnectionString =
        "Data Source=(local);Initial Catalog=MirIgrushekDB;Integrated Security=True";

    // ---- Текущий пользователь: пустая роль означает режим гостя ----
    public static string CurrentUserFullName = "";
    public static string CurrentRole = "";
    /// <summary>ID пользователя в БД; 0 — гость (заказы без привязки).</summary>
    public static int CurrentUserID = 0;

    public static bool IsGuest => CurrentRole == "";
    public static bool IsAdmin => CurrentRole == "Администратор";
    public static bool IsClient => CurrentRole == "Авторизированный клиент";
    public static bool CanUseTools => CurrentRole == "Менеджер" || CurrentRole == "Администратор";

    // ---- Цвета по руководству по стилю ----
    public static readonly SolidColorBrush AdditionalBackground = MakeBrush("#F5DEB3");
    public static readonly SolidColorBrush AccentBackground = MakeBrush("#DEB887");
    public static readonly SolidColorBrush BigDiscountBackground = MakeBrush("#FFDEAD");
    public static readonly SolidColorBrush OutOfStockBackground = MakeBrush("#ADD8E6");

    /// <summary>Порог скидки для усиленной подсветки строки (по руководству по стилю).</summary>
    public const int BigDiscountPercent = 17;

    private static SolidColorBrush MakeBrush(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Глобальный обработчик: приложение не завершается аварийно
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Произошла непредвиденная ошибка: " + e.Exception.Message +
            "\nПриложение продолжит работу. Повторите действие или обратитесь к администратору.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    // =================== Работа с фотографиями товаров ===================

    /// <summary>
    /// Загружает фото товара из папки images рядом с приложением;
    /// если файла нет, возвращает картинку-заглушку picture.png.
    /// </summary>
    public static ImageSource LoadPhoto(string relativePath)
    {
        string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
        string fullPath = Path.Combine(imagesDir, relativePath ?? "");
        if (string.IsNullOrWhiteSpace(relativePath) || !File.Exists(fullPath))
        {
            fullPath = Path.Combine(imagesDir, "picture.png");
        }
        if (!File.Exists(fullPath))
        {
            return null;
        }
        BitmapImage image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(fullPath, UriKind.Absolute);
        image.EndInit();
        return image;
    }

    /// <summary>
    /// Сохраняет выбранное администратором фото в папку images/Products,
    /// уменьшая его до размеров не более 300x200 пикселей.
    /// Возвращает относительный путь для записи в базу данных.
    /// </summary>
    public static string SavePhoto(string sourcePath)
    {
        string productsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "Products");
        Directory.CreateDirectory(productsDir);
        BitmapImage original = new BitmapImage(new Uri(sourcePath, UriKind.Absolute));
        double scale = Math.Min(300.0 / original.PixelWidth, 200.0 / original.PixelHeight);
        scale = Math.Min(scale, 1.0);
        TransformedBitmap resized = new TransformedBitmap(original, new ScaleTransform(scale, scale));
        string fileName = Guid.NewGuid().ToString("N") + ".JPG";
        string fullPath = Path.Combine(productsDir, fileName);
        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        encoder.QualityLevel = 90;
        encoder.Frames.Add(BitmapFrame.Create(resized));
        using (FileStream stream = new FileStream(fullPath, FileMode.Create))
        {
            encoder.Save(stream);
        }
        return "Products/" + fileName;
    }

    /// <summary>Удаляет старое фото товара из папки приложения при его замене.</summary>
    public static void DeletePhoto(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("Products/"))
        {
            return;
        }
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
