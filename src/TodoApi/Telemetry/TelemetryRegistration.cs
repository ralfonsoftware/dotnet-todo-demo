using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TodoApi.Telemetry;

public static class TelemetryRegistration
{
    public static IServiceCollection AddTodoApiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = TelemetrySettings.FromConfiguration(configuration);
        var options = TelemetryOptions.Resolve(settings, environmentName);
        services.AddSingleton(options);

        if (!options.Enabled)
        {
            return services;
        }

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddEnvironmentVariableDetector()
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = options.DeploymentEnvironment
                })
                .AddService(options.ServiceName, serviceVersion: options.ServiceVersion));

        openTelemetry.WithTracing(tracing => tracing
            .AddSource(TodoTelemetry.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

        openTelemetry.WithMetrics(metrics => metrics
            .AddMeter(TodoTelemetry.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation());

        openTelemetry.WithLogging(
            configureBuilder: null,
            configureOptions: loggingOptions =>
            {
                loggingOptions.IncludeFormattedMessage = true;
                loggingOptions.IncludeScopes = true;
            });

        // One exporter for all signals. With http/protobuf it appends the
        // /v1/traces, /v1/metrics, and /v1/logs paths to the base URL; with
        // gRPC it uses the base URL unchanged. Never combine it with a
        // per-signal AddOtlpExporter call: the SDK rejects that mix.
        if (options.ExporterEnabled)
        {
            openTelemetry.UseOtlpExporter(options.Protocol, options.Endpoint);
        }

        return services;
    }
}
