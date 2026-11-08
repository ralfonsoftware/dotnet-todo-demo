using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoApi.Tests;

/// <summary>
/// Builds a host with the OpenTelemetry pipeline enabled and the OTLP exporter
/// disabled, so tests can observe activities without making network calls.
/// </summary>
internal sealed class TelemetryEnabledFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("OpenTelemetry:Enabled", "true");
        builder.UseSetting("OpenTelemetry:ExporterEnabled", "false");
    }
}
