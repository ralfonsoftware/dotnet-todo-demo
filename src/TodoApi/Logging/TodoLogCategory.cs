namespace TodoApi.Logging;

/// <summary>
/// Log category for Todo Item operations. Static handler classes cannot be
/// used as a generic <see cref="Microsoft.Extensions.Logging.ILogger{T}"/>
/// argument, so the Todos Store decorator, the HTTP slices, and the board
/// inject this marker type instead. Every Todo Item event shares it.
/// </summary>
public sealed class TodoLogCategory;
