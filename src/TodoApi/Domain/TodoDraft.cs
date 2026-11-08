using System.Diagnostics.CodeAnalysis;

namespace TodoApi.Domain;

/// <summary>
/// The title, optional description, and priority supplied when creating or
/// editing a Todo Item, normalised and validated before it reaches the Todos
/// Store.
/// </summary>
public sealed record TodoDraft
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const Priority DefaultPriority = Priority.Medium;

    public required string Title { get; init; }

    public string? Description { get; init; }

    public Priority Priority { get; init; } = DefaultPriority;

    public static bool TryCreate(
        string? title,
        string? description,
        Priority? priority,
        [NotNullWhen(true)] out TodoDraft? draft,
        out Dictionary<string, string[]> errors)
    {
        var normalizedTitle = title?.Trim();
        var normalizedDescription =
            string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        errors = [];

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            errors["title"] = ["Title is required."];
        }
        else if (normalizedTitle.Length > TitleMaxLength)
        {
            errors["title"] = [$"Title must be {TitleMaxLength} characters or fewer."];
        }

        if (normalizedDescription is { Length: > DescriptionMaxLength })
        {
            errors["description"] = [$"Description must be {DescriptionMaxLength} characters or fewer."];
        }

        if (errors.Count > 0)
        {
            draft = null;
            return false;
        }

        draft = new TodoDraft
        {
            Title = normalizedTitle!,
            Description = normalizedDescription,
            Priority = priority ?? DefaultPriority
        };

        return true;
    }
}
