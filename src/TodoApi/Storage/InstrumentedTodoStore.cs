using System.Diagnostics;
using System.Diagnostics.Metrics;
using TodoApi.Domain;

namespace TodoApi.Storage;

/// <summary>
/// Decorates an <see cref="ITodoStore"/> with one Activity and one set of
/// measurements per Todos Store operation, so a request trace shows which
/// store operation ran and how it ended, while metrics summarise Todo Item
/// lifecycle activity without recording Todo Item title or description.
/// </summary>
/// <remarks>
/// Span names: <c>todos.list</c>, <c>todos.get</c>, <c>todos.create</c>,
/// <c>todos.update</c>, <c>todos.complete</c>, <c>todos.delete</c>.
/// Tags: <c>todo.operation</c> (list/get/create/update/complete/delete),
/// <c>todo.id</c> (when an id is involved), <c>todo.priority</c> (lowercase
/// low/medium/high, when an item or priority is involved), and
/// <c>todo.outcome</c> (found/not found/created/updated/completed/already
/// done/deleted).
/// <para>
/// Meter <c>TodoApi.Todos</c>: <c>todo.items.created</c> (Counter, item,
/// <c>todo.priority</c>), <c>todo.items.completed</c> (Counter, item),
/// <c>todo.items.deleted</c> (Counter, item), <c>todo.store.failures</c>
/// (Counter, failure, <c>todo.operation</c> + <c>todo.outcome</c>),
/// <c>todo.items.total</c> and <c>todo.items.done</c> (ObservableGauge, item,
/// read from the raw store), and <c>todo.store.operation.duration</c>
/// (Histogram, seconds, <c>todo.operation</c>).
/// </para>
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

    private const string OperationTag = "todo.operation";
    private const string IdTag = "todo.id";
    private const string PriorityTag = "todo.priority";
    private const string OutcomeTag = "todo.outcome";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly InMemoryTodoStore inner;
    private readonly object completionGate = new();
    private readonly Meter meter;
    private readonly Counter<long> createdItems;
    private readonly Counter<long> completedItems;
    private readonly Counter<long> deletedItems;
    private readonly Counter<long> failedOperations;
    private readonly Histogram<double> operationDuration;

    public InstrumentedTodoStore(InMemoryTodoStore inner)
    {
        this.inner = inner;
        meter = new Meter(MeterName);
        createdItems = meter.CreateCounter<long>(
            "todo.items.created",
            unit: "{item}",
            description: "Todo Items created, tagged by Priority.");
        deletedItems = meter.CreateCounter<long>(
            "todo.items.deleted",
            unit: "{item}",
            description: "Todo Items deleted from the Todos Store.");
        completedItems = meter.CreateCounter<long>(
            "todo.items.completed",
            unit: "{item}",
            description: "Todo Items that transitioned from not Done to Done.");
        failedOperations = meter.CreateCounter<long>(
            "todo.store.failures",
            unit: "{failure}",
            description: "Todos Store operations that ended without applying their change.");
        operationDuration = meter.CreateHistogram<double>(
            "todo.store.operation.duration",
            unit: "s",
            description: "Duration of every Todos Store operation.");
        meter.CreateObservableGauge(
            "todo.items.total",
            ObserveTotalItems,
            unit: "{item}",
            description: "Current number of Todo Items in the Todos Store.");
        meter.CreateObservableGauge(
            "todo.items.done",
            ObserveDoneItems,
            unit: "{item}",
            description: "Current number of Done Todo Items in the Todos Store.");
    }

    public void Dispose() => meter.Dispose();

    public IReadOnlyList<TodoItem> GetAll()
    {
        using var timing = new OperationTiming(operationDuration, "list");
        using var activity = Source.StartActivity("todos.list", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "list");

        var items = inner.GetAll();
        activity?.SetTag(OutcomeTag, "found");

        return items;
    }

    public TodoItem? Get(int id)
    {
        using var timing = new OperationTiming(operationDuration, "get");
        using var activity = Source.StartActivity("todos.get", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "get");
        activity?.SetTag(IdTag, id);

        var item = LookUpWithPriority(activity, id);
        var outcome = item is null ? "not found" : "found";
        activity?.SetTag(OutcomeTag, outcome);

        if (item is null)
        {
            RecordFailure("get", outcome);
        }

        return item;
    }

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        using var timing = new OperationTiming(operationDuration, "create");
        using var activity = Source.StartActivity("todos.create", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "create");
        activity?.SetTag(PriorityTag, FormatPriority(priority));

        var item = inner.Create(title, description, priority, createdAt);
        createdItems.Add(1, new KeyValuePair<string, object?>(PriorityTag, FormatPriority(item.Priority)));
        activity?.SetTag(IdTag, item.Id);
        activity?.SetTag(OutcomeTag, "created");

        return item;
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        using var timing = new OperationTiming(operationDuration, "update");
        using var activity = Source.StartActivity("todos.update", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "update");
        activity?.SetTag(IdTag, id);
        activity?.SetTag(PriorityTag, FormatPriority(priority));

        var item = inner.Update(id, title, description, priority);
        var outcome = item is null ? "not found" : "updated";
        activity?.SetTag(OutcomeTag, outcome);

        if (item is null)
        {
            RecordFailure("update", outcome);
        }

        return item;
    }

    public TodoItem? Complete(int id, DateTimeOffset completedAt)
    {
        using var timing = new OperationTiming(operationDuration, "complete");
        using var activity = Source.StartActivity("todos.complete", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "complete");
        activity?.SetTag(IdTag, id);

        TodoItem? existing;
        TodoItem? item;
        lock (completionGate)
        {
            existing = LookUpWithPriority(activity, id);
            item = inner.Complete(id, completedAt);
        }

        var outcome = existing switch
        {
            null => "not found",
            { Done: true } => "already done",
            _ => "completed"
        };
        activity?.SetTag(OutcomeTag, outcome);

        if (existing is { Done: false } && item is { Done: true })
        {
            completedItems.Add(1);
        }

        if (outcome == "not found")
        {
            RecordFailure("complete", outcome);
        }

        return item;
    }

    public TodoItem? Delete(int id)
    {
        using var timing = new OperationTiming(operationDuration, "delete");
        using var activity = Source.StartActivity("todos.delete", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "delete");
        activity?.SetTag(IdTag, id);

        var deleted = inner.Delete(id);
        if (deleted is not null)
        {
            deletedItems.Add(1);
            activity?.SetTag(PriorityTag, FormatPriority(deleted.Priority));
        }
        else
        {
            RecordFailure("delete", "not found");
        }

        activity?.SetTag(OutcomeTag, deleted is not null ? "deleted" : "not found");

        return deleted;
    }

    private TodoItem? LookUpWithPriority(Activity? activity, int id)
    {
        var item = inner.Get(id);
        if (item is not null)
        {
            activity?.SetTag(PriorityTag, FormatPriority(item.Priority));
        }

        return item;
    }

    private long ObserveTotalItems() => inner.GetAll().Count;

    private long ObserveDoneItems() => inner.GetAll().Count(item => item.Done);

    private void RecordFailure(string operation, string outcome) =>
        failedOperations.Add(
            1,
            new KeyValuePair<string, object?>(OperationTag, operation),
            new KeyValuePair<string, object?>(OutcomeTag, outcome));

    private static string FormatPriority(Priority priority) => priority.ToString().ToLowerInvariant();

    /// <summary>Records the operation duration on dispose, including when the operation throws.</summary>
    private readonly struct OperationTiming : IDisposable
    {
        private readonly Histogram<double> histogram;
        private readonly string operation;
        private readonly long startedAt;

        public OperationTiming(Histogram<double> histogram, string operation)
        {
            this.histogram = histogram;
            this.operation = operation;
            startedAt = Stopwatch.GetTimestamp();
        }

        public void Dispose() =>
            histogram.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>(OperationTag, operation));
    }
}
