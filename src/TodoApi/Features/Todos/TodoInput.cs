namespace TodoApi.Features.Todos;

internal static class TodoInput
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public static string NormalizeTitle(string title) => title.Trim();

    public static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public static Dictionary<string, string[]> Validate(string? title, string? description)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (title.Trim().Length > TitleMaxLength)
        {
            errors["title"] = [$"Title must be {TitleMaxLength} characters or fewer."];
        }

        if (description is not null && description.Trim().Length > DescriptionMaxLength)
        {
            errors["description"] = [$"Description must be {DescriptionMaxLength} characters or fewer."];
        }

        return errors;
    }
}
