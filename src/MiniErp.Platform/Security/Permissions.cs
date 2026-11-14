namespace MiniErp.Platform.Security;

/// <summary>
/// What a user may do. Endpoints require a permission, never a role, so changing who may do
/// what is a change to <see cref="RolePermissions"/> alone. Each permission is also the name of
/// an authorization policy: <c>.RequireAuthorization(Permissions.CatalogWrite)</c>.
/// </summary>
public static class Permissions
{
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";
    public const string CustomersRead = "sales.customers.read";
    public const string CustomersWrite = "sales.customers.write";
    public const string NumberSeriesManage = "platform.number-series.manage";

    public static readonly IReadOnlyList<string> All =
    [
        CatalogRead,
        CatalogWrite,
        CustomersRead,
        CustomersWrite,
        NumberSeriesManage,
    ];
}
