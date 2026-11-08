using System.ComponentModel.DataAnnotations;
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
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string? NewTitle { get; set; }

    [BindProperty]
    [StringLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    public string? NewDescription { get; set; }

    [BindProperty]
    public Priority NewPriority { get; set; } = Priority.Medium;

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

    public Priority EditPriority { get; private set; } = Priority.Medium;

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

        var item = _store.Create(
            NewTitle!.Trim(),
            string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription.Trim(),
            NewPriority,
            _clock.GetUtcNow());

        TempData["Toast"] = $"Added \u201c{item.Title}\u201d.";
        return BackToBoard();
    }

    public IActionResult OnPostUpdate(int id, string? editTitle, string? editDescription, Priority editPriority)
    {
        var title = editTitle?.Trim();
        var description = string.IsNullOrWhiteSpace(editDescription) ? null : editDescription.Trim();
        var error = ValidateEdit(title, description);

        if (error is not null)
        {
            EditId = id;
            EditTitle = editTitle ?? string.Empty;
            EditDescription = editDescription ?? string.Empty;
            EditPriority = editPriority;
            EditError = error;
            Load();
            return Page();
        }

        if (_store.Update(id, title!, description, editPriority) is null)
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

    private static string? ValidateEdit(string? title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }

        if (title.Length > 200)
        {
            return "Title must be 200 characters or fewer.";
        }

        if (description is { Length: > 2000 })
        {
            return "Description must be 2000 characters or fewer.";
        }

        return null;
    }

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
