using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class CompleteTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/todos/{id:int}/complete", Complete)
            .WithName("CompleteTodo")
            .Produces<TodoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<Ok<TodoResponse>, ProblemHttpResult> Complete(
        int id,
        ITodoStore store,
        TimeProvider clock)
    {
        var item = store.Complete(id, clock.GetUtcNow());
        return item is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.")
            : TypedResults.Ok(TodoResponse.From(item));
    }
}
