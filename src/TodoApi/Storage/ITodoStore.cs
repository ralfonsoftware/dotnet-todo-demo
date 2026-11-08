using TodoApi.Domain;

namespace TodoApi.Storage;

/// <summary>
/// The Todos Store: the only source of truth for Todo Item state. Every member
/// may be called concurrently, and every timestamp is supplied by the caller
/// and stored as given.
/// </summary>
public interface ITodoStore
{
    /// <summary>
    /// Returns every Todo Item sorted by id ascending. The observability
    /// decorator calls this on each metric collection, so it should stay cheap.
    /// </summary>
    IReadOnlyList<TodoItem> GetAll();

    /// <summary>
    /// Returns the Todo Item with the given id, or null when it does not exist.
    /// </summary>
    TodoItem? Get(int id);

    /// <summary>
    /// Stores a new Todo Item built from the validated <paramref name="draft"/>
    /// under the next id — starting at 1, increasing by one, and never reused
    /// after Delete — and returns it. The supplied creation time is stored
    /// unchanged.
    /// </summary>
    TodoItem Create(TodoDraft draft, DateTimeOffset createdAt);

    /// <summary>
    /// Replaces the title, description, and priority of the Todo Item with the
    /// given id with those of the validated <paramref name="draft"/>, while
    /// preserving its id, creation time, and completion state. Returns null
    /// when it does not exist.
    /// </summary>
    TodoItem? Update(int id, TodoDraft draft);

    /// <summary>
    /// Applies Completion to the Todo Item with the given id. Completion is
    /// idempotent: repeating it keeps the original completion timestamp. The
    /// returned <see cref="CompletionResult"/> names the outcome.
    /// </summary>
    CompletionResult Complete(int id, DateTimeOffset completedAt);

    /// <summary>
    /// Removes and returns the Todo Item with the given id, or null when it
    /// does not exist.
    /// </summary>
    TodoItem? Delete(int id);
}
