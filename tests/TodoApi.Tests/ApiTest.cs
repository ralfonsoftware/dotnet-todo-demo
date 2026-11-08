using Microsoft.Extensions.DependencyInjection;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Tests;

/// <summary>
/// One fresh application host, store, and clock per test. xUnit creates a new
/// test-class instance for every test, so nothing leaks between tests.
/// </summary>
public abstract class ApiTest : IDisposable
{
    protected TodoApiFactory Factory { get; } = new();

    protected HttpClient Client { get; }

    protected ITodoStore Store { get; }

    protected ApiTest()
    {
        Client = Factory.CreateClient();
        Store = Factory.Services.GetRequiredService<ITodoStore>();
    }

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    protected TodoItem Seed(
        string title = "Seeded todo",
        string? description = null,
        Priority priority = Priority.Medium) =>
        Store.Create(title, description, priority, Now);

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}
