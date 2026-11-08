using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Tests.Telemetry;

[Collection(TracingCollection.Name)]
public sealed class MetricsIntegrationTests
{
    [Fact]
    public async Task CreateCompleteDelete_PublishLifecycleMetricsThroughRealRequests()
    {
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);
        using var factory = new TelemetryEnabledFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var createResponse = await client.PostAsJsonAsync(
            "/todos",
            new { title = "Measure me 7b3e", priority = "high" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = await createResponse.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, cancellationToken);
        item.ShouldNotBeNull();

        using var completeResponse = await client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, cancellationToken);
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var missingResponse = await client.GetAsync("/todos/9999", cancellationToken);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var deleteResponse = await client.DeleteAsync($"/todos/{item.Id}", cancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        metrics.ObserveGauges();

        var created = metrics.Single("todo.items.created");
        created.Value.ShouldBe(1L);
        created.Unit.ShouldBe("{item}");
        created.Tag("todo.priority").ShouldBe("high");

        metrics.Single("todo.items.completed").Value.ShouldBe(1L);
        metrics.Single("todo.items.deleted").Value.ShouldBe(1L);

        var failure = metrics.Single("todo.store.failures");
        failure.Value.ShouldBe(1L);
        failure.Tag("todo.operation").ShouldBe("get");
        failure.Tag("todo.outcome").ShouldBe("not found");

        var durations = metrics.All("todo.store.operation.duration");
        durations.Select(duration => duration.Tag("todo.operation"))
            .ShouldBe(["create", "complete", "get", "delete"], ignoreOrder: true);
        durations.ShouldAllBe(duration => duration.Unit == "s");

        metrics.Single("todo.items.total").Value.ShouldBe(0L);
        metrics.Single("todo.items.done").Value.ShouldBe(0L);
    }
}
