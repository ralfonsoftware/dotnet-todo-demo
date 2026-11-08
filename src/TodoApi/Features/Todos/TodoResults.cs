using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Logging;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

internal static class TodoResults
{
    public static ProblemHttpResult NotFound(
        ILogger<TodoLogCategory> logger, int id, TodoOperation operation)
    {
        TodoLogs.NotFound(logger, id, operation.Name);
        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Todo item not found.");
    }
}
