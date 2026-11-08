using System.Collections.Concurrent;
using TodoApi.Domain;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Storage;

/// <summary>
/// An <see cref="ITodoStore"/> adapter that delegates to an
/// <see cref="InMemoryTodoStore"/> and records every operation name, proving
/// that the observability decorator works over any Todos Store implementation.
/// </summary>
internal sealed class RecordingTodoStore : ITodoStore
{
    private readonly InMemoryTodoStore _inner = new();
    private readonly ConcurrentQueue<string> _calls = new();

    public IReadOnlyList<string> Calls => _calls.ToArray();

    public IReadOnlyList<TodoItem> GetAll()
    {
        _calls.Enqueue(TodoOperation.List.Name);
        return _inner.GetAll();
    }

    public TodoItem? Get(int id)
    {
        _calls.Enqueue(TodoOperation.Get.Name);
        return _inner.Get(id);
    }

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        _calls.Enqueue(TodoOperation.Create.Name);
        return _inner.Create(title, description, priority, createdAt);
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        _calls.Enqueue(TodoOperation.Update.Name);
        return _inner.Update(id, title, description, priority);
    }

    public CompletionResult Complete(int id, DateTimeOffset completedAt)
    {
        _calls.Enqueue(TodoOperation.Complete.Name);
        return _inner.Complete(id, completedAt);
    }

    public TodoItem? Delete(int id)
    {
        _calls.Enqueue(TodoOperation.Delete.Name);
        return _inner.Delete(id);
    }
}
