using System.Reflection;
using MiniErp.Catalog;
using MiniErp.Inventory;
using MiniErp.Invoicing;
using MiniErp.Platform;
using MiniErp.Sales;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// The assemblies the architecture rules are checked against. A new module is added here
/// and gets every rule automatically.
/// </summary>
internal static class Modules
{
    public static readonly Assembly Platform = typeof(ModuleEndpoints).Assembly;

    public static readonly IReadOnlyDictionary<string, Assembly> All = new Dictionary<string, Assembly>
    {
        ["Catalog"] = typeof(CatalogModule).Assembly,
        ["Inventory"] = typeof(InventoryModule).Assembly,
        ["Sales"] = typeof(SalesModule).Assembly,
        ["Invoicing"] = typeof(InvoicingModule).Assembly,
    };

    /// <summary>Module names in the shape xUnit needs for a <c>[Theory]</c>.</summary>
    public static TheoryData<string> Names => new(All.Keys);
}
