using MiniErp.Api.Hosting;
using MiniErp.Catalog;
using MiniErp.Inventory;
using MiniErp.Invoicing;
using MiniErp.Platform.Database;
using MiniErp.Sales;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Errors leave the API as RFC 9457 Problem Details (application/problem+json).
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddMiniErpDatabase(builder.Configuration);

builder.Services
    .AddCatalogModule()
    .AddInventoryModule()
    .AddSalesModule()
    .AddInvoicingModule();

var app = builder.Build();

// Unhandled exceptions become a 500 Problem Details response without internal details;
// error status codes without a body (such as 404) get a Problem Details body too.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference();     // API reference UI: /scalar
}

app.MapHealthEndpoints();

app.MapCatalogModule();
app.MapInventoryModule();
app.MapSalesModule();
app.MapInvoicingModule();

app.Run();
