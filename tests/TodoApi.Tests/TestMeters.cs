using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace TodoApi.Tests;

/// <summary>
/// Creates an <see cref="IMeterFactory"/> of its own for one test, so the
/// meters it creates are scoped to that test and invisible to the others.
/// </summary>
internal static class TestMeters
{
    public static IMeterFactory NewFactory() =>
        new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
}
