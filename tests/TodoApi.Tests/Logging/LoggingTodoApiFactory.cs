using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace TodoApi.Tests.Logging;

/// <summary>
/// A factory whose only logging sink is <see cref="Logs"/>. The telemetry
/// pipeline stays disabled by the Testing environment, so no OTLP export is
/// attempted. Todo Item logs are lowered to Debug to capture successful reads.
/// </summary>
internal sealed class LoggingTodoApiFactory : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider Logs { get; } = new();

    public FakeTimeProvider Clock { get; } = new(TodoApiFactory.StartTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(Logs);
            logging.AddFilter("TodoApi", LogLevel.Debug);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
