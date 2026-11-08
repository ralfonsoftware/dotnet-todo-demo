using System.Diagnostics;
using System.Diagnostics.Metrics;
using TodoApi.Domain;
using TodoApi.Telemetry;

namespace TodoApi.Storage;

/// <summary>
/// Decorates an <see cref="ITodoStore"/> with one Activity and one set of
/// measurements per Todos Store operation, so a request trace shows which
/// store operation ran and how it ended, while metrics summarise Todo Item
/// lifecycle activity without recording Todo Item title or description.
/// </summary>
/// <remarks>
/// The operation names, tag keys, instrument names, units, and outcome values
/// come from <see cref="TodoOperation"/> and <see cref="TodoTelemetry"/>.
/// Every operation emits exactly one span and one duration measurement, and no
/// span ever tags a Todo Item title or description.
/// <para>
/// <c>todo.items.completed</c> advances only for a real not-Done to Done
/// transition; replaying a Completion is reported as <c>already done</c> and
/// does not increment it.
/// </para>
/// </remarks>
public sealed class InstrumentedTodoStore : ITodoStore, IDisposable
{
    /// <summary>The name of the activity source that emits Todos Store spans.</summary>
    public const string ActivitySourceName = "TodoApi.TodosStore";

    /// <summary>The name of the meter that emits Todo Item lifecycle metrics.</summary>
    public const string MeterName = "TodoApi.Todos";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly ITodoStore inner;
    private readonly Meter meter;
    private readonly Counter<long> createdItems;
    private readonly Counter<long> completedItems;
    private readonly Counter<long> deletedItems;
    private readonly Counter<long> failedOperations;
    private readonly Histogram<double> operationDuration;

    public InstrumentedTodoStore(ITodoStore inner)
    {
        this.inner = inner;
        meter = new Meter(MeterName);
        createdItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsCreated,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items created, tagged by Priority.");
        deletedItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsDeleted,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items deleted from the Todos Store.");
        completedItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsCompleted,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items that transitioned from not Done to Done.");
        failedOperations = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.StoreFailures,
            unit: TodoTelemetry.Units.Failures,
            description: "Todos Store operations that ended without applying their change.");
        operationDuration = meter.CreateHistogram<double>(
            TodoTelemetry.Instruments.OperationDuration,
            unit: TodoTelemetry.Units.Seconds,
            description: "Duration of every Todos Store operation.");
        meter.CreateObservableGauge(
            TodoTelemetry.Instruments.ItemsTotal,
            ObserveTotalItems,
            unit: TodoTelemetry.Units.Items,
            description: "Current number of Todo Items in the Todos Store.");
        meter.CreateObservableGauge(
            TodoTelemetry.Instruments.ItemsDone,
            ObserveDoneItems,
            unit: TodoTelemetry.Units.Items,
            description: "Current number of Done Todo Items in the Todos Store.");
    }

    public void Dispose() => meter.Dispose();

    public IReadOnlyList<TodoItem> GetAll()
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.List);
        using var activity = Source.StartActivity(TodoOperation.List.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.List.Name);

        var items = inner.GetAll();
        activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Found);

        return items;
    }

    public TodoItem? Get(int id)
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.Get);
        using var activity = Source.StartActivity(TodoOperation.Get.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Get.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        var item = LookUpWithPriority(activity, id);
        var outcome = item is null ? TodoTelemetry.Outcomes.NotFound : TodoTelemetry.Outcomes.Found;
        activity?.SetTag(TodoTelemetry.Tags.Outcome, outcome);

        if (item is null)
        {
            RecordFailure(TodoOperation.Get, outcome);
        }

        return item;
    }

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.Create);
        using var activity = Source.StartActivity(TodoOperation.Create.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Create.Name);
        activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(priority));

        var item = inner.Create(title, description, priority, createdAt);
        createdItems.Add(1, new KeyValuePair<string, object?>(
            TodoTelemetry.Tags.Priority, FormatPriority(item.Priority)));
        activity?.SetTag(TodoTelemetry.Tags.Id, item.Id);
        activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Created);

        return item;
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.Update);
        using var activity = Source.StartActivity(TodoOperation.Update.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Update.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);
        activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(priority));

        var item = inner.Update(id, title, description, priority);
        var outcome = item is null ? TodoTelemetry.Outcomes.NotFound : TodoTelemetry.Outcomes.Updated;
        activity?.SetTag(TodoTelemetry.Tags.Outcome, outcome);

        if (item is null)
        {
            RecordFailure(TodoOperation.Update, outcome);
        }

        return item;
    }

    public CompletionResult Complete(int id, DateTimeOffset completedAt)
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.Complete);
        using var activity = Source.StartActivity(TodoOperation.Complete.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Complete.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        CompletionResult result = inner.Complete(id, completedAt);

        switch (result)
        {
            case CompletionResult.NotFound:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.NotFound);
                RecordFailure(TodoOperation.Complete, TodoTelemetry.Outcomes.NotFound);
                break;
            case CompletionResult.Completed completed:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Completed);
                activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(completed.Item.Priority));
                completedItems.Add(1);
                break;
            case CompletionResult.AlreadyDone alreadyDone:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.AlreadyDone);
                activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(alreadyDone.Item.Priority));
                break;
        }

        return result;
    }

    public TodoItem? Delete(int id)
    {
        using var timing = new OperationTiming(operationDuration, TodoOperation.Delete);
        using var activity = Source.StartActivity(TodoOperation.Delete.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Delete.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        var deleted = inner.Delete(id);
        if (deleted is not null)
        {
            deletedItems.Add(1);
            activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(deleted.Priority));
        }
        else
        {
            RecordFailure(TodoOperation.Delete, TodoTelemetry.Outcomes.NotFound);
        }

        activity?.SetTag(
            TodoTelemetry.Tags.Outcome,
            deleted is not null ? TodoTelemetry.Outcomes.Deleted : TodoTelemetry.Outcomes.NotFound);

        return deleted;
    }

    private TodoItem? LookUpWithPriority(Activity? activity, int id)
    {
        var item = inner.Get(id);
        if (item is not null)
        {
            activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(item.Priority));
        }

        return item;
    }

    private long ObserveTotalItems() => inner.GetAll().Count;

    private long ObserveDoneItems() => inner.GetAll().Count(item => item.Done);

    private void RecordFailure(TodoOperation operation, string outcome) =>
        failedOperations.Add(
            1,
            new KeyValuePair<string, object?>(TodoTelemetry.Tags.Operation, operation.Name),
            new KeyValuePair<string, object?>(TodoTelemetry.Tags.Outcome, outcome));

    private static string FormatPriority(Priority priority) => priority.ToString().ToLowerInvariant();

    /// <summary>Records the operation duration on dispose, including when the operation throws.</summary>
    private readonly struct OperationTiming : IDisposable
    {
        private readonly Histogram<double> histogram;
        private readonly TodoOperation operation;
        private readonly long startedAt;

        public OperationTiming(Histogram<double> histogram, TodoOperation operation)
        {
            this.histogram = histogram;
            this.operation = operation;
            startedAt = Stopwatch.GetTimestamp();
        }

        public void Dispose() =>
            histogram.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>(TodoTelemetry.Tags.Operation, operation.Name));
    }
}
