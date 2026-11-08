using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;

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
        using var capture = new ActivityCapture(InstrumentedTodoStore.ActivitySourceName);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        var item = store.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, "Buy oat milk", null, Priority.High);
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        adapter.Calls.ShouldBe(["create", "list", "get", "update", "complete", "delete"]);

        var spans = capture.From(InstrumentedTodoStore.ActivitySourceName);
        spans.Count.ShouldBe(6);

        foreach (var operation in new[] { "list", "get", "create", "update", "complete", "delete" })
        {
            spans.Count(span => Equals(span.GetTagItem("todo.operation"), operation)).ShouldBe(1);
        }

        metrics.All("todo.store.operation.duration").Count.ShouldBe(6);
    }

    [Fact]
    public void Gauges_ReadThroughTheStoreInterfaceOverAnyAdapter()
    {
        var adapter = new RecordingTodoStore();
        var open = adapter.Create("Buy milk", null, Priority.Low, DateTimeOffset.UnixEpoch);
        var done = adapter.Create("Ship the demo", null, Priority.High, DateTimeOffset.UnixEpoch);
        adapter.Complete(done.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        using var store = new InstrumentedTodoStore(adapter);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        metrics.ObserveGauges();

        metrics.Single("todo.items.total").Value.ShouldBe(2L);
        metrics.Single("todo.items.done").Value.ShouldBe(1L);

        store.Complete(open.Id, DateTimeOffset.UnixEpoch.AddHours(2));
        store.Delete(done.Id);

        metrics.ObserveGauges();

        metrics.All("todo.items.total").Last().Value.ShouldBe(1L);
        metrics.All("todo.items.done").Last().Value.ShouldBe(1L);
    }
}
