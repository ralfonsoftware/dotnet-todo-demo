using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
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
        Client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Store = Factory.Services.GetRequiredService<ITodoStore>();
    }

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    protected TodoItem Seed(
        string title = "Seeded todo",
        string? description = null,
        Priority priority = Priority.Medium) =>
        Store.Create(Drafts.Of(title, description, priority), Now);

    protected TodoItem SeedCompleted(
        string title = "Seeded todo",
        string? description = null,
        Priority priority = Priority.Medium)
    {
        var item = Seed(title, description, priority);

        return Store.Complete(item.Id, Now) switch
        {
            CompletionResult.Completed completed => completed.Item,
            _ => throw new InvalidOperationException("Seeded Todo Item did not complete.")
        };
    }

    protected Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string json) =>
        Client.SendAsync(
            new HttpRequestMessage(method, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            TestContext.Current.CancellationToken);

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
