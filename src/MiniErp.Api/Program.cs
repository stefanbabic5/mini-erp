using MiniErp.Catalog;
using MiniErp.Inventory;
using MiniErp.Invoicing;
using MiniErp.Sales;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCatalogModule()
    .AddInventoryModule()
    .AddSalesModule()
    .AddInvoicingModule();

var app = builder.Build();

app.MapCatalogModule();
app.MapInventoryModule();
app.MapSalesModule();
app.MapInvoicingModule();

app.Run();
