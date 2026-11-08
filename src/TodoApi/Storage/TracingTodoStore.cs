using System.Diagnostics;
using TodoApi.Domain;

namespace TodoApi.Storage;

/// <summary>
/// Decorates an <see cref="ITodoStore"/> with one Activity per Todos Store
/// operation, so a request trace shows which store operation ran and how it
/// ended without recording Todo Item title or description.
/// </summary>
/// <remarks>
/// Span names: <c>todos.list</c>, <c>todos.get</c>, <c>todos.create</c>,
/// <c>todos.update</c>, <c>todos.complete</c>, <c>todos.delete</c>.
/// Tags: <c>todo.operation</c> (list/get/create/update/complete/delete),
/// <c>todo.id</c> (when an id is involved), <c>todo.priority</c> (lowercase
/// low/medium/high, when an item or priority is involved), and
/// <c>todo.outcome</c> (found/not found/created/updated/completed/already
/// done/deleted).
/// </remarks>
public sealed class TracingTodoStore(InMemoryTodoStore inner) : ITodoStore
{
    public const string ActivitySourceName = "TodoApi.TodosStore";

    private const string OperationTag = "todo.operation";
    private const string IdTag = "todo.id";
    private const string PriorityTag = "todo.priority";
    private const string OutcomeTag = "todo.outcome";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    public IReadOnlyList<TodoItem> GetAll()
    {
        using var activity = Source.StartActivity("todos.list", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "list");

        var items = inner.GetAll();
        activity?.SetTag(OutcomeTag, "found");

        return items;
    }

    public TodoItem? Get(int id)
    {
        using var activity = Source.StartActivity("todos.get", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "get");
        activity?.SetTag(IdTag, id);

        var item = inner.Get(id);
        if (item is not null)
        {
            activity?.SetTag(PriorityTag, Format(item.Priority));
        }

        activity?.SetTag(OutcomeTag, item is null ? "not found" : "found");
        return item;
    }

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        using var activity = Source.StartActivity("todos.create", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "create");
        activity?.SetTag(PriorityTag, Format(priority));

        var item = inner.Create(title, description, priority, createdAt);
        activity?.SetTag(IdTag, item.Id);
        activity?.SetTag(OutcomeTag, "created");

        return item;
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        using var activity = Source.StartActivity("todos.update", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "update");
        activity?.SetTag(IdTag, id);
        activity?.SetTag(PriorityTag, Format(priority));

        var item = inner.Update(id, title, description, priority);
        activity?.SetTag(OutcomeTag, item is null ? "not found" : "updated");

        return item;
    }

    public TodoItem? Complete(int id, DateTimeOffset completedAt)
    {
        using var activity = Source.StartActivity("todos.complete", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "complete");
        activity?.SetTag(IdTag, id);

        var existing = inner.Get(id);
        if (existing is not null)
        {
            activity?.SetTag(PriorityTag, Format(existing.Priority));
        }

        var item = inner.Complete(id, completedAt);
        activity?.SetTag(OutcomeTag, existing switch
        {
            null => "not found",
            { Done: true } => "already done",
            _ => "completed"
        });

        return item;
    }

    public bool Delete(int id)
    {
        using var activity = Source.StartActivity("todos.delete", ActivityKind.Internal);
        activity?.SetTag(OperationTag, "delete");
        activity?.SetTag(IdTag, id);

        var existing = inner.Get(id);
        if (existing is not null)
        {
            activity?.SetTag(PriorityTag, Format(existing.Priority));
        }

        var deleted = inner.Delete(id);
        activity?.SetTag(OutcomeTag, deleted ? "deleted" : "not found");

        return deleted;
    }

    private static string Format(Priority priority) => priority.ToString().ToLowerInvariant();
}
