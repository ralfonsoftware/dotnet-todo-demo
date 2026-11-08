using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public void OpenTelemetryPipeline_IsRegistered_WhenEnabled()
    {
        using var factory = new TelemetryEnabledFactory();

        factory.Services.GetRequiredService<IConfiguration>()["OpenTelemetry:Enabled"].ShouldBe("true");
        factory.Services.GetRequiredService<TelemetryOptions>().Enabled.ShouldBeTrue();
        factory.Services.GetService<TracerProvider>().ShouldNotBeNull();
        factory.Services.GetService<MeterProvider>().ShouldNotBeNull();
        factory.Services.GetServices<ILoggerProvider>()
            .ShouldContain(provider => provider is OpenTelemetryLoggerProvider);
    }

    [Fact]
    public void OpenTelemetryLogging_ExportsScopesAndFormattedMessages()
    {
        using var factory = new TelemetryEnabledFactory();

        var options = factory.Services
            .GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>()
            .Value;

        options.IncludeScopes.ShouldBeTrue();
        options.IncludeFormattedMessage.ShouldBeTrue();
    }

    [Fact]
    public void ConsoleLogging_RemainsEnabled()
    {
        using var factory = new TodoApiFactory();

        factory.Services.GetServices<ILoggerProvider>()
            .ShouldContain(provider => provider is ConsoleLoggerProvider);
    }

    [Fact]
    public void OpenTelemetryPipeline_IsAbsent_InTestEnvironment()
    {
        using var factory = new TodoApiFactory();

        var options = factory.Services.GetRequiredService<TelemetryOptions>();
        options.Enabled.ShouldBeFalse();
        options.ExporterEnabled.ShouldBeFalse();
        factory.Services.GetService<TracerProvider>().ShouldBeNull();
        factory.Services.GetService<MeterProvider>().ShouldBeNull();
        factory.Services.GetServices<ILoggerProvider>()
            .ShouldNotContain(provider => provider is OpenTelemetryLoggerProvider);
    }

    [Fact]
    public async Task TodosEndpoint_StillWorks_WithTelemetryEnabled()
    {
        using var factory = new TelemetryEnabledFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/todos", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

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
    public void Resolve_UsesStandardOtelSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "otel-service",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector:4318",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
            })
            .Build();

        var options = TelemetryOptions.Resolve(
            TelemetrySettings.FromConfiguration(configuration),
            "Development");

        options.ServiceName.ShouldBe("otel-service");
        options.Endpoint.ShouldBe(new Uri("http://otel-collector:4318"));
        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
    }

    [Fact]
    public void Resolve_StandardOtelSettings_WinOverApplicationConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:ServiceName"] = "configured-service",
                ["OpenTelemetry:Endpoint"] = "http://configured-collector:4317",
                ["OTEL_SERVICE_NAME"] = "otel-service",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector:4317"
            })
            .Build();

        var options = TelemetryOptions.Resolve(
            TelemetrySettings.FromConfiguration(configuration),
            "Development");

        options.ServiceName.ShouldBe("otel-service");
        options.Endpoint.ShouldBe(new Uri("http://otel-collector:4317"));
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
