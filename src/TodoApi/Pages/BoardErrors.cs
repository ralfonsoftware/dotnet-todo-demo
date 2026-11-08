using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TodoApi.Pages;

/// <summary>Turns model state into the single message the inline editor shows.</summary>
internal static class BoardErrors
{
    public const string Fallback = "Check the highlighted fields.";

    /// <summary>
    /// Returns the first non-empty error message, or <see cref="Fallback"/>.
    /// Errors built from exceptions carry an empty message, so a model state
    /// may hold errors without any text to show.
    /// </summary>
    public static string FirstErrorMessage(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return modelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? Fallback;
    }
}
