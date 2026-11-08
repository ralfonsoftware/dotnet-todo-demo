using TodoApi.Domain;

namespace TodoApi.Tests;

/// <summary>
/// Builds a validated <see cref="TodoDraft"/> for seeding and store tests,
/// throwing when the input would be rejected.
/// </summary>
internal static class Drafts
{
    public static TodoDraft Of(
        string title = "Seeded todo",
        string? description = null,
        Priority priority = TodoDraft.DefaultPriority)
    {
        if (!TodoDraft.TryCreate(title, description, priority, out var draft, out var errors))
        {
            throw new ArgumentException(
                $"Invalid Todo Draft: {string.Join(" ", errors.SelectMany(error => error.Value))}");
        }

        return draft;
    }
}
