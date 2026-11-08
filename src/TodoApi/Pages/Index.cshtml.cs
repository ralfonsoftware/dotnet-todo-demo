using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Pages;

public sealed class IndexModel(ITodoStore store, TimeProvider clock, ILogger<IndexModel> logger) : PageModel
{
    private const string DefaultFilter = "open";

    private readonly ITodoStore _store = store;
    private readonly TimeProvider _clock = clock;
    private readonly BoardMutations _mutations = new(store, logger);
    private string _filter = DefaultFilter;

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

    public string EmptyTitle { get; private set; } = string.Empty;

    public string EmptyBody { get; private set; } = string.Empty;

    public bool DescriptionHasError =>
        ModelState.TryGetValue(nameof(NewDescription), out var entry) && entry.Errors.Count > 0;

    public string EditTitle { get; private set; } = string.Empty;

    public string EditDescription { get; private set; } = string.Empty;

    public Priority EditPriority { get; private set; } = TodoDraft.DefaultPriority;

    public string? EditError { get; private set; }

    public void OnGet()
    {
        var board = BoardReadModel.Create(_store.GetAll(), Filter, EditId, _clock.GetLocalNow());
        Apply(board);

        if (board.Edit is { } edit)
        {
            EditTitle = edit.Title;
            EditDescription = edit.Description;
            EditPriority = edit.Priority;
        }
        else
        {
            EditId = null;
        }
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
            _mutations.CreateRejected(errors.Count);

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

        var item = _mutations.Create(draft, _clock.GetUtcNow());

        TempData["Toast"] = $"Added \u201c{item.Title}\u201d.";
        return BackToBoard();
    }

    public IActionResult OnPostUpdate(int id, string? editTitle, string? editDescription, Priority? editPriority)
    {
        if (!TodoDraft.TryCreate(editTitle, editDescription, editPriority, out var draft, out var errors))
        {
            _mutations.UpdateRejected(id, errors.Count);

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

        if (_mutations.Update(id, draft) is null)
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
        var result = _mutations.Complete(id, _clock.GetUtcNow());
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
        var item = _mutations.Delete(id);
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

    private void Load() =>
        Apply(BoardReadModel.Create(_store.GetAll(), Filter, editId: null, _clock.GetLocalNow()));

    private void Apply(BoardReadModel board)
    {
        Items = board.Items;
        TotalCount = board.TotalCount;
        OpenCount = board.OpenCount;
        PercentDone = board.PercentDone;
        TodayLabel = board.TodayLabel;
        EmptyTitle = board.EmptyTitle;
        EmptyBody = board.EmptyBody;
    }

    private IActionResult BackToBoard() =>
        RedirectToPage("/Index", new { view = Filter });
}
