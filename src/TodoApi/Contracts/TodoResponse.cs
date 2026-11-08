using TodoApi.Domain;

namespace TodoApi.Contracts;

public sealed record TodoResponse(
    int Id,
    string Title,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Priority Priority,
    bool Done,
    DateOnly? DueDate)
{
    public static TodoResponse From(TodoItem item) => new(
        item.Id,
        item.Title,
        item.Description,
        item.CreatedAt,
        item.CompletedAt,
        item.Priority,
        item.Done,
        item.DueDate);
}
