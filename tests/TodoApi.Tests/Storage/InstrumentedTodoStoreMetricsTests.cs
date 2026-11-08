using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Storage;

[Collection(TracingCollection.Name)]
public sealed class InstrumentedTodoStoreMetricsTests
{
    [Fact]
    public void Create_RecordsOneItemCreatedPerPriority()
    {
        using var store = new InstrumentedTodoStore(new InMemoryTodoStore());
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        store.Create(Drafts.Of("Low item", null, Priority.Low), DateTimeOffset.UnixEpoch);
        store.Create(Drafts.Of("Medium item", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        store.Create(Drafts.Of("High item", null, Priority.High), DateTimeOffset.UnixEpoch);

        var created = metrics.All(TodoTelemetry.Instruments.ItemsCreated);
        created.Count.ShouldBe(3);
        created.Select(measurement => measurement.Tag(TodoTelemetry.Tags.Priority))
            .ShouldBe(["low", "medium", "high"], ignoreOrder: true);
        created.ShouldAllBe(measurement => (long)measurement.Value == 1L);
        created.ShouldAllBe(measurement => measurement.Unit == TodoTelemetry.Units.Items);

        foreach (var measurement in created)
        {
            measurement.Tags.Keys.ShouldBe([TodoTelemetry.Tags.Priority]);
        }
    }

    [Fact]
    public void Delete_RecordsOneItemDeletedOnlyWhenAnItemWasRemoved()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        store.Delete(item.Id).ShouldNotBeNull();
        store.Delete(item.Id).ShouldBeNull();

        var deleted = metrics.Single(TodoTelemetry.Instruments.ItemsDeleted);
        deleted.Value.ShouldBe(1L);
        deleted.Unit.ShouldBe(TodoTelemetry.Units.Items);
        deleted.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_RecordsOneItemCompletedOnlyForARealTransition()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1))
            .ShouldBeOfType<CompletionResult.Completed>();
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(2))
            .ShouldBeOfType<CompletionResult.AlreadyDone>();
        store.Complete(42, DateTimeOffset.UnixEpoch.AddHours(3))
            .ShouldBeOfType<CompletionResult.NotFound>();

        var completed = metrics.Single(TodoTelemetry.Instruments.ItemsCompleted);
        completed.Value.ShouldBe(1L);
        completed.Unit.ShouldBe(TodoTelemetry.Units.Items);
        completed.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void FailedStoreOperations_AreCountedOnceWithOperationAndOutcome()
    {
        var inner = new InMemoryTodoStore();
        var existing = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        store.Get(42).ShouldBeNull();
        store.Update(42, Drafts.Of("New title", null, Priority.Low)).ShouldBeNull();
        store.Complete(42, DateTimeOffset.UnixEpoch)
            .ShouldBeOfType<CompletionResult.NotFound>();
        store.Delete(42).ShouldBeNull();

        // Successful reads and mutations must not be counted as failures.
        store.GetAll();
        store.Get(existing.Id);
        store.Update(existing.Id, Drafts.Of("New title", null, Priority.Low));
        store.Complete(existing.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Create(Drafts.Of("Another item", null, Priority.High), DateTimeOffset.UnixEpoch);
        store.Delete(existing.Id);

        var failures = metrics.All(TodoTelemetry.Instruments.StoreFailures);
        failures.Count.ShouldBe(4);
        failures.Select(failure => failure.Tag(TodoTelemetry.Tags.Operation))
            .ShouldBe(
                [
                    TodoOperation.Get.Name,
                    TodoOperation.Update.Name,
                    TodoOperation.Complete.Name,
                    TodoOperation.Delete.Name
                ],
                ignoreOrder: true);

        foreach (var failure in failures)
        {
            failure.Value.ShouldBe(1L);
            failure.Unit.ShouldBe(TodoTelemetry.Units.Failures);
            failure.Tag(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);
        }
    }

    [Fact]
    public void EveryStoreOperation_RecordsADurationInSecondsWithTheOperationTag()
    {
        var inner = new InMemoryTodoStore();
        var seeded = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        store.GetAll();
        store.Get(seeded.Id);
        var created = store.Create(Drafts.Of("Buy oat milk", null, Priority.High), DateTimeOffset.UnixEpoch);
        store.Update(created.Id, Drafts.Of("Buy soy milk", null, Priority.Low));
        store.Complete(created.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(created.Id);
        store.Get(42);

        var durations = metrics.All(TodoTelemetry.Instruments.OperationDuration);
        durations.Count.ShouldBe(7);
        durations.Count(duration => Equals(
            duration.Tag(TodoTelemetry.Tags.Operation), TodoOperation.Get.Name)).ShouldBe(2);

        foreach (var operation in new[]
        {
            TodoOperation.List,
            TodoOperation.Create,
            TodoOperation.Update,
            TodoOperation.Complete,
            TodoOperation.Delete
        })
        {
            durations.Count(duration => Equals(
                duration.Tag(TodoTelemetry.Tags.Operation), operation.Name)).ShouldBe(1);
        }

        foreach (var duration in durations)
        {
            duration.Unit.ShouldBe(TodoTelemetry.Units.Seconds);
            var seconds = duration.Value.ShouldBeOfType<double>();
            seconds.ShouldBeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task ConcurrentCompletions_RecordOneItemCompleted()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        var completions = Enumerable.Range(0, 32)
            .Select(hour => Task.Run(() => store.Complete(
                item.Id, DateTimeOffset.UnixEpoch.AddHours(hour))))
            .ToArray();

        var results = await Task.WhenAll(completions);

        results.Count(result => result is CompletionResult.Completed).ShouldBe(1);
        results.Count(result => result is CompletionResult.AlreadyDone).ShouldBe(31);
        metrics.Single(TodoTelemetry.Instruments.ItemsCompleted).Value.ShouldBe(1L);
    }

    [Fact]
    public void Gauges_ReportTheCurrentTotalAndDoneCounts()
    {
        var inner = new InMemoryTodoStore();
        var open = inner.Create(Drafts.Of("Buy milk", null, Priority.Low), DateTimeOffset.UnixEpoch);
        var done = inner.Create(Drafts.Of("Ship the demo", null, Priority.High), DateTimeOffset.UnixEpoch);
        inner.Complete(done.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        using var store = new InstrumentedTodoStore(inner);
        using var metrics = new MetricsCapture(TodoTelemetry.MeterName);

        metrics.ObserveGauges();

        var total = metrics.Single(TodoTelemetry.Instruments.ItemsTotal);
        total.Value.ShouldBe(2L);
        total.Unit.ShouldBe(TodoTelemetry.Units.Items);

        var doneCount = metrics.Single(TodoTelemetry.Instruments.ItemsDone);
        doneCount.Value.ShouldBe(1L);
        doneCount.Unit.ShouldBe(TodoTelemetry.Units.Items);

        store.Create(Drafts.Of("Buy oat milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        store.Complete(open.Id, DateTimeOffset.UnixEpoch.AddHours(2));
        store.Delete(done.Id);

        metrics.ObserveGauges();

        metrics.All(TodoTelemetry.Instruments.ItemsTotal)[^1].Value.ShouldBe(2L);
        metrics.All(TodoTelemetry.Instruments.ItemsDone)[^1].Value.ShouldBe(1L);
    }
}
