using System.Reflection;
using OpenTelemetry.Exporter;

namespace TodoApi.Telemetry;

/// <summary>
/// Resolved telemetry settings. Values come from standard <c>OTEL_*</c>
/// environment variables first, then the <c>OpenTelemetry</c> configuration
/// section, then the defaults below.
/// </summary>
public sealed record TelemetryOptions
{
    public const string SectionName = "OpenTelemetry";
    public const string DefaultServiceName = "todo-api";
    public const string GrpcEndpoint = "http://localhost:4317";
    public const string HttpEndpoint = "http://localhost:4318";

    public required bool Enabled { get; init; }

    public required bool ExporterEnabled { get; init; }

    public required string ServiceName { get; init; }

    public required string ServiceVersion { get; init; }

    public required string DeploymentEnvironment { get; init; }

    public required Uri Endpoint { get; init; }

    public required OtlpExportProtocol Protocol { get; init; }

    public static TelemetryOptions Resolve(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var sdkDisabled = IsTrue(Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"));

        var protocol = ParseProtocol(FirstSet(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL"),
            section["Protocol"]));

        var endpoint = FirstSet(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
            section["Endpoint"]) is { } endpointValue
            ? ParseEndpoint(endpointValue)
            : new Uri(protocol == OtlpExportProtocol.HttpProtobuf ? HttpEndpoint : GrpcEndpoint);

        return new TelemetryOptions
        {
            Enabled = (section.GetValue<bool?>("Enabled") ?? true) && !sdkDisabled,
            ExporterEnabled = section.GetValue<bool?>("ExporterEnabled") ?? true,
            ServiceName = FirstSet(
                Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
                section["ServiceName"]) ?? DefaultServiceName,
            ServiceVersion = ResolveServiceVersion(),
            DeploymentEnvironment = environmentName,
            Endpoint = endpoint,
            Protocol = protocol
        };
    }

    private static string? FirstSet(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool IsTrue(string? value) =>
        string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

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
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return FirstSet(informationalVersion, assembly.GetName().Version?.ToString()) ?? "unknown";
    }
}
