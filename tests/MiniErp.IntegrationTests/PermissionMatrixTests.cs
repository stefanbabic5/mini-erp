using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Platform.Security;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary>
/// The role → permission matrix from specification §2, checked through the API's real
/// authorization policies. The expected table is written out here on purpose, independently
/// of RolePermissions, so a change to the matrix must change this test too.
/// </summary>
public class PermissionMatrixTests(ApiFactory api)
{
    public static TheoryData<string, string, bool> Matrix => new()
    {
        // role,        permission,                       allowed
        { "Admin",     Permissions.CatalogRead,           true },
        { "Admin",     Permissions.CatalogWrite,          true },
        { "Admin",     Permissions.CustomersRead,         true },
        { "Admin",     Permissions.CustomersWrite,        true },
        { "Admin",     Permissions.NumberSeriesManage,    true },

        { "Sales",     Permissions.CatalogRead,           true },
        { "Sales",     Permissions.CatalogWrite,          false },
        { "Sales",     Permissions.CustomersRead,         true },
        { "Sales",     Permissions.CustomersWrite,        true },
        { "Sales",     Permissions.NumberSeriesManage,    false },

        { "Warehouse", Permissions.CatalogRead,           true },
        { "Warehouse", Permissions.CatalogWrite,          true },
        { "Warehouse", Permissions.CustomersRead,         true },
        { "Warehouse", Permissions.CustomersWrite,        false },
        { "Warehouse", Permissions.NumberSeriesManage,    false },

        { "Finance",   Permissions.CatalogRead,           true },
        { "Finance",   Permissions.CatalogWrite,          false },
        { "Finance",   Permissions.CustomersRead,         true },
        { "Finance",   Permissions.CustomersWrite,        false },
        { "Finance",   Permissions.NumberSeriesManage,    false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Role_grants_exactly_the_permissions_in_the_matrix(string role, string permission, bool allowed)
    {
        var authorization = api.Services.GetRequiredService<IAuthorizationService>();
        var user = TestTokens.Principal(TestTokens.TenantAlfa, role);

        var result = await authorization.AuthorizeAsync(user, permission);

        result.Succeeded.ShouldBe(allowed);
    }

    [Fact]
    public void Every_permission_is_in_the_matrix()
    {
        var covered = Matrix.Select(row => (string)row.Data.Item2).ToHashSet();

        covered.ShouldBe(Permissions.All, ignoreOrder: true);
    }

    [Fact]
    public async Task User_without_roles_gets_no_permission()
    {
        var authorization = api.Services.GetRequiredService<IAuthorizationService>();
        var user = TestTokens.Principal(TestTokens.TenantAlfa);

        foreach (var permission in Permissions.All)
        {
            (await authorization.AuthorizeAsync(user, permission)).Succeeded.ShouldBeFalse(permission);
        }
    }

    [Fact]
    public async Task Admin_without_tenant_gets_no_permission()
    {
        var authorization = api.Services.GetRequiredService<IAuthorizationService>();
        var user = TestTokens.Principal(tenantId: null, "Admin");

        foreach (var permission in Permissions.All)
        {
            (await authorization.AuthorizeAsync(user, permission)).Succeeded.ShouldBeFalse(permission);
        }
    }
}
