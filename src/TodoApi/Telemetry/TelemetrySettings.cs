using Microsoft.Extensions.Configuration;

namespace TodoApi.Telemetry;

/// <summary>
/// Raw telemetry configuration. Values come from the standard <c>OTEL_*</c>
/// settings (environment variables by default) first, then the
/// <c>OpenTelemetry</c> configuration section.
/// </summary>
public sealed class TelemetrySettings
{
    public const string SectionName = "OpenTelemetry";

    public bool? Enabled { get; set; }

    public bool? ExporterEnabled { get; set; }

    public string? ServiceName { get; set; }

    public string? Endpoint { get; set; }

    public string? Protocol { get; set; }

    public bool SdkDisabled { get; set; }

    public static TelemetrySettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.GetSection(SectionName).Get<TelemetrySettings>()
            ?? new TelemetrySettings();

        settings.ServiceName = FirstSet(
            configuration["OTEL_SERVICE_NAME"],
            settings.ServiceName);
        settings.Endpoint = FirstSet(
            configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            settings.Endpoint);
        settings.Protocol = FirstSet(
            configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
            settings.Protocol);
        settings.SdkDisabled = settings.SdkDisabled || IsTrue(configuration["OTEL_SDK_DISABLED"]);

        return settings;
    }

    private static string? FirstSet(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool IsTrue(string? value) =>
        string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
