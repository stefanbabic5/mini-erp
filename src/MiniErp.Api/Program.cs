using System.Text.Json.Serialization;
using MiniErp.Api.Hosting;
using MiniErp.Catalog;
using MiniErp.Inventory;
using MiniErp.Invoicing;
using MiniErp.Platform;
using MiniErp.Platform.Database;
using MiniErp.Platform.Security;
using MiniErp.Sales;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Errors leave the API as RFC 9457 Problem Details (application/problem+json).
builder.Services.AddProblemDetails();

// Enums travel as names ("Standard"), not numbers, so the API is readable and adding an enum
// member can't silently shift meanings.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddMiniErpDatabase(builder.Configuration);
builder.Services.AddMiniErpSecurity(builder.Configuration);

builder.Services
    .AddCatalogModule()
    .AddInventoryModule()
    .AddSalesModule()
    .AddInvoicingModule();

var app = builder.Build();

// Development and tests bring the database schema up to date on start. Other environments
// apply migrations as a separate deployment step instead.
if (app.Configuration.GetValue<bool>(DatabaseRegistration.MigrateOnStartupSetting))
{
    await app.Services.MigrateModuleDatabasesAsync();
}

// Unhandled exceptions become a 500 Problem Details response without internal details;
// error status codes without a body (404, 401, 403) get a Problem Details body too.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();                // OpenAPI document: /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();     // API reference UI: /scalar
}

app.MapHealthEndpoints();
app.MapPlatformEndpoints();

app.MapCatalogModule();
app.MapInventoryModule();
app.MapSalesModule();
app.MapInvoicingModule();

await app.RunAsync();
