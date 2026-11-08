using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

internal static class GetTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/todos/{id:int}", Get)
            .WithName("GetTodo")
            .Produces<TodoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<Ok<TodoResponse>, ProblemHttpResult> Get(
        int id,
        ITodoStore store,
        ILogger<TodoLogCategory> logger)
    {
        var item = store.Get(id);

        if (item is null)
        {
            TodoLogs.NotFound(logger, id, TodoOperation.Get.Name);
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.");
        }

        TodoLogs.GetSucceeded(logger, TodoOperation.Get.Name, item.Id);
        return TypedResults.Ok(TodoResponse.From(item));
    }
}
