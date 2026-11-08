using System.Globalization;
using TodoApi.Domain;

namespace TodoApi.Pages;

/// <summary>
/// Everything the board view reads: the filtered items, the counts, and the
/// labels. Built once per request from the Todos Store.
/// </summary>
public sealed record BoardReadModel(
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

    /// <summary>The number of Todo Items the given filter lists.</summary>
    public int CountFor(BoardFilter filter) =>
        filter switch
        {
            BoardFilter.Done => DoneCount,
            BoardFilter.All => TotalCount,
            _ => OpenCount
        };

    public static BoardReadModel Create(
        IReadOnlyList<TodoItem> all,
        BoardFilter filter,
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
            BoardFilter.All => [.. open, .. done],
            BoardFilter.Done => done,
            _ => open
        };

        var totalCount = all.Count;
        var openCount = all.Count(item => !item.Done);
        var percentDone = totalCount == 0
            ? 0
            : (int)Math.Round((totalCount - openCount) * 100d / totalCount, MidpointRounding.AwayFromZero);

        var (emptyTitle, emptyBody) = filter switch
        {
            BoardFilter.Done => ("Nothing completed yet", "Finished todos will collect here. Complete one to see it."),
            BoardFilter.All => ("Your list is empty", "Add your first todo above and it will show up here."),
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
