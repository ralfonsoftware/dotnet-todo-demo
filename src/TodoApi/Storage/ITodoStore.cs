using TodoApi.Domain;

namespace TodoApi.Storage;

public interface ITodoStore
{
    IReadOnlyList<TodoItem> GetAll();

    TodoItem? Get(int id);

    TodoItem Create(string title, string? description, Priority priority, DateTimeOffset createdAt);

    TodoItem? Update(int id, string title, string? description, Priority priority);

    TodoItem? Complete(int id, DateTimeOffset completedAt);

    TodoItem? Delete(int id);
}
