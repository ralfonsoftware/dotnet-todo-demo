using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace TodoApi.Tests;

public sealed class TodoApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset StartTime = new(2026, 11, 8, 11, 16, 0, TimeSpan.Zero);

    public FakeTimeProvider Clock { get; } = new(StartTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
