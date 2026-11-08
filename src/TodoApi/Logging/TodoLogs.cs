using TodoApi.Domain;

namespace TodoApi.Logging;

/// <summary>
/// Log category for Todo Item operations. Static handler classes cannot be
/// used as a generic <see cref="Microsoft.Extensions.Logging.ILogger{T}"/>
/// argument, so handlers inject this marker type instead.
/// </summary>
internal sealed class TodoLogCategory;

/// <summary>
/// Source-generated log events for Todo Item operations. Every record carries
/// named properties instead of interpolated text, and never includes a Todo
/// Item title or description.
/// </summary>
internal static partial class TodoLogs
{
    [LoggerMessage(
        EventId = TodoEventIds.CreateSucceeded,
        Level = LogLevel.Information,
        Message = "{Operation} succeeded for Todo Item {TodoId} with {Priority} priority.")]
    public static partial void CreateSucceeded(
        ILogger logger, string operation, int todoId, Priority priority);

    [LoggerMessage(
        EventId = TodoEventIds.UpdateSucceeded,
        Level = LogLevel.Information,
        Message = "{Operation} succeeded for Todo Item {TodoId} with {Priority} priority.")]
    public static partial void UpdateSucceeded(
        ILogger logger, string operation, int todoId, Priority priority);

    [LoggerMessage(
        EventId = TodoEventIds.CompleteSucceeded,
        Level = LogLevel.Information,
        Message = "{Operation} succeeded for Todo Item {TodoId} with {Priority} priority.")]
    public static partial void CompleteSucceeded(
        ILogger logger, string operation, int todoId, Priority priority);

    [LoggerMessage(
        EventId = TodoEventIds.CompleteAlreadyDone,
        Level = LogLevel.Information,
        Message = "{Operation} found Todo Item {TodoId} already done.")]
    public static partial void CompleteAlreadyDone(
        ILogger logger, string operation, int todoId);

    [LoggerMessage(
        EventId = TodoEventIds.DeleteSucceeded,
        Level = LogLevel.Information,
        Message = "{Operation} succeeded for Todo Item {TodoId} with {Priority} priority.")]
    public static partial void DeleteSucceeded(
        ILogger logger, string operation, int todoId, Priority priority);

    [LoggerMessage(
        EventId = TodoEventIds.NotFound,
        Level = LogLevel.Warning,
        Message = "Todo Item {TodoId} not found for {Operation}.")]
    public static partial void NotFound(ILogger logger, int todoId, string operation);

    [LoggerMessage(
        EventId = TodoEventIds.CreateValidationRejected,
        Level = LogLevel.Warning,
        Message = "{Operation} rejected with {ErrorCount} validation error(s).")]
    public static partial void CreateValidationRejected(ILogger logger, string operation, int errorCount);

    [LoggerMessage(
        EventId = TodoEventIds.UpdateValidationRejected,
        Level = LogLevel.Warning,
        Message = "{Operation} rejected for Todo Item {TodoId} with {ErrorCount} validation error(s).")]
    public static partial void UpdateValidationRejected(
        ILogger logger, string operation, int todoId, int errorCount);

    [LoggerMessage(
        EventId = TodoEventIds.GetSucceeded,
        Level = LogLevel.Debug,
        Message = "{Operation} succeeded for Todo Item {TodoId}.")]
    public static partial void GetSucceeded(ILogger logger, string operation, int todoId);

    [LoggerMessage(
        EventId = TodoEventIds.ListSucceeded,
        Level = LogLevel.Debug,
        Message = "{Operation} succeeded with {Count} Todo Items.")]
    public static partial void ListSucceeded(ILogger logger, string operation, int count);
}
