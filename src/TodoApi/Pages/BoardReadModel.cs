using System.Globalization;
using TodoApi.Domain;

namespace TodoApi.Pages;

internal sealed record TodoEditState(string Title, string Description, Priority Priority);

internal sealed record BoardReadModel(
    IReadOnlyList<TodoItem> Items,
    int TotalCount,
    int OpenCount,
    int PercentDone,
    string TodayLabel,
    string EmptyTitle,
    string EmptyBody,
    TodoEditState? Edit)
{
    public int DoneCount => TotalCount - OpenCount;

    public static BoardReadModel Create(
        IReadOnlyList<TodoItem> all,
        string filter,
        int? editId,
        DateTimeOffset now)
    {
        var open = all
            .Where(item => !item.Done)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .ToArray();

        var done = all
            .Where(item => item.Done)
            .OrderByDescending(item => item.CompletedAt)
            .ToArray();

        IReadOnlyList<TodoItem> items = filter switch
        {
            "all" => [.. open, .. done],
            "done" => done,
            _ => open
        };

        var totalCount = all.Count;
        var openCount = all.Count(item => !item.Done);
        var percentDone = totalCount == 0
            ? 0
            : (int)Math.Round((totalCount - openCount) * 100d / totalCount, MidpointRounding.AwayFromZero);

        var (emptyTitle, emptyBody) = filter switch
        {
            "done" => ("Nothing completed yet", "Finished todos will collect here. Complete one to see it."),
            "all" => ("Your list is empty", "Add your first todo above and it will show up here."),
            _ => ("All clear", "There is nothing left to do. Add something new when it comes to mind.")
        };

        var edit = editId is { } id
            ? all.FirstOrDefault(item => item.Id == id) is { } item
                ? new TodoEditState(item.Title, item.Description ?? string.Empty, item.Priority)
                : null
            : null;

        return new BoardReadModel(
            items,
            totalCount,
            openCount,
            percentDone,
            now.ToString("dddd, d MMMM", CultureInfo.InvariantCulture),
            emptyTitle,
            emptyBody,
            edit);
    }
}
