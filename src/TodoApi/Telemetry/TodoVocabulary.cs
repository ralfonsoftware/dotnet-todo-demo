namespace TodoApi.Telemetry;

/// <summary>
/// One Todos Store operation. The name is the single source for the
/// <c>todo.operation</c> tag, the <c>Operation</c> log property, the operation
/// segment of the span name, and metric tags.
/// </summary>
public sealed record TodoOperation
{
    public static readonly TodoOperation List = new("list");
    public static readonly TodoOperation Get = new("get");
    public static readonly TodoOperation Create = new("create");
    public static readonly TodoOperation Update = new("update");
    public static readonly TodoOperation Complete = new("complete");
    public static readonly TodoOperation Delete = new("delete");

    private TodoOperation(string name) => Name = name;

    public string Name { get; }

    public string SpanName => $"todos.{Name}";

    public override string ToString() => Name;
}

/// <summary>
/// The Todo observability schema: the activity source and meter names, the tag keys, instrument names, units, and
/// outcome values shared by spans, metrics, and logs. Only the contract test
/// pins the literal strings; everything else references these constants.
/// </summary>
public static class TodoTelemetry
{
    /// <summary>The name of the activity source that emits Todos Store spans.</summary>
    public const string ActivitySourceName = "TodoApi.TodosStore";

    /// <summary>The name of the meter that emits Todo Item lifecycle metrics.</summary>
    public const string MeterName = "TodoApi.Todos";

    public static class Tags
    {
        public const string Operation = "todo.operation";
        public const string Id = "todo.id";
        public const string Priority = "todo.priority";
        public const string Outcome = "todo.outcome";
    }

    public static class Instruments
    {
        public const string ItemsCreated = "todo.items.created";
        public const string ItemsCompleted = "todo.items.completed";
        public const string ItemsDeleted = "todo.items.deleted";
        public const string StoreFailures = "todo.store.failures";
        public const string ItemsTotal = "todo.items.total";
        public const string ItemsDone = "todo.items.done";
        public const string OperationDuration = "todo.store.operation.duration";
    }

    public static class Units
    {
        public const string Items = "{item}";
        public const string Failures = "{failure}";
        public const string Seconds = "s";
    }

    public static class Outcomes
    {
        public const string Found = "found";
        public const string NotFound = "not found";
        public const string Created = "created";
        public const string Updated = "updated";
        public const string Completed = "completed";
        public const string AlreadyDone = "already done";
        public const string Deleted = "deleted";
    }
}
