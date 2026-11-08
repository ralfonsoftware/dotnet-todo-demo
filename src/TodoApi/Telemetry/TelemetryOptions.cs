using System.Reflection;
using OpenTelemetry.Exporter;

namespace TodoApi.Telemetry;

/// <summary>
/// Resolved telemetry settings. Values come from <see cref="TelemetrySettings"/>,
/// which applies the standard <c>OTEL_*</c> settings first, then the
/// <c>OpenTelemetry</c> configuration section, then the defaults below.
/// </summary>
public sealed record TelemetryOptions
{
    public const string DefaultServiceName = "todo-api";
    public const string DefaultGrpcEndpoint = "http://localhost:4317";
    public const string DefaultHttpEndpoint = "http://localhost:4318";

    public required bool Enabled { get; init; }

    public required bool ExporterEnabled { get; init; }

    public required string ServiceName { get; init; }

    public required string ServiceVersion { get; init; }

    public required string DeploymentEnvironment { get; init; }

    public required Uri Endpoint { get; init; }

    public required OtlpExportProtocol Protocol { get; init; }

    public static TelemetryOptions Resolve(TelemetrySettings settings, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var protocol = ParseProtocol(settings.Protocol);

        var endpoint = settings.Endpoint is { } endpointValue
            ? ParseEndpoint(endpointValue)
            : new Uri(protocol == OtlpExportProtocol.HttpProtobuf ? DefaultHttpEndpoint : DefaultGrpcEndpoint);

        return new TelemetryOptions
        {
            Enabled = (settings.Enabled ?? true) && !settings.SdkDisabled,
            ExporterEnabled = settings.ExporterEnabled ?? true,
            ServiceName = settings.ServiceName ?? DefaultServiceName,
            ServiceVersion = ResolveServiceVersion(),
            DeploymentEnvironment = environmentName,
            Endpoint = endpoint,
            Protocol = protocol
        };
    }

    private static string? FirstSet(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static Uri ParseEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"OpenTelemetry endpoint '{value}' must be an absolute http or https URI.");
        }

        return endpoint;
    }

    private static OtlpExportProtocol ParseProtocol(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" or "http" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException(
                $"OpenTelemetry protocol '{value}' is not supported. Use 'grpc' or 'http/protobuf'.")
        };

    private static string ResolveServiceVersion()
    {
        var assembly = typeof(TelemetryOptions).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return FirstSet(informationalVersion, assembly.GetName().Version?.ToString()) ?? "unknown";
    }
}
