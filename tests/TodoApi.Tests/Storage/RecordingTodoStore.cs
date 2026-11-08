using System.Collections.Concurrent;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Tests.Storage;

/// <summary>
/// An <see cref="ITodoStore"/> adapter that delegates to an
/// <see cref="InMemoryTodoStore"/> and records every operation name, proving
/// that the observability decorator works over any Todos Store implementation.
/// </summary>
internal sealed class RecordingTodoStore : ITodoStore
{
    private readonly InMemoryTodoStore inner = new();
    private readonly ConcurrentQueue<string> calls = new();

    public IReadOnlyList<string> Calls => calls.ToArray();

    public IReadOnlyList<TodoItem> GetAll()
    {
        calls.Enqueue("list");
        return inner.GetAll();
    }

    public TodoItem? Get(int id)
    {
        calls.Enqueue("get");
        return inner.Get(id);
    }

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        calls.Enqueue("create");
        return inner.Create(title, description, priority, createdAt);
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        calls.Enqueue("update");
        return inner.Update(id, title, description, priority);
    }

    public CompletionResult Complete(int id, DateTimeOffset completedAt)
    {
        calls.Enqueue("complete");
        return inner.Complete(id, completedAt);
    }

    public TodoItem? Delete(int id)
    {
        calls.Enqueue("delete");
        return inner.Delete(id);
    }
}
