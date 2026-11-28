namespace MirIgrushek.Models;

/// <summary>Пользователь системы (таблица [User]).</summary>
public class User
{
    public int IDUser { get; set; }
    public int IDRole { get; set; }
    public string UserSurname { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserPatronymic { get; set; } = "";   // в БД может быть NULL
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>True — учётная запись заблокирована (после 3 неудачных входов).</summary>
    public bool IsBlocked { get; set; }

    /// <summary>Статус для отображения: «Активен» или «Заблокирован».</summary>
    public string StatusName => IsBlocked ? "Заблокирован" : "Активен";

    // Название роли (заполняется JOIN-запросом — для отображения)
    public string RoleName { get; set; } = "";

    /// <summary>ФИО целиком: фамилия, имя и (если есть) отчество.</summary>
    public string FullName =>
        UserSurname + " " + UserName +
        (string.IsNullOrWhiteSpace(UserPatronymic) ? "" : " " + UserPatronymic);
}
