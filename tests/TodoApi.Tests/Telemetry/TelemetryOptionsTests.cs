using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Telemetry;

/// <summary>
/// Covers <see cref="TelemetryOptions.Resolve"/>: defaults, protocol and
/// endpoint resolution, and the enabled flags. Reading and precedence of the
/// raw settings belong to <see cref="TelemetrySettings"/> and its tests.
/// </summary>
public sealed class TelemetryOptionsTests
{
    [Fact]
    public void Resolve_UsesDocumentedDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = TelemetryOptions.Resolve(
            TelemetrySettings.FromConfiguration(configuration),
            "Testing");

        options.Enabled.ShouldBeTrue();
        options.ExporterEnabled.ShouldBeTrue();
        options.ServiceName.ShouldBe("todo-api");
        options.DeploymentEnvironment.ShouldBe("Testing");
        options.Endpoint.ShouldBe(new Uri("http://localhost:4317"));
        options.Protocol.ShouldBe(OtlpExportProtocol.Grpc);
        options.ServiceVersion.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Resolve_HonorsApplicationConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "false",
                ["OpenTelemetry:ExporterEnabled"] = "false",
                ["OpenTelemetry:ServiceName"] = "configured-service",
                ["OpenTelemetry:Endpoint"] = "http://collector:4318",
                ["OpenTelemetry:Protocol"] = "http/protobuf"
            })
            .Build();

        var options = TelemetryOptions.Resolve(
            TelemetrySettings.FromConfiguration(configuration),
            "Staging");

        options.Enabled.ShouldBeFalse();
        options.ExporterEnabled.ShouldBeFalse();
        options.ServiceName.ShouldBe("configured-service");
        options.DeploymentEnvironment.ShouldBe("Staging");
        options.Endpoint.ShouldBe(new Uri("http://collector:4318"));
        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
    }

    [Fact]
    public void Resolve_SdkDisabled_DisablesTelemetry()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true",
                ["OTEL_SDK_DISABLED"] = "true"
            })
            .Build();

        TelemetryOptions.Resolve(TelemetrySettings.FromConfiguration(configuration), "Development")
            .Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_UsesOtlpHttpDefaultPort_WhenProtocolIsHttpProtobuf()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
            })
            .Build();

        var options = TelemetryOptions.Resolve(
            TelemetrySettings.FromConfiguration(configuration),
            "Development");

        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
        options.Endpoint.ShouldBe(new Uri("http://localhost:4318"));
    }

    [Fact]
    public void Resolve_RejectsInvalidProtocol()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "carrier-pigeon"
            })
            .Build();

        Should.Throw<InvalidOperationException>(() =>
            TelemetryOptions.Resolve(TelemetrySettings.FromConfiguration(configuration), "Development"));
    }
}
