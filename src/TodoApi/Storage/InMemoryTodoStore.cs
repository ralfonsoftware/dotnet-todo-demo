using System.Collections.Concurrent;
using TodoApi.Domain;

namespace TodoApi.Storage;

public sealed class InMemoryTodoStore : ITodoStore
{
    private readonly ConcurrentDictionary<int, TodoItem> _items = new();
    private readonly object _gate = new();
    private int _lastId;

    public IReadOnlyList<TodoItem> GetAll() =>
        _items.Values.OrderBy(item => item.Id).ToArray();

    public TodoItem? Get(int id) =>
        _items.TryGetValue(id, out var item) ? item : null;

    public TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        var id = Interlocked.Increment(ref _lastId);
        var item = new TodoItem
        {
            Id = id,
            Title = title,
            Description = description,
            CreatedAt = createdAt,
            Priority = priority
        };

        _items[id] = item;
        return item;
    }

    public TodoItem? Update(int id, string title, string? description, Priority priority)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var existing))
            {
                return null;
            }

            var updated = existing with
            {
                Title = title,
                Description = description,
                Priority = priority
            };

            _items[id] = updated;
            return updated;
        }
    }

    public CompletionResult Complete(int id, DateTimeOffset completedAt)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var existing))
            {
                return new CompletionResult.NotFound();
            }

            var completed = existing.Complete(completedAt);
            _items[id] = completed;

            return existing.Done
                ? new CompletionResult.AlreadyDone(completed)
                : new CompletionResult.Completed(completed);
        }
    }

    public TodoItem? Delete(int id)
    {
        lock (_gate)
        {
            return _items.TryRemove(id, out var item) ? item : null;
        }
    }
}
