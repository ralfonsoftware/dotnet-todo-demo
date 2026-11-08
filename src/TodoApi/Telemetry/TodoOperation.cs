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
