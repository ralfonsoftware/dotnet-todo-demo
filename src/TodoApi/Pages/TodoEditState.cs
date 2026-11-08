using TodoApi.Domain;

namespace TodoApi.Pages;

/// <summary>The current values of the Todo Item open in the inline editor.</summary>
public sealed record TodoEditState(
    string Title,
    string Description,
    Priority Priority,
    DateOnly? DueDate);
