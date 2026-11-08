using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Pages;

public sealed class IndexModel : PageModel
{
    private const string DefaultFilter = "open";

    private readonly ITodoStore _store;
    private readonly TimeProvider _clock;
    private string _filter = DefaultFilter;

    public IndexModel(ITodoStore store, TimeProvider clock)
    {
        _store = store;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string Filter
    {
        get => _filter;
        set => _filter = NormalizeFilter(value);
    }

    [BindProperty(SupportsGet = true, Name = "edit")]
    public int? EditId { get; set; }

    [BindProperty]
    public string? NewTitle { get; set; }

    [BindProperty]
    public string? NewDescription { get; set; }

    [BindProperty]
    public Priority NewPriority { get; set; } = TodoDraft.DefaultPriority;

    public IReadOnlyList<TodoItem> Items { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int OpenCount { get; private set; }

    public int DoneCount => TotalCount - OpenCount;

    public int PercentDone { get; private set; }

    public string TodayLabel { get; private set; } = string.Empty;

    public bool DescriptionHasError =>
        ModelState.TryGetValue(nameof(NewDescription), out var entry) && entry.Errors.Count > 0;

    public string EditTitle { get; private set; } = string.Empty;

    public string EditDescription { get; private set; } = string.Empty;

    public Priority EditPriority { get; private set; } = TodoDraft.DefaultPriority;

    public string? EditError { get; private set; }

    public void OnGet()
    {
        if (EditId is { } id)
        {
            if (_store.Get(id) is { } item)
            {
                EditTitle = item.Title;
                EditDescription = item.Description ?? string.Empty;
                EditPriority = item.Priority;
            }
            else
            {
                EditId = null;
            }
        }

        Load();
    }

    public IActionResult OnPostCreate()
    {
        if (!ModelState.IsValid)
        {
            Load();
            return Page();
        }

        if (!TodoDraft.TryCreate(NewTitle, NewDescription, NewPriority, out var draft, out var errors))
        {
            foreach (var message in MessagesFor(errors, "title"))
            {
                ModelState.AddModelError(nameof(NewTitle), message);
            }

            foreach (var message in MessagesFor(errors, "description"))
            {
                ModelState.AddModelError(nameof(NewDescription), message);
            }

            Load();
            return Page();
        }

        var item = _store.Create(
            draft.Title,
            draft.Description,
            draft.Priority,
            _clock.GetUtcNow());

        TempData["Toast"] = $"Added \u201c{item.Title}\u201d.";
        return BackToBoard();
    }

    public IActionResult OnPostUpdate(int id, string? editTitle, string? editDescription, Priority? editPriority)
    {
        if (!TodoDraft.TryCreate(editTitle, editDescription, editPriority, out var draft, out var errors))
        {
            EditId = id;
            EditTitle = editTitle ?? string.Empty;
            EditDescription = editDescription ?? string.Empty;
            EditPriority = editPriority ?? TodoDraft.DefaultPriority;
            EditError = MessagesFor(errors, "title")
                .Concat(MessagesFor(errors, "description"))
                .First();
            Load();
            return Page();
        }

        if (_store.Update(id, draft.Title, draft.Description, draft.Priority) is null)
        {
            TempData["Toast"] = "That todo no longer exists.";
        }
        else
        {
            TempData["Toast"] = "Todo updated.";
        }

        return BackToBoard();
    }

    public IActionResult OnPostComplete(int id)
    {
        var result = _store.Complete(id, _clock.GetUtcNow());
        TempData["Toast"] = result switch
        {
            CompletionResult.Completed => "Done. One less thing to think about.",
            CompletionResult.AlreadyDone => "That todo was already done.",
            _ => "That todo no longer exists."
        };

        return BackToBoard();
    }

    public IActionResult OnPostDelete(int id)
    {
        var item = _store.Delete(id);
        TempData["Toast"] = item is null
            ? "That todo no longer exists."
            : $"Deleted \u201c{item.Title}\u201d.";

        return BackToBoard();
    }

    private static string NormalizeFilter(string? value) =>
        value is "all" or "done" or "open" ? value : DefaultFilter;

    private static IEnumerable<string> MessagesFor(
        Dictionary<string, string[]> errors, string key) =>
        errors.TryGetValue(key, out var messages) ? messages : [];

    private void Load()
    {
        var all = _store.GetAll();
        TotalCount = all.Count;
        OpenCount = all.Count(item => !item.Done);
        PercentDone = TotalCount == 0
            ? 0
            : (int)Math.Round(DoneCount * 100d / TotalCount, MidpointRounding.AwayFromZero);

        var open = all
            .Where(item => !item.Done)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .ToArray();

        var done = all
            .Where(item => item.Done)
            .OrderByDescending(item => item.CompletedAt)
            .ToArray();

        Items = Filter switch
        {
            "all" => [.. open, .. done],
            "done" => done,
            _ => open
        };

        TodayLabel = _clock.GetLocalNow().ToString("dddd, d MMMM", CultureInfo.InvariantCulture);
    }

    private IActionResult BackToBoard() =>
        RedirectToPage("/Index", new { view = Filter });
}
