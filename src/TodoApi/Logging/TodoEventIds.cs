namespace TodoApi.Logging;

/// <summary>
/// Stable identifiers for Todo Item log events. The numbers are grouped by
/// outcome so a range is self-describing in the Aspire Dashboard:
/// <list type="bullet">
/// <item>1000–1099: successful mutations.</item>
/// <item>1100–1199: not-found outcomes.</item>
/// <item>1200–1299: validation rejections.</item>
/// <item>1300–1399: successful reads.</item>
/// </list>
/// Never renumber an existing id; treat these values as part of the
/// observability contract.
/// </summary>
public static class TodoEventIds
{
    public const int CreateSucceeded = 1001;
    public const int UpdateSucceeded = 1002;
    public const int CompleteSucceeded = 1003;
    public const int DeleteSucceeded = 1004;

    public const int NotFound = 1101;

    public const int ValidationRejected = 1201;

    public const int GetSucceeded = 1301;
    public const int ListSucceeded = 1302;
}

/// <summary>
/// Lowercase operation names carried in the <c>Operation</c> log property.
/// </summary>
public static class TodoOperations
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Complete = "complete";
    public const string Delete = "delete";
    public const string Get = "get";
    public const string List = "list";
}
