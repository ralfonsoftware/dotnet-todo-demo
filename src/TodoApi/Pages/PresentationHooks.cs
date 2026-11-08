using TodoApi.Domain;

namespace TodoApi.Pages;

/// <summary>
/// The presentation contract shared by the board view, stylesheet, and
/// JavaScript: the ids and Priority class names that couple them, and the
/// <c>view</c> query values that name a <see cref="BoardFilter"/> in URLs.
/// </summary>
public static class PresentationHooks
{
    public const string NewTitleId = "NewTitle";

    public static string PriorityModifier(Priority priority) =>
        priority.ToString().ToLowerInvariant();

    public static string BadgeClass(Priority priority) =>
        $"badge badge--{PriorityModifier(priority)}";

    public static string PriorityOptionClass(Priority priority) =>
        $"prio-option prio--{PriorityModifier(priority)}";

    /// <summary>The <c>view</c> query value for a board filter.</summary>
    public static string FilterValue(BoardFilter filter) =>
        filter switch
        {
            BoardFilter.Done => "done",
            BoardFilter.All => "all",
            _ => "open"
        };

    /// <summary>
    /// Parses a <c>view</c> query value. Unknown or missing values fall back to
    /// <see cref="BoardFilter.Open"/>.
    /// </summary>
    public static BoardFilter ParseFilter(string? value) =>
        value switch
        {
            "done" => BoardFilter.Done,
            "all" => BoardFilter.All,
            _ => BoardFilter.Open
        };
}
