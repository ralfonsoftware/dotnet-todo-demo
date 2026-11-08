using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Pages;

/// <summary>
/// The board. Reads and writes go straight to the Todos Store, whose decorator
/// logs every outcome; the page logs only validation rejections (ADR-0006).
/// </summary>
public sealed class IndexModel(
    ITodoStore store,
    TimeProvider clock,
    ILogger<TodoLogCategory> logger) : PageModel
{
    private const string DefaultFilter = "open";

    private readonly ITodoStore _store = store;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<TodoLogCategory> _logger = logger;
    private string _filter = DefaultFilter;
    private BoardReadModel? _board;

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

    /// <summary>The board state the view renders. Every handler that renders the page loads it.</summary>
    public BoardReadModel Board =>
        _board ?? throw new InvalidOperationException("The board has not been loaded for this request.");

    public bool DescriptionHasError =>
        ModelState.TryGetValue(nameof(NewDescription), out var entry) && entry.Errors.Count > 0;

    public string EditTitle { get; private set; } = string.Empty;

    public string EditDescription { get; private set; } = string.Empty;

    public Priority EditPriority { get; private set; } = TodoDraft.DefaultPriority;

    public string? EditError { get; private set; }

    /// <summary>The zone board dates are shown in; the same one the "Today" label uses.</summary>
    public TimeZoneInfo TimeZone => _clock.LocalTimeZone;

    public void OnGet()
    {
        _board = BoardReadModel.Create(_store.GetAll(), Filter, EditId, _clock.GetLocalNow());

        if (_board.Edit is { } edit)
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
            TodoLogs.CreateValidationRejected(_logger, TodoOperation.Create.Name, errors.Count);

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

        var item = _store.Create(draft, _clock.GetUtcNow());

        TempData["Toast"] = $"Added \u201c{item.Title}\u201d.";
        return BackToBoard();
    }

    public IActionResult OnPostUpdate(int id, string? editTitle, string? editDescription, Priority? editPriority)
    {
        if (!ModelState.IsValid)
        {
            EditId = id;
            EditTitle = editTitle ?? string.Empty;
            EditDescription = editDescription ?? string.Empty;
            EditPriority = editPriority ?? TodoDraft.DefaultPriority;
            EditError = ModelState.TryGetValue(nameof(editPriority), out var priorityState) &&
                        priorityState is { Errors.Count: > 0 }
                ? "Choose a valid priority."
                : BoardErrors.FirstErrorMessage(ModelState);
            Load();
            return Page();
        }

        if (!TodoDraft.TryCreate(editTitle, editDescription, editPriority, out var draft, out var errors))
        {
            TodoLogs.UpdateValidationRejected(_logger, TodoOperation.Update.Name, id, errors.Count);

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

        if (_store.Update(id, draft) is null)
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

    private static string[] MessagesFor(
        Dictionary<string, string[]> errors, string key) =>
        errors.TryGetValue(key, out var messages) ? messages : [];

    private void Load() =>
        _board = BoardReadModel.Create(_store.GetAll(), Filter, editId: null, _clock.GetLocalNow());

    private RedirectToPageResult BackToBoard() =>
        RedirectToPage("/Index", new { view = Filter });
}
