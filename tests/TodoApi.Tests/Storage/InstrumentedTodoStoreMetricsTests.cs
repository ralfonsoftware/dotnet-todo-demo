using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Tests.Storage;

[Collection(TracingCollection.Name)]
public sealed class InstrumentedTodoStoreMetricsTests
{
    [Fact]
    public void Create_RecordsOneItemCreatedPerPriority()
    {
        using var store = new InstrumentedTodoStore(new InMemoryTodoStore());
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        store.Create("Low item", null, Priority.Low, DateTimeOffset.UnixEpoch);
        store.Create("Medium item", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        store.Create("High item", null, Priority.High, DateTimeOffset.UnixEpoch);

        var created = metrics.All("todo.items.created");
        created.Count.ShouldBe(3);
        created.Select(measurement => measurement.Tag("todo.priority"))
            .ShouldBe(["low", "medium", "high"], ignoreOrder: true);
        created.ShouldAllBe(measurement => (long)measurement.Value == 1L);
        created.ShouldAllBe(measurement => measurement.Unit == "{item}");

        foreach (var measurement in created)
        {
            measurement.Tags.Keys.ShouldBe(["todo.priority"]);
        }
    }

    [Fact]
    public void Delete_RecordsOneItemDeletedOnlyWhenAnItemWasRemoved()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        store.Delete(item.Id).ShouldNotBeNull();
        store.Delete(item.Id).ShouldBeNull();

        var deleted = metrics.Single("todo.items.deleted");
        deleted.Value.ShouldBe(1L);
        deleted.Unit.ShouldBe("{item}");
        deleted.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_RecordsOneItemCompletedOnlyForARealTransition()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1))
            .ShouldBeOfType<CompletionResult.Completed>();
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(2))
            .ShouldBeOfType<CompletionResult.AlreadyDone>();
        store.Complete(42, DateTimeOffset.UnixEpoch.AddHours(3))
            .ShouldBeOfType<CompletionResult.NotFound>();

        var completed = metrics.Single("todo.items.completed");
        completed.Value.ShouldBe(1L);
        completed.Unit.ShouldBe("{item}");
        completed.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void FailedStoreOperations_AreCountedOnceWithOperationAndOutcome()
    {
        var inner = new InMemoryTodoStore();
        var existing = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        store.Get(42).ShouldBeNull();
        store.Update(42, "New title", null, Priority.Low).ShouldBeNull();
        store.Complete(42, DateTimeOffset.UnixEpoch)
            .ShouldBeOfType<CompletionResult.NotFound>();
        store.Delete(42).ShouldBeNull();

        // Successful reads and mutations must not be counted as failures.
        store.GetAll();
        store.Get(existing.Id);
        store.Update(existing.Id, "New title", null, Priority.Low);
        store.Complete(existing.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Create("Another item", null, Priority.High, DateTimeOffset.UnixEpoch);
        store.Delete(existing.Id);

        var failures = metrics.All("todo.store.failures");
        failures.Count.ShouldBe(4);
        failures.Select(failure => failure.Tag("todo.operation"))
            .ShouldBe(["get", "update", "complete", "delete"], ignoreOrder: true);

        foreach (var failure in failures)
        {
            failure.Value.ShouldBe(1L);
            failure.Unit.ShouldBe("{failure}");
            failure.Tag("todo.outcome").ShouldBe("not found");
        }
    }

    [Fact]
    public void EveryStoreOperation_RecordsADurationInSecondsWithTheOperationTag()
    {
        var inner = new InMemoryTodoStore();
        var seeded = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        store.GetAll();
        store.Get(seeded.Id);
        var created = store.Create("Buy oat milk", null, Priority.High, DateTimeOffset.UnixEpoch);
        store.Update(created.Id, "Buy soy milk", null, Priority.Low);
        store.Complete(created.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(created.Id);
        store.Get(42);

        var durations = metrics.All("todo.store.operation.duration");
        durations.Count.ShouldBe(7);
        durations.Count(duration => duration.Tag("todo.operation") is "get").ShouldBe(2);

        foreach (var operation in new[] { "list", "create", "update", "complete", "delete" })
        {
            durations.Count(duration => Equals(duration.Tag("todo.operation"), operation)).ShouldBe(1);
        }

        foreach (var duration in durations)
        {
            duration.Unit.ShouldBe("s");
            var seconds = duration.Value.ShouldBeOfType<double>();
            seconds.ShouldBeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task ConcurrentCompletions_RecordOneItemCompleted()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        var completions = Enumerable.Range(0, 32)
            .Select(hour => Task.Run(() => store.Complete(
                item.Id, DateTimeOffset.UnixEpoch.AddHours(hour))))
            .ToArray();

        var results = await Task.WhenAll(completions);

        results.Count(result => result is CompletionResult.Completed).ShouldBe(1);
        results.Count(result => result is CompletionResult.AlreadyDone).ShouldBe(31);
        metrics.Single("todo.items.completed").Value.ShouldBe(1L);
    }

    [Fact]
    public void Gauges_ReportTheCurrentTotalAndDoneCounts()
    {
        var inner = new InMemoryTodoStore();
        var open = inner.Create("Buy milk", null, Priority.Low, DateTimeOffset.UnixEpoch);
        var done = inner.Create("Ship the demo", null, Priority.High, DateTimeOffset.UnixEpoch);
        inner.Complete(done.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(InstrumentedTodoStore.MeterName);

        metrics.ObserveGauges();

        var total = metrics.Single("todo.items.total");
        total.Value.ShouldBe(2L);
        total.Unit.ShouldBe("{item}");

        var doneCount = metrics.Single("todo.items.done");
        doneCount.Value.ShouldBe(1L);
        doneCount.Unit.ShouldBe("{item}");

        store.Create("Buy oat milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        store.Complete(open.Id, DateTimeOffset.UnixEpoch.AddHours(2));
        store.Delete(done.Id);

        metrics.ObserveGauges();

        metrics.All("todo.items.total").Last().Value.ShouldBe(2L);
        metrics.All("todo.items.done").Last().Value.ShouldBe(1L);
    }
}
