using System.Diagnostics;
using System.Diagnostics.Metrics;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Telemetry;

namespace TodoApi.Storage;

/// <summary>
/// Decorates an <see cref="ITodoStore"/> with one Activity, one set of
/// measurements, and one outcome log record per Todos Store operation, so a
/// request trace shows which store operation ran and how it ended, metrics
/// summarise Todo Item lifecycle activity, and every caller gets the same
/// outcome logs, all without recording Todo Item title or description.
/// </summary>
/// <remarks>
/// The operation names, tag keys, instrument names, units, and outcome values
/// come from <see cref="TodoOperation"/> and <see cref="TodoTelemetry"/>.
/// Every operation emits exactly one span, one duration measurement, and one
/// <see cref="TodoLogs"/> outcome record under <see cref="TodoLogCategory"/>
/// (ADR-0006). No span or log ever carries a Todo Item title or description.
/// Callers log only what the store cannot know: validation rejections.
/// <para>
/// <c>todo.items.completed</c> advances only for a real not-Done to Done
/// transition; replaying a Completion is reported as <c>already_done</c> and
/// does not increment it.
/// </para>
/// </remarks>
public sealed class InstrumentedTodoStore : ITodoStore
{
    private static readonly ActivitySource Source = new(TodoTelemetry.ActivitySourceName);

    private readonly ITodoStore _inner;
    private readonly ILogger<TodoLogCategory> _logger;
    private readonly Counter<long> _createdItems;
    private readonly Counter<long> _completedItems;
    private readonly Counter<long> _deletedItems;
    private readonly Counter<long> _failedOperations;
    private readonly Histogram<double> _operationDuration;

    /// <param name="inner">The Todos Store that holds the state.</param>
    /// <param name="meterFactory">
    /// Creates the <see cref="TodoTelemetry.MeterName"/> meter. The factory
    /// owns the meter's lifetime and scopes it to its container.
    /// </param>
    /// <param name="logger">Receives one outcome record per operation.</param>
    public InstrumentedTodoStore(
        ITodoStore inner,
        IMeterFactory meterFactory,
        ILogger<TodoLogCategory> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _logger = logger;
        var meter = meterFactory.Create(TodoTelemetry.MeterName);
        _createdItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsCreated,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items created, tagged by Priority.");
        _deletedItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsDeleted,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items deleted from the Todos Store.");
        _completedItems = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.ItemsCompleted,
            unit: TodoTelemetry.Units.Items,
            description: "Todo Items that transitioned from not Done to Done.");
        _failedOperations = meter.CreateCounter<long>(
            TodoTelemetry.Instruments.StoreFailures,
            unit: TodoTelemetry.Units.Failures,
            description: "Todos Store operations that ended without applying their change.");
        _operationDuration = meter.CreateHistogram<double>(
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

    public IReadOnlyList<TodoItem> GetAll()
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.List);
        using var activity = Source.StartActivity(TodoOperation.List.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.List.Name);

        var items = _inner.GetAll();
        activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Found);
        TodoLogs.ListSucceeded(_logger, TodoOperation.List.Name, items.Count);

        return items;
    }

    public TodoItem? Get(int id)
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.Get);
        using var activity = Source.StartActivity(TodoOperation.Get.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Get.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        var item = LookUpWithPriority(activity, id);
        var outcome = item is null ? TodoTelemetry.Outcomes.NotFound : TodoTelemetry.Outcomes.Found;
        activity?.SetTag(TodoTelemetry.Tags.Outcome, outcome);

        if (item is null)
        {
            RecordFailure(TodoOperation.Get, outcome);
            TodoLogs.NotFound(_logger, id, TodoOperation.Get.Name);
        }
        else
        {
            TodoLogs.GetSucceeded(_logger, TodoOperation.Get.Name, item.Id);
        }

        return item;
    }

    public TodoItem Create(TodoDraft draft, DateTimeOffset createdAt)
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.Create);
        using var activity = Source.StartActivity(TodoOperation.Create.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Create.Name);
        activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(draft.Priority));

        var item = _inner.Create(draft, createdAt);
        _createdItems.Add(1, new KeyValuePair<string, object?>(
            TodoTelemetry.Tags.Priority, FormatPriority(item.Priority)));
        activity?.SetTag(TodoTelemetry.Tags.Id, item.Id);
        activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Created);
        TodoLogs.CreateSucceeded(_logger, TodoOperation.Create.Name, item.Id, item.Priority);

        return item;
    }

    public TodoItem? Update(int id, TodoDraft draft)
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.Update);
        using var activity = Source.StartActivity(TodoOperation.Update.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Update.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);
        activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(draft.Priority));

        var item = _inner.Update(id, draft);
        var outcome = item is null ? TodoTelemetry.Outcomes.NotFound : TodoTelemetry.Outcomes.Updated;
        activity?.SetTag(TodoTelemetry.Tags.Outcome, outcome);

        if (item is null)
        {
            RecordFailure(TodoOperation.Update, outcome);
            TodoLogs.NotFound(_logger, id, TodoOperation.Update.Name);
        }
        else
        {
            TodoLogs.UpdateSucceeded(_logger, TodoOperation.Update.Name, item.Id, item.Priority);
        }

        return item;
    }

    public CompletionResult Complete(int id, DateTimeOffset completedAt)
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.Complete);
        using var activity = Source.StartActivity(TodoOperation.Complete.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Complete.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        CompletionResult result = _inner.Complete(id, completedAt);

        switch (result)
        {
            case CompletionResult.NotFound:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.NotFound);
                RecordFailure(TodoOperation.Complete, TodoTelemetry.Outcomes.NotFound);
                TodoLogs.NotFound(_logger, id, TodoOperation.Complete.Name);
                break;
            case CompletionResult.Completed completed:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Completed);
                activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(completed.Item.Priority));
                _completedItems.Add(1);
                TodoLogs.CompleteSucceeded(
                    _logger, TodoOperation.Complete.Name, completed.Item.Id, completed.Item.Priority);
                break;
            case CompletionResult.AlreadyDone alreadyDone:
                activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.AlreadyDone);
                activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(alreadyDone.Item.Priority));
                TodoLogs.CompleteAlreadyDone(_logger, TodoOperation.Complete.Name, alreadyDone.Item.Id);
                break;
        }

        return result;
    }

    public TodoItem? Delete(int id)
    {
        using var timing = new OperationTiming(_operationDuration, TodoOperation.Delete);
        using var activity = Source.StartActivity(TodoOperation.Delete.SpanName, ActivityKind.Internal);
        activity?.SetTag(TodoTelemetry.Tags.Operation, TodoOperation.Delete.Name);
        activity?.SetTag(TodoTelemetry.Tags.Id, id);

        var deleted = _inner.Delete(id);
        if (deleted is not null)
        {
            _deletedItems.Add(1);
            activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(deleted.Priority));
            activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.Deleted);
            TodoLogs.DeleteSucceeded(_logger, TodoOperation.Delete.Name, deleted.Id, deleted.Priority);
        }
        else
        {
            activity?.SetTag(TodoTelemetry.Tags.Outcome, TodoTelemetry.Outcomes.NotFound);
            RecordFailure(TodoOperation.Delete, TodoTelemetry.Outcomes.NotFound);
            TodoLogs.NotFound(_logger, id, TodoOperation.Delete.Name);
        }

        return deleted;
    }

    private TodoItem? LookUpWithPriority(Activity? activity, int id)
    {
        var item = _inner.Get(id);
        if (item is not null)
        {
            activity?.SetTag(TodoTelemetry.Tags.Priority, FormatPriority(item.Priority));
        }

        return item;
    }

    private long ObserveTotalItems() => _inner.GetAll().Count;

    private long ObserveDoneItems() => _inner.GetAll().Count(item => item.Done);

    private void RecordFailure(TodoOperation operation, string outcome) =>
        _failedOperations.Add(
            1,
            new KeyValuePair<string, object?>(TodoTelemetry.Tags.Operation, operation.Name),
            new KeyValuePair<string, object?>(TodoTelemetry.Tags.Outcome, outcome));

    private static string FormatPriority(Priority priority) => priority.ToString().ToLowerInvariant();

    /// <summary>Records the operation duration on dispose, including when the operation throws.</summary>
    private readonly struct OperationTiming : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly TodoOperation _operation;
        private readonly long _startedAt;

        public OperationTiming(Histogram<double> histogram, TodoOperation operation)
        {
            _histogram = histogram;
            _operation = operation;
            _startedAt = Stopwatch.GetTimestamp();
        }

        public void Dispose() =>
            _histogram.Record(
                Stopwatch.GetElapsedTime(_startedAt).TotalSeconds,
                new KeyValuePair<string, object?>(TodoTelemetry.Tags.Operation, _operation.Name));
    }
}
