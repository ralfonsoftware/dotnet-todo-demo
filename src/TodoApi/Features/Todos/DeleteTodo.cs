using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

internal static class DeleteTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/todos/{id:int}", Delete)
            .WithName("DeleteTodo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<NoContent, ProblemHttpResult> Delete(
        int id,
        ITodoStore store,
        ILogger<TodoLogCategory> logger)
    {
        var deleted = store.Delete(id);

        if (deleted is null)
        {
            return TodoResults.NotFound(logger, id, TodoOperation.Delete);
        }

        TodoLogs.DeleteSucceeded(logger, TodoOperation.Delete.Name, deleted.Id, deleted.Priority);
        return TypedResults.NoContent();
    }
}
