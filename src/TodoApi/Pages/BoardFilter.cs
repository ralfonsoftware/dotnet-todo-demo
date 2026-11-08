namespace TodoApi.Pages;

/// <summary>
/// Which Todo Items the board lists. The lowercase URL value (<c>?view=done</c>)
/// exists only at the URL boundary; see <see cref="PresentationHooks.FilterValue"/>.
/// </summary>
public enum BoardFilter
{
    Open,
    Done,
    All
}
