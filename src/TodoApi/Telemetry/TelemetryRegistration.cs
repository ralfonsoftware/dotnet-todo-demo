using OpenTelemetry.Exporter;
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

        var options = TelemetryOptions.Resolve(configuration, environmentName);
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

        openTelemetry.WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

            if (options.ExporterEnabled)
            {
                tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (options.ExporterEnabled)
            {
                metrics.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
            }
        });

        openTelemetry.WithLogging(logging =>
        {
            if (options.ExporterEnabled)
            {
                logging.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
            }
        });

        return services;
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, TelemetryOptions options)
    {
        exporter.Endpoint = options.Endpoint;
        exporter.Protocol = options.Protocol;
    }
}
