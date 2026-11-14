using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MiniErp.Api.Hosting;

/// <summary>
/// Logs, traces and metrics through OpenTelemetry. Locally they go to the dashboard from
/// compose.yaml (http://localhost:18888); in any other environment, to whatever OTLP
/// endpoint is configured.
/// </summary>
internal static class ObservabilityRegistration
{
    public const string ServiceName = "mini-erp-api";

    /// <summary>Standard OpenTelemetry setting: where to send telemetry, e.g. http://localhost:4317.</summary>
    private const string OtlpEndpointSetting = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    // Health probes run every few seconds; tracing them would only add noise.
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Export only when an endpoint is configured, so tests and plain `dotnet run`
        // without the dashboard don't try to reach a collector that isn't there.
        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointSetting]))
        {
            openTelemetry.UseOtlpExporter();
        }

        return builder;
    }
}
