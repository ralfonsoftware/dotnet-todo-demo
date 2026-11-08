using TodoApi.Domain;

namespace TodoApi.Pages;

/// <summary>
/// The presentation contract shared by the board view, stylesheet, and
/// JavaScript: the ids and Priority class names that couple them.
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
}
