using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Storage;
using TodoApi.Telemetry;

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

        var created = metrics.Single(TodoTelemetry.Instruments.ItemsCreated);
        created.Value.ShouldBe(1L);
        created.Unit.ShouldBe(TodoTelemetry.Units.Items);
        created.Tag(TodoTelemetry.Tags.Priority).ShouldBe("high");

        metrics.Single(TodoTelemetry.Instruments.ItemsCompleted).Value.ShouldBe(1L);
        metrics.Single(TodoTelemetry.Instruments.ItemsDeleted).Value.ShouldBe(1L);

        var failure = metrics.Single(TodoTelemetry.Instruments.StoreFailures);
        failure.Value.ShouldBe(1L);
        failure.Tag(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Get.Name);
        failure.Tag(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);

        var durations = metrics.All(TodoTelemetry.Instruments.OperationDuration);
        durations.Select(duration => duration.Tag(TodoTelemetry.Tags.Operation))
            .ShouldBe(
                [
                    TodoOperation.Create.Name,
                    TodoOperation.Complete.Name,
                    TodoOperation.Get.Name,
                    TodoOperation.Delete.Name
                ],
                ignoreOrder: true);
        durations.ShouldAllBe(duration => duration.Unit == TodoTelemetry.Units.Seconds);

        metrics.Single(TodoTelemetry.Instruments.ItemsTotal).Value.ShouldBe(0L);
        metrics.Single(TodoTelemetry.Instruments.ItemsDone).Value.ShouldBe(0L);
    }
}
