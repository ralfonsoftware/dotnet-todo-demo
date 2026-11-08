using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests;

[Collection(EnvironmentVariablesCollection.Name)]
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
        using var _ = EnvironmentVariables.Clear();

        var options = TelemetryOptions.Resolve(new ConfigurationBuilder().Build(), "Testing");

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
        using var _ = EnvironmentVariables.Clear();

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

        var options = TelemetryOptions.Resolve(configuration, "Staging");

        options.Enabled.ShouldBeFalse();
        options.ExporterEnabled.ShouldBeFalse();
        options.ServiceName.ShouldBe("configured-service");
        options.DeploymentEnvironment.ShouldBe("Staging");
        options.Endpoint.ShouldBe(new Uri("http://collector:4318"));
        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
    }

    [Fact]
    public void Resolve_HonorsStandardOtelEnvironmentVariables()
    {
        using var _ = EnvironmentVariables.Clear().Set(
            ("OTEL_SERVICE_NAME", "env-service"),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://env-collector:4318"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));

        var options = TelemetryOptions.Resolve(new ConfigurationBuilder().Build(), "Development");

        options.ServiceName.ShouldBe("env-service");
        options.Endpoint.ShouldBe(new Uri("http://env-collector:4318"));
        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
    }

    [Fact]
    public void Resolve_EnvironmentVariablesWin_OverApplicationConfiguration()
    {
        using var _ = EnvironmentVariables.Clear().Set(
            ("OTEL_SERVICE_NAME", "env-service"),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://env-collector:4317"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:ServiceName"] = "configured-service",
                ["OpenTelemetry:Endpoint"] = "http://configured-collector:4317"
            })
            .Build();

        var options = TelemetryOptions.Resolve(configuration, "Development");

        options.ServiceName.ShouldBe("env-service");
        options.Endpoint.ShouldBe(new Uri("http://env-collector:4317"));
    }

    [Fact]
    public void Resolve_OtelSdkDisabled_DisablesTelemetry()
    {
        using var _ = EnvironmentVariables.Clear().Set(("OTEL_SDK_DISABLED", "true"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true"
            })
            .Build();

        TelemetryOptions.Resolve(configuration, "Development").Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_UsesOtlpHttpDefaultPort_WhenProtocolIsHttpProtobuf()
    {
        using var _ = EnvironmentVariables.Clear().Set(("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));

        var options = TelemetryOptions.Resolve(new ConfigurationBuilder().Build(), "Development");

        options.Protocol.ShouldBe(OtlpExportProtocol.HttpProtobuf);
        options.Endpoint.ShouldBe(new Uri("http://localhost:4318"));
    }

    [Fact]
    public void Resolve_RejectsInvalidProtocol()
    {
        using var _ = EnvironmentVariables.Clear().Set(("OTEL_EXPORTER_OTLP_PROTOCOL", "carrier-pigeon"));

        Should.Throw<InvalidOperationException>(() =>
            TelemetryOptions.Resolve(new ConfigurationBuilder().Build(), "Development"));
    }

    private sealed class TelemetryEnabledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("OpenTelemetry:Enabled", "true");
        }
    }
}
