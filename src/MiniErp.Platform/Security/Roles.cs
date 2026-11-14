namespace MiniErp.Platform.Security;

/// <summary>Roles as they arrive in the access token's <c>roles</c> claim (specification §2).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Sales = "Sales";
    public const string Warehouse = "Warehouse";
    public const string Finance = "Finance";

    public static readonly IReadOnlyList<string> All = [Admin, Sales, Warehouse, Finance];
}
