using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Logging;
using TodoApi.Storage;

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
        // Read first so the successful deletion can carry the Priority; the
        // store's Delete result still decides the response, so HTTP behaviour
        // is unchanged even if another request removes the item concurrently.
        var item = store.Get(id);

        if (!store.Delete(id))
        {
            TodoLogs.NotFound(logger, id, TodoOperations.Delete);
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.");
        }

        TodoLogs.DeleteSucceeded(logger, TodoOperations.Delete, id, item!.Priority);
        return TypedResults.NoContent();
    }
}
