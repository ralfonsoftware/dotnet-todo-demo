using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class GetTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/todos/{id:int}", Get)
            .WithName("GetTodo")
            .Produces<TodoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<Ok<TodoResponse>, ProblemHttpResult> Get(int id, ITodoStore store)
    {
        var item = store.Get(id);
        return item is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.")
            : TypedResults.Ok(TodoResponse.From(item));
    }
}
