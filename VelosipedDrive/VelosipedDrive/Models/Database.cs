using System.Data.SqlClient;

namespace VelosipedDrive.Models;

/// <summary>
/// Слой доступа к данным на чистом ADO.NET (System.Data.SqlClient).
/// Все запросы параметризованы, соединение открывается на время одного
/// вызова и закрывается автоматически (using). Методы работают
/// с объектами-сущностями из папки Models.
/// </summary>
public static class Database
{
    // ===================== Базовые помощники =====================

    private static SqlConnection NewConnection() =>
        new SqlConnection(App.ConnectionString);

    private static SqlParameter P(string name, object value) =>
        new SqlParameter(name, value ?? DBNull.Value);

    /// <summary>Выполняет SELECT и отображает строки в список сущностей.</summary>
    private static List<T> Query<T>(string sql, Func<SqlDataReader, T> map,
                                    params SqlParameter[] parameters)
    {
        List<T> list = new List<T>();
        using SqlConnection connection = NewConnection();
        using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(map(reader));
        }
        return list;
    }

    /// <summary>Выполняет INSERT/UPDATE/DELETE, возвращает число затронутых строк.</summary>
    private static int NonQuery(string sql, params SqlParameter[] parameters)
    {
        using SqlConnection connection = NewConnection();
        using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        connection.Open();
        return command.ExecuteNonQuery();
    }

    /// <summary>Выполняет запрос, возвращающий одно значение (COUNT, ID и т.п.).</summary>
    private static object Scalar(string sql, params SqlParameter[] parameters)
    {
        using SqlConnection connection = NewConnection();
        using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        connection.Open();
        return command.ExecuteScalar();
    }

    // ===================== Авторизация и пользователи =====================

    /// <summary>Ищет пользователя с такой парой логин/пароль; null — не найден.</summary>
    public static User LoginUser(string login, string password)
    {
        const string sql =
            "SELECT u.IDUser, u.IDRole, u.UserSurname, u.UserName, u.UserPatronymic, " +
            "u.Login, u.[Password], u.IsBlocked, r.RoleName " +
            "FROM [User] u INNER JOIN Role r ON r.IDRole = u.IDRole " +
            "WHERE u.Login = @Login AND u.[Password] = @Password;";
        List<User> found = Query(sql, MapUser, P("@Login", login), P("@Password", password));
        return found.Count > 0 ? found[0] : null;
    }

    /// <summary>Все пользователи с названиями ролей.</summary>
    public static List<User> GetUsers() => Query(
        "SELECT u.IDUser, u.IDRole, u.UserSurname, u.UserName, u.UserPatronymic, " +
        "u.Login, u.[Password], u.IsBlocked, r.RoleName " +
        "FROM [User] u INNER JOIN Role r ON r.IDRole = u.IDRole " +
        "ORDER BY u.IDUser;",
        MapUser);

    private static User MapUser(SqlDataReader r) => new User
    {
        IDUser = (int)r["IDUser"],
        IDRole = (int)r["IDRole"],
        UserSurname = (string)r["UserSurname"],
        UserName = (string)r["UserName"],
        UserPatronymic = r["UserPatronymic"] as string ?? "",
        Login = (string)r["Login"],
        Password = (string)r["Password"],
        RoleName = (string)r["RoleName"],
        IsBlocked = Convert.ToBoolean(r["IsBlocked"])
    };

    /// <summary>Пользователь по логину без проверки пароля; null — не найден.</summary>
    public static User GetUserByLogin(string login)
    {
        const string sql =
            "SELECT u.IDUser, u.IDRole, u.UserSurname, u.UserName, u.UserPatronymic, " +
            "u.Login, u.[Password], u.IsBlocked, r.RoleName " +
            "FROM [User] u INNER JOIN Role r ON r.IDRole = u.IDRole " +
            "WHERE u.Login = @Login;";
        List<User> found = Query(sql, MapUser, P("@Login", login));
        return found.Count > 0 ? found[0] : null;
    }

    /// <summary>True, если логин уже занят другим пользователем.</summary>
    public static bool IsLoginTaken(string login, int excludeUserId) =>
        (int)Scalar("SELECT COUNT(*) FROM [User] WHERE Login = @Login AND IDUser <> @Id;",
                    P("@Login", login), P("@Id", excludeUserId)) > 0;

    /// <summary>Добавляет пользователя, возвращает присвоенный IDUser.</summary>
    public static int AddUser(User user) => Convert.ToInt32(Scalar(
        "INSERT INTO [User] (IDRole, UserSurname, UserName, UserPatronymic, Login, [Password]) " +
        "VALUES (@Role, @Surname, @Name, @Patronymic, @Login, @Password); SELECT SCOPE_IDENTITY();",
        P("@Role", user.IDRole), P("@Surname", user.UserSurname), P("@Name", user.UserName),
        P("@Patronymic", string.IsNullOrWhiteSpace(user.UserPatronymic) ? null : user.UserPatronymic),
        P("@Login", user.Login), P("@Password", user.Password)));

    /// <summary>Обновляет данные пользователя по IDUser.</summary>
    public static bool UpdateUser(User user) => NonQuery(
        "UPDATE [User] SET IDRole = @Role, UserSurname = @Surname, UserName = @Name, " +
        "UserPatronymic = @Patronymic, Login = @Login, [Password] = @Password WHERE IDUser = @Id;",
        P("@Role", user.IDRole), P("@Surname", user.UserSurname), P("@Name", user.UserName),
        P("@Patronymic", string.IsNullOrWhiteSpace(user.UserPatronymic) ? null : user.UserPatronymic),
        P("@Login", user.Login), P("@Password", user.Password), P("@Id", user.IDUser)) > 0;

    /// <summary>Блокирует или разблокирует учётную запись пользователя.</summary>
    public static bool SetUserBlocked(int userId, bool blocked) => NonQuery(
        "UPDATE [User] SET IsBlocked = @Blocked WHERE IDUser = @Id;",
        P("@Blocked", blocked), P("@Id", userId)) > 0;

    // ===================== Справочники =====================

    public static List<Role> GetRoles() => Query(
        "SELECT IDRole, RoleName FROM Role ORDER BY IDRole;",
        r => new Role { IDRole = (int)r["IDRole"], RoleName = (string)r["RoleName"] });

    public static List<OrderStatus> GetOrderStatuses() => Query(
        "SELECT IDStatus, StatusName FROM OrderStatus ORDER BY IDStatus;",
        r => new OrderStatus { IDStatus = (int)r["IDStatus"], StatusName = (string)r["StatusName"] });

    public static List<Unit> GetUnits() => Query(
        "SELECT IDUnit, UnitName FROM Unit ORDER BY IDUnit;",
        r => new Unit { IDUnit = (int)r["IDUnit"], UnitName = (string)r["UnitName"] });

    public static List<Category> GetCategories() => Query(
        "SELECT IDCategory, CategoryName FROM Category ORDER BY IDCategory;",
        r => new Category { IDCategory = (int)r["IDCategory"], CategoryName = (string)r["CategoryName"] });

    public static List<Manufacturer> GetManufacturers() => Query(
        "SELECT IDManufacturer, ManufacturerName FROM Manufacturer ORDER BY IDManufacturer;",
        r => new Manufacturer { IDManufacturer = (int)r["IDManufacturer"], ManufacturerName = (string)r["ManufacturerName"] });

    public static List<Supplier> GetSuppliers() => Query(
        "SELECT IDSupplier, SupplierName FROM Supplier ORDER BY IDSupplier;",
        r => new Supplier { IDSupplier = (int)r["IDSupplier"], SupplierName = (string)r["SupplierName"] });

    public static List<PickupPoint> GetPickupPoints() => Query(
        "SELECT IDPickupPoint, Address FROM PickupPoint ORDER BY IDPickupPoint;",
        r => new PickupPoint { IDPickupPoint = (int)r["IDPickupPoint"], Address = (string)r["Address"] });

    // ===================== Товары =====================

    /// <summary>Каталог товаров с наименованиями справочников для отображения.</summary>
    public static List<Product> GetProducts() => Query(
        "SELECT p.ProductID, p.ProductName, p.ProductDescription, p.ProductPhoto, " +
        "p.ProductPrice, p.CurrentDiscount, p.QuantityInStock, " +
        "p.IDUnit, p.IDCategory, p.IDSupplier, p.IDManufacturer, " +
        "u.UnitName, c.CategoryName, s.SupplierName, m.ManufacturerName " +
        "FROM Product p " +
        "INNER JOIN Unit u ON u.IDUnit = p.IDUnit " +
        "INNER JOIN Category c ON c.IDCategory = p.IDCategory " +
        "INNER JOIN Supplier s ON s.IDSupplier = p.IDSupplier " +
        "INNER JOIN Manufacturer m ON m.IDManufacturer = p.IDManufacturer " +
        "ORDER BY p.ProductID;",
        MapProduct);

    private static Product MapProduct(SqlDataReader r) => new Product
    {
        ProductID = (int)r["ProductID"],
        ProductName = (string)r["ProductName"],
        ProductDescription = r["ProductDescription"] as string ?? "",
        ProductPhoto = r["ProductPhoto"] as string ?? "",
        ProductPrice = (decimal)r["ProductPrice"],
        CurrentDiscount = Convert.ToInt32(r["CurrentDiscount"]),
        QuantityInStock = (int)r["QuantityInStock"],
        IDUnit = (int)r["IDUnit"],
        IDCategory = (int)r["IDCategory"],
        IDSupplier = (int)r["IDSupplier"],
        IDManufacturer = (int)r["IDManufacturer"],
        UnitName = (string)r["UnitName"],
        CategoryName = (string)r["CategoryName"],
        SupplierName = (string)r["SupplierName"],
        ManufacturerName = (string)r["ManufacturerName"]
    };

    /// <summary>Добавляет товар, возвращает присвоенный ProductID.</summary>
    public static int AddProduct(Product p) => Convert.ToInt32(Scalar(
        "INSERT INTO Product (ProductName, ProductDescription, ProductPhoto, ProductPrice, " +
        "CurrentDiscount, QuantityInStock, IDUnit, IDCategory, IDSupplier, IDManufacturer) " +
        "VALUES (@Name, @Description, @Photo, @Price, @Discount, @Stock, @Unit, @Category, @Supplier, @Manufacturer); " +
        "SELECT SCOPE_IDENTITY();",
        P("@Name", p.ProductName), P("@Description", p.ProductDescription),
        P("@Photo", p.ProductPhoto), P("@Price", p.ProductPrice),
        P("@Discount", p.CurrentDiscount), P("@Stock", p.QuantityInStock),
        P("@Unit", p.IDUnit), P("@Category", p.IDCategory),
        P("@Supplier", p.IDSupplier), P("@Manufacturer", p.IDManufacturer)));

    /// <summary>Обновляет данные товара по ProductID.</summary>
    public static bool UpdateProduct(Product p) => NonQuery(
        "UPDATE Product SET ProductName = @Name, ProductDescription = @Description, " +
        "ProductPhoto = @Photo, ProductPrice = @Price, CurrentDiscount = @Discount, " +
        "QuantityInStock = @Stock, IDUnit = @Unit, IDCategory = @Category, " +
        "IDSupplier = @Supplier, IDManufacturer = @Manufacturer " +
        "WHERE ProductID = @ID;",
        P("@Name", p.ProductName), P("@Description", p.ProductDescription),
        P("@Photo", p.ProductPhoto), P("@Price", p.ProductPrice),
        P("@Discount", p.CurrentDiscount), P("@Stock", p.QuantityInStock),
        P("@Unit", p.IDUnit), P("@Category", p.IDCategory),
        P("@Supplier", p.IDSupplier), P("@Manufacturer", p.IDManufacturer),
        P("@ID", p.ProductID)) > 0;

    /// <summary>Удаляет товар; false — товара с таким ID не было.</summary>
    public static bool DeleteProduct(int productId) => NonQuery(
        "DELETE FROM Product WHERE ProductID = @ID;", P("@ID", productId)) > 0;

    /// <summary>True, если товар входит хотя бы в один заказ (удалять нельзя).</summary>
    public static bool IsProductInOrders(int productId) =>
        (int)Scalar("SELECT COUNT(*) FROM OrderProduct WHERE IDProduct = @ID;",
                    P("@ID", productId)) > 0;

    // ===================== Заказы =====================

    /// <summary>Все заказы с названиями статусов, адресами пунктов выдачи и ФИО клиентов.</summary>
    public static List<Order> GetOrders() => Query(
        "SELECT o.IDOrder, o.OrderDate, o.OrderDeliveryDate, o.IDPickupPoint, o.IDUser, " +
        "o.OrderCode, o.IDStatus, s.StatusName, pp.Address AS PickupPointAddress, " +
        "ISNULL(u.UserSurname + ' ' + u.UserName + ISNULL(' ' + u.UserPatronymic, ''), '') AS UserFullName " +
        "FROM [Order] o " +
        "INNER JOIN OrderStatus s ON s.IDStatus = o.IDStatus " +
        "INNER JOIN PickupPoint pp ON pp.IDPickupPoint = o.IDPickupPoint " +
        "LEFT JOIN [User] u ON u.IDUser = o.IDUser " +
        "ORDER BY o.IDOrder;",
        MapOrder);

    private static Order MapOrder(SqlDataReader r) => new Order
    {
        IDOrder = (int)r["IDOrder"],
        OrderDate = (DateTime)r["OrderDate"],
        OrderDeliveryDate = (DateTime)r["OrderDeliveryDate"],
        IDPickupPoint = (int)r["IDPickupPoint"],
        IDUser = r["IDUser"] == DBNull.Value ? 0 : (int)r["IDUser"],
        OrderCode = (int)r["OrderCode"],
        IDStatus = (int)r["IDStatus"],
        StatusName = (string)r["StatusName"],
        PickupPointAddress = (string)r["PickupPointAddress"],
        UserFullName = (string)r["UserFullName"]
    };

    /// <summary>Состав всех заказов.</summary>
    public static List<OrderProduct> GetOrderProducts() => Query(
        "SELECT op.IDOrder, op.IDProduct, op.ProductCount, p.ProductName " +
        "FROM OrderProduct op INNER JOIN Product p ON p.ProductID = op.IDProduct " +
        "ORDER BY op.IDOrder, op.IDProduct;",
        MapOrderProduct);

    /// <summary>Состав одного заказа.</summary>
    public static List<OrderProduct> GetOrderProducts(int orderId) => Query(
        "SELECT op.IDOrder, op.IDProduct, op.ProductCount, p.ProductName " +
        "FROM OrderProduct op INNER JOIN Product p ON p.ProductID = op.IDProduct " +
        "WHERE op.IDOrder = @ID ORDER BY op.IDProduct;",
        MapOrderProduct, P("@ID", orderId));

    private static OrderProduct MapOrderProduct(SqlDataReader r) => new OrderProduct
    {
        IDOrder = (int)r["IDOrder"],
        IDProduct = (int)r["IDProduct"],
        ProductCount = (int)r["ProductCount"],
        ProductName = (string)r["ProductName"]
    };

    /// <summary>Создаёт заказ со составом в одной транзакции; возвращает IDOrder.</summary>
    public static int AddOrder(Order order, List<OrderProduct> items)
    {
        using SqlConnection connection = NewConnection();
        connection.Open();
        using SqlTransaction transaction = connection.BeginTransaction();
        try
        {
            const string insertOrder =
                "INSERT INTO [Order] (OrderDate, OrderDeliveryDate, IDPickupPoint, IDUser, " +
                "OrderCode, IDStatus) OUTPUT INSERTED.IDOrder " +
                "VALUES (@OrderDate, @OrderDeliveryDate, @PickupPoint, @User, @Code, @Status);";
            int orderId;
            using (SqlCommand command = new SqlCommand(insertOrder, connection, transaction))
            {
                command.Parameters.AddRange(new[]
                {
                    P("@OrderDate", order.OrderDate),
                    P("@OrderDeliveryDate", order.OrderDeliveryDate),
                    P("@PickupPoint", order.IDPickupPoint),
                    P("@User", order.IDUser == 0 ? DBNull.Value : order.IDUser),
                    P("@Code", order.OrderCode),
                    P("@Status", order.IDStatus)
                });
                orderId = (int)command.ExecuteScalar();
            }
            const string insertItem =
                "INSERT INTO OrderProduct (IDOrder, IDProduct, ProductCount) " +
                "VALUES (@Order, @Product, @Count);";
            foreach (OrderProduct item in items)
            {
                using SqlCommand command = new SqlCommand(insertItem, connection, transaction);
                command.Parameters.AddRange(new[]
                {
                    P("@Order", orderId),
                    P("@Product", item.IDProduct),
                    P("@Count", item.ProductCount)
                });
                command.ExecuteNonQuery();
            }
            // Списываем товар со склада той же транзакцией
            const string decrement =
                "UPDATE Product SET QuantityInStock = QuantityInStock - @Delta WHERE ProductID = @Id;";
            foreach (OrderProduct item in items)
            {
                using SqlCommand command = new SqlCommand(decrement, connection, transaction);
                command.Parameters.AddRange(new[]
                {
                    P("@Delta", item.ProductCount),
                    P("@Id", item.IDProduct)
                });
                command.ExecuteNonQuery();
            }
            transaction.Commit();
            return orderId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>Обновляет даты, пункт выдачи и статус заказа.</summary>
    public static bool UpdateOrder(Order o) => NonQuery(
        "UPDATE [Order] SET OrderDate = @OrderDate, OrderDeliveryDate = @OrderDeliveryDate, " +
        "IDPickupPoint = @PickupPoint, IDStatus = @Status WHERE IDOrder = @ID;",
        P("@OrderDate", o.OrderDate), P("@OrderDeliveryDate", o.OrderDeliveryDate),
        P("@PickupPoint", o.IDPickupPoint), P("@Status", o.IDStatus),
        P("@ID", o.IDOrder)) > 0;

    /// <summary>Удаляет заказ: возвращает товары на склад, затем удаляет состав и заказ.</summary>
    public static bool DeleteOrder(int orderId)
    {
        NonQuery("UPDATE p SET p.QuantityInStock = p.QuantityInStock + op.ProductCount " +
                 "FROM Product p INNER JOIN OrderProduct op ON op.IDProduct = p.ProductID " +
                 "WHERE op.IDOrder = @ID;", P("@ID", orderId));
        NonQuery("DELETE FROM OrderProduct WHERE IDOrder = @ID;", P("@ID", orderId));
        return NonQuery("DELETE FROM [Order] WHERE IDOrder = @ID;", P("@ID", orderId)) > 0;
    }
}
