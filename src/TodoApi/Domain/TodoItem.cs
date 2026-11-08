namespace TodoApi.Domain;

public sealed record TodoItem
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public Priority Priority { get; init; }

    public bool Done { get; init; }

    public TodoItem Complete(DateTimeOffset completedAt) =>
        Done ? this : this with { Done = true, CompletedAt = completedAt };
}
