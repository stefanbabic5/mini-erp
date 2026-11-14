namespace MiniErp.Platform.Security;

/// <summary>
/// The role → permission matrix (specification §2). The only place that decides who may do what.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Matrix =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Roles.Admin] = Permissions.All.ToHashSet(),
            [Roles.Sales] = new HashSet<string>
            {
                Permissions.CatalogRead,
                Permissions.CustomersRead,
                Permissions.CustomersWrite,
            },
            [Roles.Warehouse] = new HashSet<string>
            {
                Permissions.CatalogRead,
                Permissions.CatalogWrite,
                Permissions.CustomersRead,
            },
            [Roles.Finance] = new HashSet<string>
            {
                Permissions.CatalogRead,
                Permissions.CustomersRead,
            },
        };

    /// <summary>All permissions granted by the given roles. Unknown roles grant nothing.</summary>
    public static IReadOnlySet<string> For(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (Matrix.TryGetValue(role, out var granted))
            {
                permissions.UnionWith(granted);
            }
        }

        return permissions;
    }
}
