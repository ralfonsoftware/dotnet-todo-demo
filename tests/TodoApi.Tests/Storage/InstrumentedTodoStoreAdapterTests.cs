using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Storage;

/// <summary>
/// Proves the observability decorator is welded to the <see cref="ITodoStore"/>
/// contract rather than to <see cref="InMemoryTodoStore"/>: it forwards every
/// operation to an arbitrary adapter while still emitting its own spans and
/// metrics.
/// </summary>
[Collection(TracingCollection.Name)]
public sealed class InstrumentedTodoStoreAdapterTests
{
    [Fact]
    public void EveryOperation_ForwardsToTheAdapterAndStillEmitsSpansAndDurations()
    {
        var adapter = new RecordingTodoStore();
        using var store = new InstrumentedTodoStore(adapter);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        var item = store.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, Drafts.Of("Buy oat milk", null, Priority.High));
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        adapter.Calls.ShouldBe(
        [
            TodoOperation.Create.Name,
            TodoOperation.List.Name,
            TodoOperation.Get.Name,
            TodoOperation.Update.Name,
            TodoOperation.Complete.Name,
            TodoOperation.Delete.Name
        ]);

        var spans = capture.From(TodoTelemetry.ActivitySourceName);
        spans.Count.ShouldBe(6);

        foreach (var operation in new[]
        {
            TodoOperation.List,
            TodoOperation.Get,
            TodoOperation.Create,
            TodoOperation.Update,
            TodoOperation.Complete,
            TodoOperation.Delete
        })
        {
            spans.Count(span => Equals(
                span.GetTagItem(TodoTelemetry.Tags.Operation), operation.Name)).ShouldBe(1);
        }

        metrics.All(TodoTelemetry.Instruments.OperationDuration).Count.ShouldBe(6);
    }

    [Fact]
    public void Gauges_ReadThroughTheStoreInterfaceOverAnyAdapter()
    {
        var adapter = new RecordingTodoStore();
        var open = adapter.Create(Drafts.Of("Buy milk", null, Priority.Low), DateTimeOffset.UnixEpoch);
        var done = adapter.Create(Drafts.Of("Ship the demo", null, Priority.High), DateTimeOffset.UnixEpoch);
        adapter.Complete(done.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        using var store = new InstrumentedTodoStore(adapter);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        metrics.ObserveGauges();

        metrics.Single(TodoTelemetry.Instruments.ItemsTotal).Value.ShouldBe(2L);
        metrics.Single(TodoTelemetry.Instruments.ItemsDone).Value.ShouldBe(1L);

        store.Complete(open.Id, DateTimeOffset.UnixEpoch.AddHours(2));
        store.Delete(done.Id);

        metrics.ObserveGauges();

        metrics.All(TodoTelemetry.Instruments.ItemsTotal)[^1].Value.ShouldBe(1L);
        metrics.All(TodoTelemetry.Instruments.ItemsDone)[^1].Value.ShouldBe(1L);
    }
}
