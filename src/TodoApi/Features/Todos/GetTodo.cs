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

    private static Results<Ok<TodoResponse>, ProblemHttpResult> Get(int id, ITodoStore store) =>
        store.Get(id) is { } item
            ? TypedResults.Ok(TodoResponse.From(item))
            : TodoResults.NotFound();
}
