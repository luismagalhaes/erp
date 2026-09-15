using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Erp.Identity.Telemetry;

/// <summary>
/// Wires <see cref="ILogger"/>, tracing and metrics through OpenTelemetry. ASP.NET Core stamps a
/// trace/span id on every request via <see cref="System.Diagnostics.Activity"/>, and OpenTelemetry
/// carries that id onto every log line, so a request can be followed end to end without any manual
/// correlation id.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Exporting to Azure Monitor only turns on when "ApplicationInsights:ConnectionString" is
    /// configured, so the app keeps working unchanged in environments — development, tests — that
    /// don't have one.
    /// </summary>
    public static WebApplicationBuilder AddErpOpenTelemetry(this WebApplicationBuilder builder, string serviceName)
    {
        var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(connectionString))
            otel.UseAzureMonitor(options => options.ConnectionString = connectionString);

        return builder;
    }
}
