using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Telemetry;

/// <summary>Covers <see cref="TelemetryRegistration"/>: which providers and options the host ends up with.</summary>
public sealed class TelemetryRegistrationTests
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
    public void Resource_CarriesTheDeploymentEnvironmentName()
    {
        using var factory = new TelemetryEnabledFactory();

        var attributes = factory.Services.GetRequiredService<TracerProvider>()
            .GetResource()
            .Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

        attributes[TodoTelemetry.Resource.DeploymentEnvironmentName].ShouldBe("Testing");
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
}
